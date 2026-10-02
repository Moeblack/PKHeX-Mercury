using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PKHeX.Mercury.Core;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercuryBulkLegalityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptySaveIsZeroUnknownNotAllLegalAndDoesNotMutate(bool edited)
    {
        var save = MercurySaveFile.CreateBlank(MercuryGameData.NumericOnly());
        save.State.Edited = edited;
        var result = AnalyzeUnchanged(save);
        Assert.Empty(result.Entries);
        Assert.Equal(0, result.Count);
        Assert.Equal(0, result.PassCount);
        Assert.Equal(0, result.UnknownCount);
        Assert.Equal(0, result.InvalidCount);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
        Assert.Contains("无可检查个体", result.Summary);
        Assert.Contains("非空总数：0", result.Report());
        Assert.DoesNotContain("Pass", result.Report());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumeratesBoundaryBoxesAndActivePartyFromCurrentBuffersOnly(bool edited)
    {
        var data = MercuryGameData.NumericOnly();
        // Obtain a valid in-memory input without Export or touching any user save.
        var input = Bytes(MercurySaveFile.CreateBlank(data).Backend, "_data").ToArray();
        var beforeInput = input.ToArray();
        var save = new MercurySaveFile(input, data);
        foreach (var (box, slot) in new[] { (0, 0), (0, 29), (24, 0), (24, 29) })
            save.SetBoxSlotAtIndex(Mon(data, 1), box, slot, EntityImportSettings.None);
        save.SetBoxSlotAtIndex(Mon(data, ushort.MaxValue), 12, 10, EntityImportSettings.None);
        save.SetPartySlotAtIndex(Mon(data, 1), 0, EntityImportSettings.None);
        save.SetPartySlotAtIndex(Mon(data, 1), 1, EntityImportSettings.None);
        // An inactive party record must not be counted. Only test setup touches raw buffers.
        Mon(data, 1).ToMercuryPokemon().ToPartyBytes().CopyTo(Bytes(save, "_partyBuffer"), 5 * save.SIZE_PARTY);
        save.State.Edited = edited;
        Assert.Equal(0, save.Backend.GetBox(0, 0).Species); // Backend remains an old snapshot until export.
        Assert.Equal(0, save.Backend.PartyCount);

        var result = AnalyzeUnchanged(save);
        Assert.Equal(7, result.Count);
        Assert.Equal(7, result.UnknownCount);
        Assert.Equal(0, result.InvalidCount); // Numeric data cannot prove even the range invalid.
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
        foreach (var (box, slot) in new[] { (0, 0), (0, 29), (24, 0), (24, 29), (12, 10) })
            Assert.Contains(result.Entries, z => z.Slot.Source is SlotInfoBox b && b.Box == box && b.Slot == slot);
        Assert.Equal(new[] { 0, 1 }, result.Entries.Select(z => z.Slot.Source).OfType<SlotInfoParty>().Select(z => z.Slot));
        Assert.Contains(result.Entries, z => z.Slot.Entity.Species == ushort.MaxValue);
        Assert.All(result.Entries, z => Assert.NotEqual(0, z.Slot.Entity.Species));
        Assert.Equal(beforeInput, input);
        AssertIndividualResults(save, result);
        AssertCompleteReport(result);
    }

    [MercuryRomFact]
    public void RealRomMixedDistributionOrdinaryAndInvalidSpeciesMatchIndividualChecks()
    {
        var data = LoadV11();
        var save = MercurySaveFile.CreateBlank(data);
        save.SetBoxSlotAtIndex(Distribution(data), 0, 0, EntityImportSettings.None);
        save.SetBoxSlotAtIndex(Mon(data, ushort.MaxValue), 24, 29, EntityImportSettings.None);
        save.SetPartySlotAtIndex(Mon(data, 1), 0, EntityImportSettings.None);
        var result = AnalyzeUnchanged(save);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result.PassCount);
        Assert.Equal(1, result.UnknownCount);
        Assert.Equal(1, result.InvalidCount);
        Assert.Equal(MercuryCheckStatus.Invalid, result.Status);
        var invalid = Assert.Single(result.Entries, z => z.Result.Status == MercuryCheckStatus.Invalid);
        Assert.Equal(ushort.MaxValue, invalid.Slot.Entity.Species);
        Assert.Equal(MercuryCheckStatus.Invalid, Check(invalid, "species.range").Status);
        AssertIndividualResults(save, result);
        AssertCompleteReport(result);
    }

    [MercuryRomFact]
    public void IdenticalDistributionsAreNotCloneErrorsAndPassOnlyCoveredChecks()
    {
        var data = LoadV11();
        var save = MercurySaveFile.CreateBlank(data);
        save.SetBoxSlotAtIndex(Distribution(data), 0, 0, EntityImportSettings.None);
        save.SetBoxSlotAtIndex(Distribution(data), 24, 29, EntityImportSettings.None);
        var result = AnalyzeUnchanged(save);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.PassCount);
        Assert.Equal(0, result.UnknownCount);
        Assert.Equal(0, result.InvalidCount);
        Assert.Equal(MercuryCheckStatus.Pass, result.Status);
        Assert.Contains("不代表完整游戏合法性", result.Summary);
        Assert.All(result.Entries, z => Assert.Contains(z.Result.Checks,
            check => check.Role == MercuryCheckRole.Diagnostic && check.Status == MercuryCheckStatus.Unknown));
        AssertIndividualResults(save, result);
        AssertCompleteReport(result);
    }

    [MercuryRomFact]
    public void OrdinaryUnknownDominatesPassWithoutBecomingInvalid()
    {
        var data = LoadV11();
        var save = MercurySaveFile.CreateBlank(data);
        save.SetBoxSlotAtIndex(Distribution(data), 0, 0, EntityImportSettings.None);
        save.SetPartySlotAtIndex(Mon(data, 1), 0, EntityImportSettings.None);
        var result = AnalyzeUnchanged(save);
        Assert.Equal(1, result.PassCount);
        Assert.Equal(1, result.UnknownCount);
        Assert.Equal(0, result.InvalidCount);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
        AssertIndividualResults(save, result);
    }

    [MercuryRomFact]
    public void SharedEvidenceUsesIndividualShaGateAndDoesNotLeakAcrossCalls()
    {
        var data = LoadV11();
        var save = MercurySaveFile.CreateBlank(data);
        save.SetBoxSlotAtIndex(Mon(data, 1), 0, 0, EntityImportSettings.None);
        save.SetPartySlotAtIndex(Mon(data, 1), 0, EntityImportSettings.None);
        var evidence = Evidence();
        var accepted = AnalyzeUnchanged(save, evidence);
        Assert.All(accepted.Entries, z => Assert.Equal(MercuryCheckStatus.Pass, Check(z, "encounter.record-fields").Status));
        Assert.Equal(MercuryCheckStatus.Unknown, accepted.Status);
        AssertIndividualResults(save, accepted, evidence);

        // A corrupt-provenance fixture exercises the same independent gate as the individual tests.
        var constructor = typeof(MercuryEncounterEvidence).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var wrongSha = (MercuryEncounterEvidence)constructor.Invoke([
            MercuryRomVersion.V1_0.Sha256, "synthetic-test", "{}", "{}", "{}", "{}", evidence.Records.ToList(),
        ]);
        var rejected = AnalyzeUnchanged(save, wrongSha);
        Assert.All(rejected.Entries, z =>
        {
            Assert.Equal(MercuryCheckStatus.Unknown, Check(z, "encounter.record-fields").Status);
            Assert.Contains("SHA与遭遇来源声明不一致", Check(z, "encounter.record-fields").Evidence);
        });
        AssertIndividualResults(save, rejected, wrongSha);
        Assert.All(AnalyzeUnchanged(save).Entries,
            z => Assert.Equal(MercuryCheckStatus.Unknown, Check(z, "encounter.record-fields").Status));
        Assert.All(AnalyzeUnchanged(save, evidence).Entries,
            z => Assert.Equal(MercuryCheckStatus.Pass, Check(z, "encounter.record-fields").Status));
    }

    [Fact]
    public void OtherVersionCannotUseV11EncounterEvidence()
    {
        // Synthetic rejection-only state, not a claim of verified 1.0 ROM data.
        var constructor = typeof(MercuryGameData).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var data = (MercuryGameData)constructor.Invoke([
            MercuryRomVersion.V1_0.Sha256, "rom", Array.Empty<MercurySpecies>(), new List<MercuryMove>(), new List<MercuryItem>(),
            MercuryGameData.NumericOnly().Text, Array.Empty<string>(), Array.Empty<uint[]>(), new byte[1], null,
        ]);
        var save = MercurySaveFile.CreateBlank(data);
        save.SetBoxSlotAtIndex(Mon(data, 1), 0, 0, EntityImportSettings.None);
        var evidence = Evidence();
        var result = AnalyzeUnchanged(save, evidence);
        var entry = Assert.Single(result.Entries);
        Assert.Equal(MercuryCheckStatus.Unknown, Check(entry, "encounter.record-fields").Status);
        Assert.Contains("仅支持水银1.1", Check(entry, "encounter.record-fields").Evidence);
        AssertIndividualResults(save, result, evidence);
    }

    private static MercuryBulkLegalityResult AnalyzeUnchanged(MercurySaveFile save, MercuryEncounterEvidence? evidence = null)
    {
        var box = Bytes(save, "_boxBuffer").ToArray();
        var party = Bytes(save, "_partyBuffer").ToArray();
        var raw = Bytes(save.Backend, "_data").ToArray();
        var boxSnapshot = Bytes(save, "_boxSnapshot").ToArray();
        var partySnapshot = Bytes(save, "_partySnapshot").ToArray();
        bool edited = save.State.Edited;
        bool dirty = save.Backend.IsDirty;
        int count = save.PartyCount;
        var result = MercuryBulkLegalityAnalysis.Analyze(save, evidence);
        _ = result.Report(); // Formatting must not mutate the save either.
        Assert.Equal(box, Bytes(save, "_boxBuffer"));
        Assert.Equal(party, Bytes(save, "_partyBuffer"));
        Assert.Equal(raw, Bytes(save.Backend, "_data"));
        Assert.Equal(boxSnapshot, Bytes(save, "_boxSnapshot"));
        Assert.Equal(partySnapshot, Bytes(save, "_partySnapshot"));
        Assert.Equal(edited, save.State.Edited);
        Assert.Equal(dirty, save.Backend.IsDirty);
        Assert.Equal(count, save.PartyCount);
        return result;
    }

    private static void AssertIndividualResults(MercurySaveFile save, MercuryBulkLegalityResult result, MercuryEncounterEvidence? evidence = null)
    {
        var slots = new List<SlotCache>();
        SlotInfoLoader.AddFromSaveFile(save, slots);
        var expected = slots.Where(z => z.Entity.Species != 0).ToArray();
        Assert.Equal(expected.Length, result.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            var actual = result.Entries[i];
            Assert.Same(save, actual.Slot.SAV);
            Assert.Equal(expected[i].Source, actual.Slot.Source);
            Assert.Equal(expected[i].Identify(), actual.Slot.Identify());
            var individual = MercuryLegalityAnalysis.Analyze(Assert.IsType<MercuryPKM>(expected[i].Entity), evidence);
            Assert.Equal(individual.Status, actual.Result.Status);
            Assert.Equal(individual.Checks.ToArray(), actual.Result.Checks.ToArray());
        }
        Assert.Equal(result.Entries.Count(z => z.Result.Status == MercuryCheckStatus.Pass), result.PassCount);
        Assert.Equal(result.Entries.Count(z => z.Result.Status == MercuryCheckStatus.Unknown), result.UnknownCount);
        Assert.Equal(result.Entries.Count(z => z.Result.Status == MercuryCheckStatus.Invalid), result.InvalidCount);
    }

    private static void AssertCompleteReport(MercuryBulkLegalityResult result)
    {
        string report = result.Report();
        Assert.Contains($"非空总数：{result.Count}；通过：{result.PassCount}；未知：{result.UnknownCount}；无效：{result.InvalidCount}", result.Summary);
        Assert.Contains("不代表完整游戏合法性", report);
        Assert.Contains("Required 适用检查", report);
        Assert.Contains("Diagnostic 辅助诊断（不单独决定总结果）", report);
        foreach (var entry in result.Entries)
        {
            Assert.Contains(entry.Slot.Identify(), report);
            Assert.Contains(entry.Result.Summary, report);
            foreach (var check in entry.Result.Checks)
            {
                Assert.Contains($"[{check.Code}]", report);
                Assert.Contains($"/ {check.Status}: {check.Evidence}", report);
            }
        }
    }

    private static byte[] Bytes(object instance, string field)
        => (byte[])instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static MercuryPKM Mon(MercuryGameData data, ushort species)
    {
        var mon = MercuryPokemon.Create(species, 0);
        mon.MetLocation = 7;
        mon.MetLevel = 3;
        return new MercuryPKM(data, mon);
    }

    private static MercuryPKM Distribution(MercuryGameData data)
        => MercuryPKM.FromStored(data, Base64Url.DecodeFromChars(MercuryDistributionCatalog.Entries[0].Pmh1Payload.AsSpan(5)));

    private static MercuryGameData LoadV11()
    {
        var data = MercuryGameData.FromRom(File.ReadAllBytes(Environment.GetEnvironmentVariable(MercuryRomFactAttribute.Variable)!));
        Assert.Same(MercuryRomVersion.V1_1, data.RomVersion);
        return data;
    }

    private static MercuryLegalityCheck Check(MercuryBulkLegalityEntry entry, string code)
        => entry.Result.Checks.Single(z => z.Code == code);

    private static MercuryEncounterEvidence Evidence() => MercuryEncounterEvidenceLoader.Parse($$"""
        {
          "sha256": "{{MercuryRomVersion.V1_1.Sha256}}",
          "tables": { "day": { "entries": [
            { "mapsec_id": 7, "land": { "slots": [ { "species_id": 1, "min_level": 2, "max_level": 4 } ] } }
          ] } },
          "swarm": { "entries": [] },
          "broadcast": { "entries": [], "fallback_tables": {}, "evidence": {}, "gating": {} },
          "evidence": {}, "unresolved": []
        }
        """, MercuryRomVersion.V1_1.Sha256, "synthetic-test");
}
