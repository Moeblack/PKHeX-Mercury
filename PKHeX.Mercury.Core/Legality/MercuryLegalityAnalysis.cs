using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

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

    /// <summary>Checks stored fields; table-dependent checks need loaded data, while RNG correlation is purely numeric.</summary>
    public static MercuryLegalityResult Analyze(MercuryPokemon pk, MercuryGameData? data, MercuryEncounterEvidence? encounterEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(pk);
        var distribution = MercuryDistributionCatalog.Match(pk);
        var ordinarySourceRole = distribution is null ? MercuryCheckRole.Required : MercuryCheckRole.Diagnostic;
        bool hasRom = data is { HasSprites: true } && data.Source != "numeric"
            && data.RomVersion?.CanReadGameData == true;
        bool speciesData = hasRom && HasShape(data!.Species, MercuryRomLayout.SpeciesCount, z => z.Id);
        bool moveData = hasRom && HasShape(data!.Moves, MercuryRomLayout.MoveCount, z => z.Id);
        bool itemData = hasRom && HasShape(data!.Items, MercuryRomLayout.ItemCount, z => z.Id);
        var checks = new List<MercuryLegalityCheck>
        {
            CheckRange("species.range", pk.Species, MercuryRomLayout.SpeciesCount, speciesData,
                "内部species ID", "0为空记录，不是非法种类；不对空槽推断获取来源"),
            CheckRange("held-item.range", pk.HeldItem, MercuryRomLayout.ItemCount, itemData,
                "携带道具表索引", "0表示未携带道具"),
            CheckMethod1(pk) with { Role = MercuryCheckRole.Diagnostic },
        };

        MercurySpecies? species = speciesData && pk.Species > 0 && pk.Species < MercuryRomLayout.SpeciesCount
            ? data!.Species[pk.Species] : null;
        Learnset? levelUp = species is null ? null : data!.GetLevelUpLearnset(pk.Species);
        for (int i = 0; i < 4; i++)
        {
            int move = pk.Moves[i];
            string code = $"move.{i + 1}";
            checks.Add(CheckRange($"{code}.range", move, MercuryRomLayout.MoveCount, moveData, "招式ID", "0为空招式"));
            checks.Add(CheckLearning($"{code}.known-learning", move, species, levelUp, speciesData && moveData)
                with { Role = ordinarySourceRole });
        }

        var distributionCheck = distribution is null
            ? new MercuryLegalityCheck("distribution.reference", MercuryCheckStatus.Unknown,
                "未匹配固定版本的4条公开配信原始内容；升级、改名或进化后内容可能不同，未匹配不代表非法；不猜测可变字段。", MercuryCheckRole.Diagnostic)
            : new MercuryLegalityCheck("distribution.reference", MercuryCheckStatus.Pass,
                $"完整58字节内容匹配公开配信「{distribution.Label}」；来源：{distribution.SourceUrl}；commit={distribution.SourceCommit}。"
                + "仅证明参考内容一致，不证明真实领取或身份，不是密码学认证，也不代表完整合法。", MercuryCheckRole.Diagnostic);
        checks.Add(distributionCheck);

        string encounterReport;
        var fieldMatchStatus = MercuryCheckStatus.Unknown;
        if (hasRom && data!.RomVersion != MercuryRomVersion.V1_1)
            encounterReport = "当前ROM版本不支持现有遭遇证据（仅支持水银1.1）；范围与学习检查仍可使用当前版本ROM资料。" + MercuryEncounterMatcher.CoverageGap;
        else if (encounterEvidence is not null && (!hasRom || !string.Equals(data!.RomSha256, encounterEvidence.RomSha256, StringComparison.OrdinalIgnoreCase)))
            encounterReport = "未使用遭遇候选证据：缺少有效的当前ROM资料或其SHA与遭遇来源声明不一致。" + MercuryEncounterMatcher.CoverageGap;
        else
        {
            var match = MercuryEncounterMatcher.Match(pk.Species, pk.MetLocation, pk.MetLevel, encounterEvidence);
            encounterReport = match.Evidence;
            fieldMatchStatus = match.FieldMatchStatus;
        }
        checks.Add(new MercuryLegalityCheck("encounter.record-fields", fieldMatchStatus,
            "普通遭遇记录诊断（不含公开配信参考）：" + encounterReport, ordinarySourceRole));
        checks.Add(distribution is null
            ? new MercuryLegalityCheck("encounter.source", MercuryCheckStatus.Unknown, encounterReport)
            : new MercuryLegalityCheck("encounter.source", MercuryCheckStatus.Pass,
                "公开配信原始内容来源匹配（仅限完整58字节参考内容）；" + distributionCheck.Evidence));
        return new MercuryLegalityResult(checks);
    }

    private static MercuryLegalityCheck CheckMethod1(MercuryPokemon pk)
    {
        const string code = "rng.method1";
        if (pk.Species == 0)
            return new(code, MercuryCheckStatus.Unknown, "species 0为空槽，不对空记录推断个体PID/IV相关性。");

        // IVs exposes six five-bit values in HP/Atk/Def/Spe/SpA/SpD order, without egg/ability flags.
        var ivs = pk.IVs;
        uint iv32 = (uint)ivs[0] | ((uint)ivs[1] << 5) | ((uint)ivs[2] << 10)
            | ((uint)ivs[3] << 15) | ((uint)ivs[4] << 20) | ((uint)ivs[5] << 25);
        if (MethodFinder.GetLCRNGMethod1Match(pk.PID, iv32, out uint seed))
            return new(code, MercuryCheckStatus.Pass,
                $"符合基础Method1数值相关性，不证明该来源必用此方法，也不代表完整合法；数值候选seed=0x{seed:X8}，不是已还原的真实捕获种子。");
        return new(code, MercuryCheckStatus.Unknown,
            "未匹配基础Method1数值相关性；CFRU同步性格、闪光后处理或IV覆盖可能打破该关系，不能据此判为非法，也不代表完整来源已核对。");
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

    private static MercuryLegalityCheck CheckLearning(string code, int move, MercurySpecies? species, Learnset? levelUp, bool hasData)
    {
        if (!hasData)
            return new(code, MercuryCheckStatus.Unknown, MissingData);
        if (move == 0)
            return new(code, MercuryCheckStatus.Pass, $"{DataEvidence}；0为空招式，无需学习来源匹配。");
        if (species is null || (uint)move >= MercuryRomLayout.MoveCount)
            return new(code, MercuryCheckStatus.Unknown, "空记录或索引不在已覆盖范围内，无法匹配当前物种学习表。" + LearningGap);

        string adapterGap = levelUp is null
            ? " levelUp含无法无损表示为ushort招式ID/byte等级的条目，该来源匹配为Unknown；原始值未截断。"
            : string.Empty;
        var sources = new List<string>(3);
        if (levelUp?.GetIsLearn((ushort)move) == true)
            sources.Add("levelUp");
        if (species.MachineMoves.Contains(move))
            sources.Add("tmhm");
        if (species.TutorMoves.Contains(move))
            sources.Add("tutor");
        if (sources.Count != 0)
            return new(code, MercuryCheckStatus.Pass,
                $"{DataEvidence}；招式{move}在当前物种{species.Id}的已知学习表中（{string.Join("/", sources)}，已解析move ID）。"
                + "仅证明当前资料的已知学习表命中，不证明学习等级、实际学习过程或获取来源。" + adapterGap);
        return new(code, MercuryCheckStatus.Unknown,
            $"{DataEvidence}；招式{move}未命中当前物种{species.Id}的可用levelUp/tmhm/tutor。{adapterGap}{LearningGap}");
    }
}
