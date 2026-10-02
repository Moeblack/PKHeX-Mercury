using System;
using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

/// <summary>
/// A narrowly proven conditional species transition and its reverse target, not an editable Form option.
/// A real party-species write does not prove that the converted species persists outside that lifecycle.
/// </summary>
public sealed record MercurySpeciesTransition
{
    internal MercurySpeciesTransition(ushort source, ushort target, ushort methodRaw, ushort paramRaw,
        string condition, ushort? reverseTarget, uint[] evidence)
    {
        Source = source;
        Target = target;
        MethodRaw = methodRaw;
        ParamRaw = paramRaw;
        Condition = condition;
        ReverseTarget = reverseTarget;
        Evidence = Array.AsReadOnly(evidence);
    }

    public ushort Source { get; }
    public ushort Target { get; }
    public ushort MethodRaw { get; }
    public ushort ParamRaw { get; }

    /// <summary>Proven conditions and outstanding lifecycle limits; this is not a predicate for editing.</summary>
    public string Condition { get; }

    /// <summary>Proven fallback target of a forward conversion; null for fallback records, whose re-entry needs separate conditions.</summary>
    public ushort? ReverseTarget { get; }

    /// <summary>ROM addresses for the records, consumers, species writes and normalization-before-copy lifecycle.</summary>
    public IReadOnlyList<uint> Evidence { get; }

    /// <summary>All currently admitted samples have conditional writes and pre-copy reversal, not proven permanent forms.</summary>
    public bool PersistenceUnknown => true;
}
