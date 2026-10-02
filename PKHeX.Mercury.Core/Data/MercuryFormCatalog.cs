using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Proven form/resource rules for Mercury 1.1 (SHA-256 in MercuryRomLayout).
/// This metadata does not resolve context, generate PIDs, or mutate a Pokemon. Use the existing
/// MercuryGameData.GetSpriteIndex selector and species-201 Form implementation for those operations.
/// In particular, GetSpriteIndex's base-resource fallback for null runtimeState is not proof of a state.
/// </summary>
public static class MercuryFormCatalog
{
    private static readonly IReadOnlyDictionary<ushort, MercuryFormMechanism> Known =
        new Dictionary<ushort, MercuryFormMechanism>
        {
            [201] = CreateUnown(),
            [0x1F6] = CreateGender(0x1F6, 0x2E8, 0x0940E308, 0x0940E32E),
            [0x1F7] = CreateGender(0x1F7, 0x2E9, 0x0940E310, 0x0940E334),
            [0x23E] = CreateGender(0x23E, 0x2BF, 0x0940E300, 0x0940E338),
            [0x285] = CreateGender(0x285, 0x2C0, 0x0940E326, 0x0940E32A),
            [0x286] = CreateGender(0x286, 0x2C1, 0x0940E31A, 0x0940E33C),
            [0x308] = CreateGender(0x308, 0x33F, 0x0940E320, 0x0940E340),
            [0x338] = new(0x338, MercuryFormMechanismKind.RuntimeDependentResource,
                "0x0940E2F0: when gender helper != 0xFE, RAM 0x03003529 bit 1 selects " +
                "false -> resource 0x44D, true -> resource 0x338. Missing runtime context is unknown; " +
                "it is not a save field. Gender helper == 0xFE bypasses this switch.",
                MercuryFormContext.Pid | MercuryFormContext.RomGenderRatio | MercuryFormContext.RuntimeState,
                [0x0940E2F0, 0x0803F78C, 0x0940E348, 0x0940E350, 0x0940E352, 0x0940E356],
                [
                    new(0x44D, 0x44D, genderHelperReturnsFemale: false, runtimeState: false),
                    new(0x338, 0x338, genderHelperReturnsFemale: false, runtimeState: true),
                    new(0x338, 0x338, genderHelperReturnsFemale: true),
                ]),
        };

    /// <summary>
    /// Looks up an internal species without retail-number conversion or masking. Unlisted IDs,
    /// including resource targets, return Unresolved with no proven options, never "has no forms".
    /// </summary>
    public static MercuryFormMechanism Get(ushort species)
        => Known.TryGetValue(species, out var mechanism) ? mechanism : new(species,
            MercuryFormMechanismKind.Unresolved,
            "No proven form mechanism or persistent base/form-to-internal-species mapping in this catalog.",
            MercuryFormContext.None, [], []);

    private static MercuryFormMechanism CreateUnown()
    {
        var options = new MercuryFormOption[28];
        for (byte form = 0; form < options.Length; form++)
            options[form] = new(form == 0 ? 201 : 412 + form, 201, pidForm: form);
        return new(201, MercuryFormMechanismKind.PidDerived,
            "0x0940E442..0x0940E48A calls 0x08082AB8: EntityPID.GetUnownForm3(PID), " +
            "28 forms. Form 0 uses tile resource 201; forms 1..27 use 412 + form; palette remains 201.",
            MercuryFormContext.Pid, [0x0940E442, 0x0940E48A, 0x08082AB8], options);
    }

    private static MercuryFormMechanism CreateGender(ushort species, int target, uint comparison, uint selection)
        => new(species, MercuryFormMechanismKind.GenderDependentResource,
            "0x0940E2F0 uses ROM gender helper 0x0803F78C (this ROM's ratio/PID, not retail ratios): " +
            "0xFE selects the alternate resource; other results retain the base resource. " +
            "This does not prove a stored species/form mapping.",
            MercuryFormContext.Pid | MercuryFormContext.RomGenderRatio,
            [0x0940E2F0, 0x0803F78C, comparison, selection],
            [
                new(species, species, genderHelperReturnsFemale: false),
                new(target, target, genderHelperReturnsFemale: true),
            ]);
}
