using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>Retains the upstream slot identity and its evidence-scoped individual result.</summary>
public sealed record MercuryBulkLegalityEntry(SlotCache Slot, MercuryLegalityResult Result);

/// <summary>A snapshot of nonempty slot checks, not a complete game legality verdict.</summary>
public sealed class MercuryBulkLegalityResult
{
    public IReadOnlyList<MercuryBulkLegalityEntry> Entries { get; }
    public int Count => Entries.Count;
    public int PassCount { get; }
    public int UnknownCount { get; }
    public int InvalidCount { get; }
    public MercuryCheckStatus Status { get; }

    public string Summary => Count == 0
        ? "无可检查个体（非空总数：0）；不代表完整游戏合法性。"
        : $"已检查非空总数：{Count}；通过：{PassCount}；未知：{UnknownCount}；无效：{InvalidCount}。"
            + $"\n水银已覆盖适用检查：{Status}；不代表完整游戏合法性。";

    public MercuryBulkLegalityResult(IEnumerable<MercuryBulkLegalityEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var snapshot = entries.ToArray();
        Entries = Array.AsReadOnly(snapshot);
        PassCount = snapshot.Count(z => z.Result.Status == MercuryCheckStatus.Pass);
        UnknownCount = snapshot.Count(z => z.Result.Status == MercuryCheckStatus.Unknown);
        InvalidCount = snapshot.Count(z => z.Result.Status == MercuryCheckStatus.Invalid);
        Status = InvalidCount != 0 ? MercuryCheckStatus.Invalid
            : snapshot.Length == 0 || UnknownCount != 0 ? MercuryCheckStatus.Unknown
            : MercuryCheckStatus.Pass;
    }

    /// <summary>Full clipboard report, including every unknown check and supporting evidence.</summary>
    public string Report()
    {
        var text = new StringBuilder(Summary);
        foreach (var entry in Entries)
        {
            text.AppendLine().AppendLine().AppendLine(entry.Slot.Identify());
            text.Append(entry.Result.Status).Append("：").AppendLine(entry.Result.Summary);
            foreach (var check in entry.Result.Checks)
            {
                string role = check.Role == MercuryCheckRole.Required
                    ? "Required 适用检查" : "Diagnostic 辅助诊断（不单独决定总结果）";
                text.Append('[').Append(check.Code).Append("] ").Append(role).Append(" / ")
                    .Append(check.Status).Append(": ").AppendLine(check.Evidence);
            }
        }
        return text.ToString();
    }
}
