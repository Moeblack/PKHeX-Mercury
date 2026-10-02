using System;
using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

/// <summary>Evidence-backed mechanisms, not a classification by names or retail species numbers.</summary>
public enum MercuryFormMechanismKind
{
    Unresolved,
    PidDerived,
    GenderDependentResource,
    RuntimeDependentResource,
}

/// <summary>Inputs required by the documented ROM selector; these are not new save fields.</summary>
[Flags]
public enum MercuryFormContext
{
    None = 0,
    Pid = 1,
    RomGenderRatio = 2,
    RuntimeState = 4,
}

/// <summary>
/// A proven resource choice and its prerequisites. Resource indices must never be assigned to Species.
/// A null prerequisite means that input is not tested by this option, not an unknown value matching it.
/// </summary>
public sealed class MercuryFormOption
{
    internal MercuryFormOption(int spriteResourceIndex, int paletteResourceIndex,
        byte? pidForm = null, bool? genderHelperReturnsFemale = null, bool? runtimeState = null)
    {
        SpriteResourceIndex = spriteResourceIndex;
        PaletteResourceIndex = paletteResourceIndex;
        PidForm = pidForm;
        GenderHelperReturnsFemale = genderHelperReturnsFemale;
        RuntimeState = runtimeState;
    }

    public int SpriteResourceIndex { get; }
    public int PaletteResourceIndex { get; }

    /// <summary>Result of EntityPID.GetUnownForm3; only species 201 has proven selectable PID forms.</summary>
    public byte? PidForm { get; }

    /// <summary>True means ROM helper 0x0803F78C returns 0xFE, false means it does not.</summary>
    public bool? GenderHelperReturnsFemale { get; }

    /// <summary>Required value of RAM 0x03003529 bit 1, when tested; not a persistent form.</summary>
    public bool? RuntimeState { get; }
}

/// <summary>
/// Read-only rule metadata for one internal species in the supported Mercury 1.1 ROM.
/// Options describe proven choices, not a base/form-to-stored-species mapping or an exhaustive form list.
/// </summary>
public sealed class MercuryFormMechanism
{
    internal MercuryFormMechanism(ushort species, MercuryFormMechanismKind kind, string selectionSource,
        MercuryFormContext requiredContext, uint[] evidenceAddresses, MercuryFormOption[] options)
    {
        BaseSpecies = species;
        Kind = kind;
        SelectionSource = selectionSource;
        RequiredContext = requiredContext;
        EvidenceAddresses = Array.AsReadOnly(evidenceAddresses);
        Options = Array.AsReadOnly(options);
    }

    public ushort BaseSpecies { get; }
    public MercuryFormMechanismKind Kind { get; }
    public string SelectionSource { get; }
    public MercuryFormContext RequiredContext { get; }
    public IReadOnlyList<uint> EvidenceAddresses { get; }
    public IReadOnlyList<MercuryFormOption> Options { get; }

    /// <summary>
    /// Only the existing species-201 Form/PID editor is proven. This does not imply a separate stored form byte.
    /// Resource-only and unresolved mechanisms must not create a Species or runtime-state setter.
    /// </summary>
    public bool CanEditStoredForm => BaseSpecies == 201 && Kind == MercuryFormMechanismKind.PidDerived;
}
