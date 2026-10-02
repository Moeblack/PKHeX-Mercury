using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

public enum MercuryEncounterSource
{
    OrdinaryTable,
    Swarm,
    Broadcast,
    BroadcastFallback,
}

/// <summary>A physical record from external research; absent fields remain null, never inferred.</summary>
public sealed record MercuryEncounterRecord(
    MercuryEncounterSource Source,
    string Category,
    int Species,
    int? MapGroup,
    int? MapNumber,
    int? MapSection,
    int? MinLevel,
    int? MaxLevel,
    string? TimeBand,
    string? HourBand,
    int? DayOfWeek,
    int? Slot,
    string? TableAddress,
    string? EntryAddress,
    string? InfoAddress,
    string? WildAddress)
{
    public string DynamicConditions => "未判定：时段、群聚、广播门控及槽选择规则的组合约束未完整核对。";
}

/// <summary>
/// Imported research whose SHA declaration matches the requested supported ROM.
/// This does not attest that the JSON rows have been compared with ROM bytes.
/// </summary>
public sealed class MercuryEncounterEvidence
{
    public string RomSha256 { get; }
    public string SourcePath { get; }
    public string EvidenceJson { get; }
    public string BroadcastEvidenceJson { get; }
    public string BroadcastGatingJson { get; }
    public string UnresolvedJson { get; }
    public IReadOnlyList<MercuryEncounterRecord> Records { get; }
    public string Provenance => "遭遇资料的ROM SHA来源声明匹配；未进行全表ROM字节复核。";

    internal MercuryEncounterEvidence(string sha, string path, string evidence, string broadcastEvidence,
        string broadcastGating, string unresolved, List<MercuryEncounterRecord> records)
    {
        RomSha256 = sha;
        SourcePath = path;
        EvidenceJson = evidence;
        BroadcastEvidenceJson = broadcastEvidence;
        BroadcastGatingJson = broadcastGating;
        UnresolvedJson = unresolved;
        Records = records.AsReadOnly();
    }
}
