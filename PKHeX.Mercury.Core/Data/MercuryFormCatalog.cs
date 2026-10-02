using System;
using System.Collections.Generic;
using System.Linq;

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

    public const string VersionKey = MercuryTransitionTableV1_1.VersionKey;
    public const string SourceTableSha256 = MercuryTransitionTableV1_1.SourceTableSha256;
    public const string RomSha256 = MercuryTransitionTableV1_1.RomSha256;

    private static readonly IReadOnlyDictionary<ushort, MercuryFormMechanism> Known = CreateKnown();

    private static Dictionary<ushort, MercuryFormMechanism> CreateKnown()
    {
        var result = new Dictionary<ushort, MercuryFormMechanism>
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

        var tables = MercuryTransitionTableV1_1.Records.ToArray().GroupBy(r => r.Source)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Slot).ToArray());
        foreach (var (species, records) in tables)
        {
            var transitions = records.Select(r => CreateTransition(r, tables)).ToArray();
            ushort? finalTarget = transitions.Where(t => t.MethodRaw == 254 && t.ParamRaw == 0)
                .Select(t => (ushort?)t.Target).LastOrDefault();
            result[species] = result.TryGetValue(species, out var resource)
                ? resource.WithTransitions(transitions, finalTarget)
                : CreateTransitions(species, transitions, finalTarget);
        }
        return result;
    }

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
        ushort? coveredReverseFinalTarget)
        => new(species, MercuryFormMechanismKind.ConditionalSpeciesTransition,
            "Shared V1_1 consumers classify the complete method-253/254 table, preserving physical record order. " +
            "Forward conversions temporarily write a target via 0x09D30740, then restore the original species " +
            "at 0x09D3091E before returning. Direct reverse consumers separately write fallback species on " +
            "the covered receive/copy path. This is not a permanent Form field or a globally unique base species.",
            MercuryFormContext.TransitionConditions,
            [0x09D30772, 0x09D3078A, 0x09D3091E, 0x0804076A, 0x09D0C864], [], transitions, coveredReverseFinalTarget);

    private static MercurySpeciesTransition CreateTransition(MercuryTransitionRecord record,
        IReadOnlyDictionary<ushort, MercuryTransitionRecord[]> tables)
    {
        ushort? reverseTarget = null;
        if (record.Parameter != 0 && tables.TryGetValue(record.Target, out var targetTable))
        {
            var reverse = targetTable.Where(r => r.Method == record.Method && r.Parameter == 0).Take(2).ToArray();
            if (reverse.Length == 1)
                reverseTarget = reverse[0].Target; // A table lookup, not an assertion that this equals record.Source.
        }

        var (condition, evidence) = DescribeTransition(record);
        if (record.Parameter != 0)
        {
            condition += " " + TemporarySpeciesWrite;
            evidence = [.. evidence, 0x09D30772, 0x09D3078A, 0x09D3091E, 0x0804076A];
        }
        return new(record.Source, record.Target, record.Method, record.Parameter, condition, reverseTarget,
            [record.Address, .. evidence], record.Auxiliary, record.Slot, record.Address);
    }

    // CFRU dynamax.c / mega.c / form_change.c consumers, matched to this ROM's numeric branches.
    // These describe prerequisites; they do not execute a conversion or claim the runtime gates are satisfied.
    private static (string Condition, uint[] Evidence) DescribeTransition(MercuryTransitionRecord r)
        => (r.Method, r.Parameter, r.Auxiliary) switch
        {
            (253, 0, _) => (
                "Fallback: 0x09D1EF68 returns the first method-253 / param-0 target before method 0. " +
                "0x09D1EFDC writes that table result at mon+0x20 before the covered receive/copy path. " +
                "The separate bank path 0x09D1EF98 can override it with nonzero backup mon+0x1C; " +
                "the table result need not equal the original forward source. Other persistence conditions remain unproven.",
                [0x09D1EF68, 0x09D1EF84, 0x09D1EFDC, 0x09D1EFEE, 0x09D1EF98, 0x09D1EFB8, 0x09D0C874, 0x09D0C8C8]),
            (253, > 0, _) => (
                "Conditional conversion: parameter is a nonzero flag, not an item or level requirement. " +
                "0x09D1EE02 returns the first method-253 / param-nonzero target before method 0. " +
                "Caller 0x09D1EE3C requires mon+0x47 bit 3; its outer item/context gate 0x09D1EC28 " +
                "has not been fully mapped to the reference configuration. 0x09D1EEB4 calls the writer.",
                [0x09D1EE02, 0x09D1EE3C, 0x09D1EE78, 0x09D1EC28, 0x09D1EEB4]),
            (254, 0, _) => (
                "Fallback: 0x09D423E4 scans the initial species table in physical slot order until method 0. " +
                "Every method-254 / param-0 record directly writes its target at 0x09D4240C; " +
                "0x09D42412 continues without re-indexing by the new species. The last write wins on this path. " +
                "No held-item or auxiliary filter and no backup selection apply. This is the covered " +
                "receive/copy reversal, not a globally unique base; other persistence conditions remain unproven.",
                [0x09D423E4, 0x09D4240C, 0x09D42412, 0x09D0C86E, 0x09D0C8C8]),
            (254, > 0, 0 or 3) => (
                $"Conditional conversion: mode {(r.Auxiliary == 0 ? 0 : 1)} / auxiliary {r.Auxiliary}; " +
                $"0x09D42562 compares held-item mon+0x22 with item index {r.Parameter}. " +
                "The first context-matching record before method 0 is returned; mode 0 is tried before mode 1. " +
                "Outer gates 0x09D61128 / 0x09D31E7C and Keystone configuration are not fully mapped. " +
                "Auxiliary 3 bypasses the additional 0x09D66590 / 0x09D66660 gate used by the ordinary path. " +
                "0x09D425D4 calls the writer.",
                [0x09D424B8, 0x09D42562, 0x09D425B8, 0x09D425D4, 0x09D61128, 0x09D31E7C, 0x09D66590, 0x09D66660]),
            (254, > 0, 1) => (
                $"Conditional conversion: method 254 / auxiliary 1 requires nonzero held-item mon+0x22 equal to item index {r.Parameter}. " +
                "0x09D42388 scans all 16 physical slots, without a method-0 terminator; the first matching record " +
                "calls the writer at 0x09D423C0 and returns its context-dependent script.",
                [0x09D42388, 0x09D42398, 0x09D423AE, 0x09D423B4, 0x09D423C0]),
            (254, > 0, 2) => (
                $"Conditional conversion: mode 0 / auxiliary 2 compares move index {r.Parameter} with four learned moves " +
                "at mon+0x2C through mon+0x32, not the held-item parameter comparison. " +
                "The first context-matching record before method 0 is returned. The held-item category gate " +
                "0x09D3D77C must return zero; battle mask 0x06000100 invokes 0x09D31DD0 when set and requires nonzero. " +
                "These category/Frontier gates and outer 0x09D61128 / 0x09D31E7C / 0x09D66590 / 0x09D66660 " +
                "configuration are not fully mapped. 0x09D425D4 calls the writer.",
                [0x09D424B8, 0x09D42522, 0x09D42538, 0x09D42544, 0x09D3D77C, 0x09D31DD0, 0x09D425D4]),
            _ => throw new InvalidOperationException("The version-bound table contains an unclassified transition branch."),
        };

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
