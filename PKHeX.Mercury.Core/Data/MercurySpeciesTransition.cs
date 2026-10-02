using System;
using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

/// <summary>
/// A consumer-proven conditional species transition and its table reverse target, not an editable Form option.
/// A real party-species write does not prove that the converted species persists outside that lifecycle.
/// </summary>
public sealed record MercurySpeciesTransition
{
    internal MercurySpeciesTransition(ushort source, ushort target, ushort methodRaw, ushort paramRaw,
        string condition, ushort? reverseTarget, uint[] evidence, ushort auxRaw, byte physicalSlot, uint recordAddress)
    {
        Source = source;
        Target = target;
        MethodRaw = methodRaw;
        ParamRaw = paramRaw;
        AuxRaw = auxRaw;
        PhysicalSlot = physicalSlot;
        RecordAddress = recordAddress;
        Condition = condition;
        ReverseTarget = reverseTarget;
        Evidence = Array.AsReadOnly(evidence);
    }

    public ushort Source { get; }
    public ushort Target { get; }
    public ushort MethodRaw { get; }
    public ushort ParamRaw { get; }
    public ushort AuxRaw { get; }

    /// <summary>Physical slot in the 16-record V1_1 block, not an index in a filtered list.</summary>
    public byte PhysicalSlot { get; }
    public uint RecordAddress { get; }

    /// <summary>Proven conditions and outstanding lifecycle limits; this is not a predicate for editing.</summary>
    public string Condition { get; }

    /// <summary>
    /// Target of the sole same-method, param-zero record in a forward target's table; not necessarily Source.
    /// Null for multiple reverse records, missing reverse records, or a reverse record itself.
    /// This table result does not include the bank-context backup override.
    /// </summary>
    public ushort? ReverseTarget { get; }

    /// <summary>ROM addresses for the records, consumers, species writes and normalization-before-copy lifecycle.</summary>
    public IReadOnlyList<uint> Evidence { get; }

    /// <summary>Conditional writes and pre-copy reversal do not establish a permanent editable form.</summary>
    public bool PersistenceUnknown => true;
}
