using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PKHeX.Mercury.Core;

/// <summary>Candidate evidence only. Neither a match nor its absence determines complete encounter legality.</summary>
public sealed class MercuryEncounterMatchResult
{
    public MercuryCheckStatus Status => MercuryCheckStatus.Unknown;
    public IReadOnlyList<MercuryEncounterRecord> Candidates { get; }
    public IReadOnlyList<MercuryEncounterRecord> DynamicRecords { get; }
    public string Evidence { get; }

    internal MercuryEncounterMatchResult(List<MercuryEncounterRecord> candidates, List<MercuryEncounterRecord> dynamicRecords, string evidence)
    {
        Candidates = candidates.AsReadOnly();
        DynamicRecords = dynamicRecords.AsReadOnly();
        Evidence = evidence;
    }
}

public static class MercuryEncounterMatcher
{
    public const string CoverageGap = "来源仍为Unknown：事件、进化前物种、等级变化、蛋及完整获取链尚未闭合；"
        + "未命中不代表非法。训练家敌方队伍不作为玩家来源；不凭当前时钟或存档猜测捕获时的动态条件。";

    /// <summary>
    /// Matches only recorded species, mapsec (= supplied MetLocation) and inclusive level range.
    /// Callers must pass null for an unknown location/level, rather than substituting zero.
    /// Dynamic rows remain separate even when their species matches.
    /// </summary>
    public static MercuryEncounterMatchResult Match(int species, int? metLocation, int? metLevel, MercuryEncounterEvidence? evidence)
    {
        if (evidence is null)
            return new([], [], "未导入遭遇证据，无法匹配来源候选。" + CoverageGap);

        var candidates = new List<MercuryEncounterRecord>();
        var dynamicRecords = evidence.Records.Where(z => z.Source != MercuryEncounterSource.OrdinaryTable && z.Species == species).ToList();
        var report = new StringBuilder().AppendLine(evidence.Provenance);
        if (species <= 0)
            report.AppendLine("空记录或未知species，无法匹配来源候选。");
        else if (metLocation is null || metLevel is null)
            report.AppendLine($"缺少已知记录字段，无法完整匹配：MetLocation={Value(metLocation)}，MetLevel={Value(metLevel)}；不以0补齐。");
        else
        {
            candidates.AddRange(evidence.Records.Where(z => z.Source == MercuryEncounterSource.OrdinaryTable
                && z.Species == species && z.MapSection is not null && z.MapSection == metLocation
                && z.MinLevel is not null && z.MaxLevel is not null && z.MinLevel <= z.MaxLevel
                && metLevel >= z.MinLevel && metLevel <= z.MaxLevel));
            report.AppendLine(candidates.Count == 0
                ? "当前表未找到同时符合species、MetLocation/mapsec与MetLevel范围的普通候选槽；可能存在未覆盖或缺字段记录。"
                : $"当前表存在候选槽：{candidates.Count}条普通槽记录；仅记录字段相符，不证明实际获取来源。");
        }
        foreach (var candidate in candidates)
            report.AppendLine(Describe(candidate));

        if (dynamicRecords.Count != 0)
        {
            report.AppendLine($"动态记录存在：{dynamicRecords.Count}条species相同的原始记录，未计入严格候选数量。"
                + "缺少等级/地点映射等必要证据，无法完整匹配；动态条件未判定。");
            foreach (var dynamicRecord in dynamicRecords)
                report.AppendLine(Describe(dynamicRecord));
        }
        report.Append(CoverageGap);
        return new(candidates, dynamicRecords, report.ToString());
    }

    private static string Describe(MercuryEncounterRecord record)
        => $"{record.Category}: species={record.Species}, map={Value(record.MapGroup)}/{Value(record.MapNumber)}, "
            + $"mapsec={Value(record.MapSection)}, level={Value(record.MinLevel)}..{Value(record.MaxLevel)}, "
            + $"时段={record.TimeBand ?? "未记录"} ({record.HourBand ?? "未记录"}), weekday={Value(record.DayOfWeek)}, slot={Value(record.Slot)}; "
            + $"table={record.TableAddress ?? "未记录"}, entry={record.EntryAddress ?? "未记录"}, "
            + $"info={record.InfoAddress ?? "未记录"}, wild={record.WildAddress ?? "未记录"}; {record.DynamicConditions}";

    private static string Value(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "未记录";
}
