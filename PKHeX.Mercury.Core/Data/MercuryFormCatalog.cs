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
    private const string TemporarySpeciesWrite =
        "0x09D30740 temporarily writes the target at 0x09D3078A and prepares a runtime change; " +
        "0x09D3091E restores the original species from sp+0x2E before returning. " +
        "Later handler behavior and other persistence conditions remain unproven.";

    private static readonly IReadOnlyDictionary<ushort, MercuryFormMechanism> Known =
        new Dictionary<ushort, MercuryFormMechanism>
        {
            [3] = CreateTransitions(3,
            [
                new(3, 1260, 253, 1,
                    "Conditional conversion: 0x09D1EE02 requires a nonzero selector flag and method 253 / param != 0. " +
                    "Caller 0x09D1EE3C reads mon+0x47 bit 3 and applies additional runtime gates. " +
                    "0x09D1EEB4 calls the writer. " + TemporarySpeciesWrite,
                    3, [0x097890E2, 0x09D1EE02, 0x09D1EE7E, 0x09D1EEB4, 0x09D30772, 0x09D3078A, 0x09D3091E, 0x0804076A]),
                new(3, 869, 254, 533,
                    "Conditional conversion: the sample has auxiliary value 0; 0x09D42562 compares mon+0x22 " +
                    "(item index) with parameter 533. Additional runtime gates in 0x09D424B8 and 0x09D42590 apply. " +
                    "0x09D425D4 calls the writer. " + TemporarySpeciesWrite,
                    3, [0x097890DA, 0x09D424B8, 0x09D42562, 0x09D425CA, 0x09D425D4, 0x09D30772, 0x09D3078A, 0x09D3091E, 0x0804076A]),
            ]),
            [1260] = CreateTransitions(1260,
            [
                new(1260, 3, 253, 0,
                    "Fallback, not the forward trigger: 0x09D1EF68 matches method 253 / param 0; " +
                    "0x09D1EFDC writes the returned species at mon+0x20. The covered 0x09D0C864 " +
                    "party-receive/copy path invokes this before copying 100 bytes into a party slot. " +
                    "Other persistence conditions remain unproven.",
                    null, [0x097B055A, 0x09D1EF68, 0x09D1EFE2, 0x09D1EFEE, 0x08040B14, 0x09D0C874, 0x09D0C8C8]),
            ]),
            [869] = CreateTransitions(869,
            [
                new(869, 3, 254, 0,
                    "Fallback, not an item-533 requirement: 0x09D423E4 matches method 254 / param 0 " +
                    "and writes the returned species at mon+0x20. The covered 0x09D0C864 " +
                    "party-receive/copy path invokes this before copying 100 bytes into a party slot. " +
                    "Other persistence conditions remain unproven.",
                    null, [0x097A41DA, 0x09D423E4, 0x09D4240C, 0x08040B14, 0x09D0C86E, 0x09D0C8C8]),
            ]),
            [6] = CreateTransitions(6,
            [
                new(6, 870, 254, 533,
                    "Conditional conversion: slot 0, mode 0 / auxiliary 0; 0x09D42562 compares held-item " +
                    "mon+0x22 with 533. The first matching record is selected, subject to the existing runtime gates. " +
                    "0x09D425D4 calls the writer. " + TemporarySpeciesWrite,
                    6, [0x0978925A, 0x09D424B8, 0x09D42562, 0x09D425CA, 0x09D425D4, 0x09D3078A, 0x09D3091E]),
                new(6, 871, 254, 535,
                    "Conditional conversion: slot 1, mode 0 / auxiliary 0; held-item mon+0x22 must equal 535, " +
                    "so slot 0's parameter 533 does not match. Existing runtime gates still apply. " +
                    "0x09D425D4 calls the writer. " + TemporarySpeciesWrite,
                    6, [0x09789262, 0x09D424B8, 0x09D42562, 0x09D42574, 0x09D425D4, 0x09D3078A, 0x09D3091E]),
                new(6, 1261, 253, 1,
                    "Conditional conversion: slot 2 / auxiliary 0; 0x09D1EE02 skips the two method-254 records " +
                    "and selects the first method-253 record with param != 0. Parameter 1 is not an item requirement. " +
                    "mon+0x47 bit 3 and the outer runtime gates apply. 0x09D1EEB4 calls the writer. " + TemporarySpeciesWrite,
                    6, [0x0978926A, 0x09D1EE02, 0x09D1EE1E, 0x09D1EE78, 0x09D1EEB4, 0x09D3078A, 0x09D3091E]),
            ]),
            [870] = CreateTransitions(870,
            [
                new(870, 6, 254, 0,
                    "Fallback: 0x09D423E4 matches method 254 / param 0 without a held-item or auxiliary filter. " +
                    "0x09D4240C directly writes 6 at mon+0x20; the next zero-method record ends the scan. " +
                    "The covered receive/copy path calls this reverse consumer before copying the mon.",
                    null, [0x097A425A, 0x09D423E4, 0x09D4240C, 0x09D42412, 0x09D0C86E, 0x09D0C8C8]),
            ]),
            [871] = CreateTransitions(871,
            [
                new(871, 6, 254, 0,
                    "Fallback: 0x09D423E4 matches method 254 / param 0 without a held-item or auxiliary filter. " +
                    "0x09D4240C directly writes 6 at mon+0x20; the next zero-method record ends the scan. " +
                    "The covered receive/copy path calls this reverse consumer before copying the mon.",
                    null, [0x097A42DA, 0x09D423E4, 0x09D4240C, 0x09D42412, 0x09D0C86E, 0x09D0C8C8]),
            ]),
            [1261] = CreateTransitions(1261,
            [
                new(1261, 6, 253, 0,
                    "Fallback: 0x09D1EF68 returns the first method-253 / param-0 target, 6. " +
                    "0x09D1EFDC directly writes it at 0x09D1EFEE before the covered receive/copy path. " +
                    "The separate bank path 0x09D1EF98 can override the table target with nonzero mon+0x1C; " +
                    "that is not an unconditional base-species rule.",
                    null, [0x097B05DA, 0x09D1EF68, 0x09D1EF84, 0x09D1EFEE, 0x09D1EFB8, 0x09D0C874, 0x09D0C8C8]),
            ]),
            [404] = CreateTransitions(404,
            [
                new(404, 910, 254, 277,
                    "Conditional conversion: 0x09D42388 requires nonzero held-item mon+0x22 equal to 277, " +
                    "method 254 / auxiliary 1. The first matching record calls the writer at 0x09D423C0, " +
                    "then returns without scanning later records. " + TemporarySpeciesWrite,
                    404, [0x0979595A, 0x09D42388, 0x09D42398, 0x09D423AE, 0x09D423B4, 0x09D423C0, 0x09D3078A, 0x09D3091E], auxRaw: 1),
            ]),
            [910] = CreateTransitions(910,
            [
                new(910, 404, 254, 0,
                    "Fallback: auxiliary 1 is recorded but not tested by 0x09D423E4. The method-254 / param-0 " +
                    "record directly writes 404 at 0x09D4240C; the next zero-method record ends the scan. " +
                    "No held-item-277 requirement applies to this covered receive/copy reversal.",
                    null, [0x097A565A, 0x09D423E4, 0x09D4240C, 0x09D42412, 0x09D0C86E, 0x09D0C8C8], auxRaw: 1),
            ]),
            [1079] = CreateTransitions(1079,
            [
                new(1079, 1081, 254, 532,
                    "Conditional conversion: mode 1 / auxiliary 3, held-item mon+0x22 == 532 and outer runtime " +
                    "gates. 0x09D42590 tries mode 0 before mode 1; auxiliary 3 bypasses the auxiliary-0 branch " +
                    "at 0x09D425B8. 0x09D425D4 calls the writer. " + TemporarySpeciesWrite +
                    " Species 1081 has two reverse records; no globally unique fallback is assigned.",
                    null, [0x097AAADA, 0x09D424B8, 0x09D4255E, 0x09D42562, 0x09D425B8, 0x09D425D4, 0x09D3078A, 0x09D3091E], auxRaw: 3),
            ]),
            [1080] = CreateTransitions(1080,
            [
                new(1080, 1081, 254, 532,
                    "Conditional conversion: mode 1 / auxiliary 3, held-item mon+0x22 == 532 and outer runtime " +
                    "gates. 0x09D42590 tries mode 0 before mode 1; auxiliary 3 bypasses the auxiliary-0 branch " +
                    "at 0x09D425B8. 0x09D425D4 calls the writer. " + TemporarySpeciesWrite +
                    " Species 1081 has two reverse records; no globally unique fallback is assigned.",
                    null, [0x097AAB5A, 0x09D424B8, 0x09D4255E, 0x09D42562, 0x09D425B8, 0x09D425D4, 0x09D3078A, 0x09D3091E], auxRaw: 3),
            ]),
            [1081] = CreateTransitions(1081,
            [
                new(1081, 1079, 254, 0,
                    "Fallback slot 0 / auxiliary 3: 0x09D423E4 directly writes 1079 at 0x09D4240C, then " +
                    "continues the original 1081 table at 0x09D42412. This is an intermediate write, not a " +
                    "first-match return or a backup-dependent selection.",
                    null, [0x097AABDA, 0x09D423E4, 0x09D4240C, 0x09D42412, 0x09D0C86E], auxRaw: 3),
                new(1081, 1080, 254, 0,
                    "Fallback slot 1 / auxiliary 3: after writing slot 0 target 1079, 0x09D423E4 directly " +
                    "writes 1080 at 0x09D4240C. The following zero method ends this scan. The last write on " +
                    "this covered path is 1080; other contexts remain unproven, so no global base is inferred.",
                    null, [0x097AABE2, 0x09D423E4, 0x09D4240C, 0x09D42412, 0x09D423FA, 0x09D0C86E], auxRaw: 3),
            ], coveredReverseFinalTarget: 1080),
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

    private static MercuryFormMechanism CreateTransitions(ushort species, MercurySpeciesTransition[] transitions,
        ushort? coveredReverseFinalTarget = null)
        => new(species, MercuryFormMechanismKind.ConditionalSpeciesTransition,
            "Forward conversions temporarily write a target via 0x09D30740, then restore the original species " +
            "at 0x09D3091E before returning. Direct reverse consumers separately write fallback species on " +
            "the covered receive/copy path. This is not a permanent Form field; only individually traced relations are admitted." +
            (coveredReverseFinalTarget is null ? string.Empty :
                " For species 1081, 0x09D423E4 writes slot 0 target 1079, then slot 1 target 1080; " +
                "the last write is 1080 on this path, not a globally unique base species."),
            MercuryFormContext.TransitionConditions,
            [0x09D30772, 0x09D3078A, 0x09D3091E, 0x0804076A, 0x09D0C864], [], transitions, coveredReverseFinalTarget);

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
