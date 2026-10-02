using System;
using System.Collections.Generic;
using System.Linq;

namespace PKHeX.Mercury.Core;

/// <summary>Status of an evidence-scoped Mercury check, not a retail legality verdict.</summary>
public enum MercuryCheckStatus
{
    Unknown,
    Pass,
    Invalid,
}

/// <summary>A stable rule identifier, its limited conclusion, and the supporting evidence or gap.</summary>
public sealed record MercuryLegalityCheck(string Code, MercuryCheckStatus Status, string Evidence);

/// <summary>Aggregates only the checks supplied; even Pass does not certify complete legality.</summary>
public sealed class MercuryLegalityResult
{
    public IReadOnlyList<MercuryLegalityCheck> Checks { get; }
    public MercuryCheckStatus Status { get; }

    public string Summary => Status switch
    {
        MercuryCheckStatus.Invalid => "已检查字段存在无效值；未覆盖规则仍未知。",
        MercuryCheckStatus.Pass => "已检查字段通过；不代表完整合法性。",
        _ => "未知：证据或规则覆盖不足，不能判定完整合法性。",
    };

    public MercuryLegalityResult(IEnumerable<MercuryLegalityCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        var snapshot = checks.ToArray();
        Checks = Array.AsReadOnly(snapshot);
        Status = snapshot.Any(z => z.Status == MercuryCheckStatus.Invalid) ? MercuryCheckStatus.Invalid
            : snapshot.Length == 0 || snapshot.Any(z => z.Status == MercuryCheckStatus.Unknown) ? MercuryCheckStatus.Unknown
            : MercuryCheckStatus.Pass;
    }
}
