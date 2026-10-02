using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using PKHeX.Mercury.Core;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercurySharedLearnsetTests
{
    [Fact]
    public void LevelUpMovesSnapshotsInputAndRejectsWrites()
    {
        var input = new List<MercuryLearnMove> { new(33, 50), new(45, 1), new(33, 0) };
        var species = new MercurySpecies { LevelUpMoves = input };
        input[0] = new(99, 99);
        input.Clear();

        Assert.Equal(new[] { new MercuryLearnMove(33, 50), new(45, 1), new(33, 0) }, species.LevelUpMoves);
        Assert.False(species.LevelUpMoves is MercuryLearnMove[]);
        var list = Assert.IsAssignableFrom<IList<MercuryLearnMove>>(species.LevelUpMoves);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = new(99, 99));
        Assert.Throws<NotSupportedException>(() => list.Add(new(99, 99)));
        Assert.True(Assert.IsAssignableFrom<IList<MercuryLearnMove>>(new MercurySpecies().LevelUpMoves).IsReadOnly);
    }

    [Fact]
    public void SpeciesSnapshotRejectsWritesAndSharesCachedInstances()
    {
        var data = MercuryGameData.NumericOnly();
        Assert.False(data.Species is MercurySpecies[]);
        var list = Assert.IsAssignableFrom<IList<MercurySpecies>>(data.Species);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[1] = new MercurySpecies { Id = 1 });
        Assert.Throws<NotSupportedException>(() => list.Clear());
        var learnset = data.GetLevelUpLearnset(1);
        Assert.NotNull(learnset);
        Assert.Same(learnset, data.GetLevelUpLearnset(1));
        Assert.Empty(learnset.GetAllMoves().ToArray());
        Assert.Null(data.GetLevelUpLearnset(-1));
        Assert.Null(data.GetLevelUpLearnset(data.Species.Count));
        Assert.All(MercuryLegalityAnalysis.Analyze(MercuryPokemon.Create(1, 0), data).Checks,
            check => Assert.Equal(MercuryCheckStatus.Unknown, check.Status));
    }

    [Fact]
    public void ProfileAdapterPreservesOrderPairsDuplicatesAndFullWidthValues()
    {
        using var profile = new TemporaryProfile(MercuryGameData.NumericOnly());
        profile.SetLearning([[50, 33], [1, 45], [0, 33], [255, 65535]], [88], [99]);
        var data = MercuryGameData.LoadProfile(profile.DirectoryPath);
        var learnset = data.GetLevelUpLearnset(1);
        Assert.NotNull(learnset);
        Assert.Same(learnset, data.GetLevelUpLearnset(1));
        Assert.Equal(new ushort[] { 33, 45, 33, 65535 }, learnset.GetAllMoves().ToArray());
        Assert.Equal(new byte[] { 50, 1, 0, 255 }, learnset.GetAllLevels().ToArray());
        Assert.True(learnset.GetIsLearn(33));
        Assert.True(learnset.GetIsLearn(65535));
        Assert.False(learnset.GetIsLearn(88));
        Assert.False(learnset.GetIsLearn(99));
        Assert.False(learnset.GetIsLearn(100));
        Assert.Equal(new[] { 88 }, data.Species[1].MachineMoves);
        Assert.Equal(new[] { 99 }, data.Species[1].TutorMoves);
        AssertPairs(data, 1);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(65536, 1)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(33, -1)]
    [InlineData(33, 256)]
    [InlineData(33, int.MaxValue)]
    public void UnrepresentableProfileEntriesRemainRawAndHaveNoAdapter(int move, int level)
    {
        using var profile = new TemporaryProfile(MercuryGameData.NumericOnly());
        profile.SetLearning([[level, move]], [], []);
        var data = MercuryGameData.LoadProfile(profile.DirectoryPath);
        Assert.Null(data.GetLevelUpLearnset(1));
        Assert.Null(data.GetLevelUpLearnset(1));
        Assert.Equal(new MercuryLearnMove(move, level), Assert.Single(data.Species[1].LevelUpMoves));
    }

    [MercuryRomFact]
    public void RomAndProfileEachShareOneAdapterAndPreserveEveryPair()
    {
        var romData = LoadTestRom();
        using var profile = new TemporaryProfile(romData);
        var profileData = MercuryGameData.LoadProfile(profile.DirectoryPath);
        Assert.True(romData.HasSprites);
        Assert.True(profileData.HasSprites);
        Assert.Equal("rom", romData.Source);
        Assert.Equal("profile", profileData.Source);
        Assert.Equal(romData.Species.Count, profileData.Species.Count);
        for (int i = 0; i < romData.Species.Count; i++)
        {
            AssertPairs(romData, i);
            AssertPairs(profileData, i);
            Assert.NotSame(romData.GetLevelUpLearnset(i), profileData.GetLevelUpLearnset(i));
            Assert.Equal(romData.Species[i].LevelUpMoves.ToArray(), profileData.Species[i].LevelUpMoves.ToArray());
        }
    }

    [MercuryRomFact]
    public void KnownLearningUsesCachedLevelUpWithoutChangingMachineTutorOrUnknown()
    {
        using var profile = new TemporaryProfile(LoadTestRom());
        profile.SetLearning([[50, 33], [1, 45], [0, 33]], [88], [99]);
        var data = MercuryGameData.LoadProfile(profile.DirectoryPath);
        var mon = MercuryPokemon.Create(1, 0);
        mon.Moves = [33, 88, 99, 100];
        var cached = data.GetLevelUpLearnset(1);
        var result = MercuryLegalityAnalysis.Analyze(mon, data);
        Assert.Same(cached, data.GetLevelUpLearnset(1));
        AssertSource(result, 1, "levelUp");
        AssertSource(result, 2, "tmhm");
        AssertSource(result, 3, "tutor");
        Assert.Equal(MercuryCheckStatus.Unknown, Learning(result, 4).Status);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
    }

    [MercuryRomFact]
    public void UnrepresentableLevelUpStillAllowsMachineAndTutorPositiveEvidence()
    {
        using var profile = new TemporaryProfile(LoadTestRom());
        profile.SetLearning([[256, 33]], [88], [99]);
        var data = MercuryGameData.LoadProfile(profile.DirectoryPath);
        var mon = MercuryPokemon.Create(1, 0);
        mon.Moves = [88, 99, 0, 0];
        var result = MercuryLegalityAnalysis.Analyze(mon, data);
        Assert.Null(data.GetLevelUpLearnset(1));
        AssertSource(result, 1, "tmhm");
        AssertSource(result, 2, "tutor");
        Assert.Contains("无法无损", Learning(result, 1).Evidence);
        Assert.DoesNotContain("（levelUp", Learning(result, 1).Evidence);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
    }

    [MercuryRomFact]
    public void UnrepresentableLevelUpWithoutOtherEvidenceRemainsUnknown()
    {
        using var profile = new TemporaryProfile(LoadTestRom());
        profile.SetLearning([[256, 33]], [], []);
        var data = MercuryGameData.LoadProfile(profile.DirectoryPath);
        var mon = MercuryPokemon.Create(1, 0);
        mon.Moves = [33, 0, 0, 0];
        var result = MercuryLegalityAnalysis.Analyze(mon, data);
        Assert.Equal(new MercuryLearnMove(33, 256), Assert.Single(data.Species[1].LevelUpMoves));
        Assert.Equal(MercuryCheckStatus.Unknown, Learning(result, 1).Status);
        Assert.Contains("无法无损", Learning(result, 1).Evidence);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
    }

    private static MercuryGameData LoadTestRom()
        => MercuryGameData.FromRom(File.ReadAllBytes(Environment.GetEnvironmentVariable(MercuryRomFactAttribute.Variable)!));

    private static void AssertPairs(MercuryGameData data, int species)
    {
        var entries = data.Species[species].LevelUpMoves;
        var adapter = data.GetLevelUpLearnset(species);
        Assert.NotNull(adapter);
        Assert.Same(adapter, data.GetLevelUpLearnset(species));
        Assert.Equal(entries.Select(z => z.Move), adapter.GetAllMoves().ToArray().Select(z => (int)z));
        Assert.Equal(entries.Select(z => z.Level), adapter.GetAllLevels().ToArray().Select(z => (int)z));
    }

    private static MercuryLegalityCheck Learning(MercuryLegalityResult result, int slot)
        => result.Checks.Single(z => z.Code == $"move.{slot}.known-learning");

    private static void AssertSource(MercuryLegalityResult result, int slot, string source)
    {
        var check = Learning(result, slot);
        Assert.Equal(MercuryCheckStatus.Pass, check.Status);
        Assert.Contains($"（{source}，", check.Evidence);
    }

    private sealed class TemporaryProfile : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "PKHeX-Mercury-Learnset-" + Guid.NewGuid().ToString("N"));

        public TemporaryProfile(MercuryGameData data)
        {
            try { data.SaveProfile(DirectoryPath); }
            catch { Dispose(); throw; }
        }

        public void SetLearning(int[][] entries, int[] machine, int[] tutor)
        {
            string path = Path.Combine(DirectoryPath, "mercury-profile.json");
            var root = JsonNode.Parse(File.ReadAllText(path))!;
            var species = root["species"]!.AsArray().Single(z => z!["id"]!.GetValue<int>() == 1)!;
            species["levelUp"] = System.Text.Json.JsonSerializer.SerializeToNode(entries);
            species["tmhm"] = System.Text.Json.JsonSerializer.SerializeToNode(machine);
            species["tutor"] = System.Text.Json.JsonSerializer.SerializeToNode(tutor);
            File.WriteAllText(path, root.ToJsonString());
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, true);
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class MercuryRomFactAttribute : FactAttribute
{
    internal const string Variable = "MERCURY_TEST_ROM";

    public MercuryRomFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to a supported user-owned Mercury ROM; no ROM is bundled.";
    }
}
