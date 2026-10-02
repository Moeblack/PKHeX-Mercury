using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using PKHeX.Mercury.Core;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercuryBuiltInDataTests
{
    private const string Prefix = "PKHeX.Mercury.Core.BuiltIn.1.1.";

    [Fact]
    public void BuiltInLoadsWithoutRomOrUserProfileAndProvidesCompleteData()
    {
        var data = MercuryGameData.LoadBuiltIn();
        Assert.Equal("builtin", data.Source);
        Assert.Same(MercuryRomVersion.V1_1, data.RomVersion);
        Assert.True(data.IsTrustedBuiltInData);
        Assert.True(data.HasTrustedGameData);
        Assert.False(data.HasVerifiedRom);
        Assert.False(data.HasSprites);
        Assert.True(data.HasSpriteResources);
        Assert.True(data.HasGrowthTables);
        Assert.True(data.Text.IsImported);
        Assert.Equal("妙蛙种子", data.SpeciesName(1));
        Assert.Equal("拍击", data.MoveName(1));
        Assert.Equal(35, data.Moves[1].PP);
        Assert.Equal(50, data.GetLevel(1, data.GetExperience(1, 50)));
        Assert.Equal("真新镇", data.GetLocationIdentifiers()[88].Name);
        Assert.Equal(256, data.GetLocationIdentifiers().Length);
        Assert.NotNull(data.GetSpriteRgba(1, 0, 0, out int width, out int height));
        Assert.Equal(64, width);
        Assert.Equal(64, height);
        Assert.NotNull(data.GetTypeSpriteRgba(0, out _, out _));
        Assert.NotNull(data.GetItemSpriteRgba(1, out _, out _));
        Assert.Null(data.GetItemSpriteRgba(729, out _, out _));
        Assert.NotNull(data.GetBallSpriteRgba(0, out _, out _));
        Assert.Same(data, data.WithTextCodec(data.Text));
        Assert.Throws<NotSupportedException>(() => data.WithTextCodec(MercuryTextCodec.Default()));
    }

    [Fact]
    public void BuiltInEnablesRangeAndKnownLearningWithoutRom()
    {
        var data = MercuryGameData.LoadBuiltIn();
        var pk = MercuryPokemon.Create(1, 0);
        pk.Moves = [33, 0, 0, 0]; // ROM-derived level-up move, not a retail learnset.
        var checks = MercuryLegalityAnalysis.Analyze(pk, data).Checks;
        Assert.Equal(MercuryCheckStatus.Pass, checks.Single(z => z.Code == "species.range").Status);
        Assert.Equal(MercuryCheckStatus.Pass, checks.Single(z => z.Code == "move.1.range").Status);
        Assert.Equal(MercuryCheckStatus.Pass, checks.Single(z => z.Code == "move.1.known-learning").Status);
        Assert.All(checks, check => Assert.DoesNotContain("ROM缓存版本已验证", check.Evidence));
        Assert.Contains("应用内置资料", checks.Single(z => z.Code == "species.range").Evidence);
        Assert.False(data.HasVerifiedRom);
    }

    [Fact]
    public void BuiltInEncounterDefaultsMatchOnlyExistingRecordedFieldRules()
    {
        var data = MercuryGameData.LoadBuiltIn();
        var evidence = Assert.IsType<MercuryEncounterEvidence>(data.DefaultEncounterEvidence);
        Assert.Equal("内置水银1.1遭遇资料", evidence.SourcePath);
        Assert.Equal(6624, evidence.Records.Count(z => z.Source == MercuryEncounterSource.OrdinaryTable));
        Assert.Equal(16, evidence.Records.Count(z => z.Source == MercuryEncounterSource.Swarm));
        Assert.Equal(1200, evidence.Records.Count(z => z.Source == MercuryEncounterSource.Broadcast));
        var mon = MercuryPokemon.Create(42, 0);
        mon.MetLocation = 132;
        mon.MetLevel = 44;
        var checks = MercuryLegalityAnalysis.Analyze(mon, data).Checks;
        Assert.Equal(MercuryCheckStatus.Pass, checks.Single(z => z.Code == "encounter.record-fields").Status);
        Assert.Equal(MercuryCheckStatus.Unknown, checks.Single(z => z.Code == "encounter.source").Status);
        var wrongSha = (MercuryEncounterEvidence)typeof(MercuryEncounterEvidence)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single().Invoke([
                MercuryRomVersion.V1_0.Sha256, "wrong-sha-test", "{}", "{}", "{}", "{}", evidence.Records.ToList(),
            ]);
        var explicitWrong = MercuryLegalityAnalysis.Analyze(mon, data, wrongSha).Checks.Single(z => z.Code == "encounter.record-fields");
        Assert.Equal(MercuryCheckStatus.Unknown, explicitWrong.Status);
        Assert.Contains("SHA与遭遇来源声明不一致", explicitWrong.Evidence);
    }

    [Fact]
    public void SavingReloadingOrSelfDeclaringBuiltInSourceDoesNotTransferTrust()
    {
        string directory = Path.Combine(Path.GetTempPath(), "MercuryBuiltInTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = MercuryGameData.LoadBuiltIn();
            data.SaveProfile(directory);
            var profile = MercuryGameData.LoadProfile(directory);
            Assert.False(profile.HasTrustedGameData);
            Assert.Null(profile.DefaultEncounterEvidence);
            Assert.False(File.Exists(Path.Combine(directory, "rom-cache.gba")));
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "mercury-profile.json")))!;
            json["romSha256"] = MercuryRomVersion.V1_0.Sha256;
            File.WriteAllText(Path.Combine(directory, "mercury-profile.json"), json.ToJsonString());
            Assert.Same(MercuryRomVersion.V1_0, MercuryGameData.LoadProfile(directory).RomVersion);
            Assert.Same(MercuryRomVersion.V1_1, MercuryGameData.LoadBuiltIn().RomVersion);
            Assert.False(MercuryGameData.NumericOnly().HasTrustedGameData);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void ExternalPackWithSameManifestStillDoesNotGainBuiltInTrust()
    {
        string directory = Path.Combine(Path.GetTempPath(), "MercuryExternalPackTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            foreach (string name in new[] { "mercury-data-pack.json", "mercury-profile.json", "locations.json", "sprites.zip" })
            {
                using var source = Open(name);
                using var target = File.Create(Path.Combine(directory, name));
                source.CopyTo(target);
            }
            var pack = MercuryGameData.LoadPack(directory);
            Assert.True(pack.HasSpriteResources);
            Assert.False(pack.IsTrustedBuiltInData);
            Assert.False(pack.HasTrustedGameData);
            Assert.Null(pack.DefaultEncounterEvidence);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("mercury-data-pack.json")]
    [InlineData("locations.json")]
    public void CorruptEmbeddedResourceFailsExplicitlyWithoutChangingGlobalData(string corrupt)
    {
        var loader = typeof(MercuryGameData).Assembly.GetType("PKHeX.Mercury.Core.MercuryBuiltInData", true)!;
        var create = loader.GetMethod("CreateReader", BindingFlags.Static | BindingFlags.NonPublic)!;
        Stream Read(string name) => name == corrupt ? new MemoryStream("{}"u8.ToArray()) : Open(name);
        var error = Assert.Throws<TargetInvocationException>(() => create.Invoke(null, [(Func<string, Stream>)Read]));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.True(MercuryGameData.LoadBuiltIn().IsTrustedBuiltInData);
    }

    [Fact]
    public void MissingEmbeddedResourceIsNotSilentlyReplacedWithNumericData()
    {
        var loader = typeof(MercuryGameData).Assembly.GetType("PKHeX.Mercury.Core.MercuryBuiltInData", true)!;
        var create = loader.GetMethod("CreateReader", BindingFlags.Static | BindingFlags.NonPublic)!;
        Stream Read(string _) => throw new InvalidDataException("missing test resource");
        var error = Assert.Throws<TargetInvocationException>(() => create.Invoke(null, [(Func<string, Stream>)Read]));
        Assert.Contains("missing test resource", Assert.IsType<InvalidDataException>(error.InnerException).Message);
    }

    [Fact]
    public void MercuryRecognitionUsesBuiltInDescriptorWithoutChangingClassificationOrInput()
    {
        var data = MercuryGameData.LoadBuiltIn();
        var save = MercurySaveFile.CreateBlank(data);
        byte[] bytes = ((byte[])save.Backend.GetType().GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(save.Backend)!).ToArray();
        byte[] before = bytes.ToArray();
        var withoutData = MercurySaveRecognition.Analyze(bytes);
        var withData = MercurySaveRecognition.Analyze(bytes, data);
        Assert.Equal(withoutData.State, withData.State);
        Assert.True(withData.IsMercuryCandidate);
        Assert.Same(data, Assert.IsType<MercurySaveFile>(withData.MercuryCandidate).GameData);
        Assert.Same(MercuryRomVersion.V1_1, withData.MercuryCandidate!.GameData.RomVersion);
        Assert.Equal(before, bytes);
    }

    private static Stream Open(string name)
        => typeof(MercuryGameData).Assembly.GetManifestResourceStream(Prefix + name)
           ?? throw new InvalidDataException("Missing test resource: " + name);
}
