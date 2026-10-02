using System;
using System.Collections.Generic;
using System.Linq;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Evidence-scoped Mercury checks. Does not invoke retail legality or infer obtainability from absence.
/// </summary>
public static class MercuryLegalityAnalysis
{
    private const string DataEvidence = "当前已加载资料；ROM缓存版本已验证（不表示资料全表已与ROM逐字段复核）";
    private const string MissingData = "缺少有效的当前水银ROM资料：需要非numeric来源、已验证ROM缓存、支持的SHA及完整表ID形状；该项未知。";
    private const string LearningGap = "蛋招式、进化前招式、事件及获取方式尚未闭合；未命中不能判为非法。";

    public static MercuryLegalityResult Analyze(MercuryPKM pk, MercuryEncounterEvidence? encounterEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(pk);
        return Analyze(pk.ToMercuryPokemon(), pk.GameData, encounterEvidence);
    }

    /// <summary>Checks native stored identifiers against the already loaded data; null data stays unknown.</summary>
    public static MercuryLegalityResult Analyze(MercuryPokemon pk, MercuryGameData? data, MercuryEncounterEvidence? encounterEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(pk);
        bool hasRom = data is { HasSprites: true } && data.Source != "numeric"
            && string.Equals(data.RomSha256, MercuryRomLayout.ExpectedSha256, StringComparison.OrdinalIgnoreCase);
        bool speciesData = hasRom && HasShape(data!.Species, MercuryRomLayout.SpeciesCount, z => z.Id);
        bool moveData = hasRom && HasShape(data!.Moves, MercuryRomLayout.MoveCount, z => z.Id);
        bool itemData = hasRom && HasShape(data!.Items, MercuryRomLayout.ItemCount, z => z.Id);
        var checks = new List<MercuryLegalityCheck>
        {
            CheckRange("species.range", pk.Species, MercuryRomLayout.SpeciesCount, speciesData,
                "内部species ID", "0为空记录，不是非法种类；不对空槽推断获取来源"),
            CheckRange("held-item.range", pk.HeldItem, MercuryRomLayout.ItemCount, itemData,
                "携带道具表索引", "0表示未携带道具"),
        };

        MercurySpecies? species = speciesData && pk.Species > 0 && pk.Species < MercuryRomLayout.SpeciesCount
            ? data!.Species[pk.Species] : null;
        for (int i = 0; i < 4; i++)
        {
            int move = pk.Moves[i];
            string code = $"move.{i + 1}";
            checks.Add(CheckRange($"{code}.range", move, MercuryRomLayout.MoveCount, moveData, "招式ID", "0为空招式"));
            checks.Add(CheckLearning($"{code}.known-learning", move, species, speciesData && moveData));
        }

        string encounterReport;
        if (encounterEvidence is not null && (!hasRom || !string.Equals(data!.RomSha256, encounterEvidence.RomSha256, StringComparison.OrdinalIgnoreCase)))
            encounterReport = "未使用遭遇候选证据：缺少有效的当前ROM资料或其SHA与遭遇来源声明不一致。" + MercuryEncounterMatcher.CoverageGap;
        else
            encounterReport = MercuryEncounterMatcher.Match(pk.Species, pk.MetLocation, pk.MetLevel, encounterEvidence).Evidence;
        checks.Add(new MercuryLegalityCheck("encounter.source", MercuryCheckStatus.Unknown, encounterReport));
        return new MercuryLegalityResult(checks);
    }

    private static bool HasShape<T>(IReadOnlyList<T> table, int count, Func<T, int> getId)
    {
        if (table.Count != count)
            return false;
        for (int i = 0; i < count; i++)
        {
            if (getId(table[i]) != i)
                return false;
        }
        return true;
    }

    private static MercuryLegalityCheck CheckRange(string code, int value, int count, bool hasData, string label, string empty)
    {
        if (!hasData)
            return new(code, MercuryCheckStatus.Unknown, MissingData);
        bool inRange = (uint)value < (uint)count;
        string detail = value == 0 ? empty : inRange ? "仅通过索引范围检查，不证明可获取性" : "超出已覆盖的存储/表索引范围";
        return new(code, inRange ? MercuryCheckStatus.Pass : MercuryCheckStatus.Invalid,
            $"{DataEvidence}；水银profile表与存储契约：{label}范围0..{count - 1}，当前值{value}；{detail}。");
    }

    private static MercuryLegalityCheck CheckLearning(string code, int move, MercurySpecies? species, bool hasData)
    {
        if (!hasData)
            return new(code, MercuryCheckStatus.Unknown, MissingData);
        if (move == 0)
            return new(code, MercuryCheckStatus.Pass, $"{DataEvidence}；0为空招式，无需学习来源匹配。");
        if (species is null || (uint)move >= MercuryRomLayout.MoveCount)
            return new(code, MercuryCheckStatus.Unknown, "空记录或索引不在已覆盖范围内，无法匹配当前物种学习表。" + LearningGap);

        var sources = new List<string>(3);
        if (species.LevelUpMoves.Any(z => z.Move == move))
            sources.Add("levelUp");
        if (species.MachineMoves.Contains(move))
            sources.Add("tmhm");
        if (species.TutorMoves.Contains(move))
            sources.Add("tutor");
        if (sources.Count != 0)
            return new(code, MercuryCheckStatus.Pass,
                $"{DataEvidence}；招式{move}在当前物种{species.Id}的已知学习表中（{string.Join("/", sources)}，已解析move ID）。"
                + "仅证明当前资料的已知学习表命中，不证明学习等级、实际学习过程或获取来源。");
        return new(code, MercuryCheckStatus.Unknown,
            $"{DataEvidence}；招式{move}未命中当前物种{species.Id}的levelUp/tmhm/tutor。{LearningGap}");
    }
}
