using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using PKHeX.Mercury.Core;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercuryEncounterFieldMatchTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void OrdinaryEndpointsPassOnlyRecordedFields(int level)
    {
        var match = MercuryEncounterMatcher.Match(1, 7, level, Evidence());
        Assert.Equal(MercuryCheckStatus.Pass, match.FieldMatchStatus);
        Assert.Equal(MercuryCheckStatus.Unknown, match.Status);
        Assert.Single(match.Candidates);
        Assert.Contains("通过所列字段约束匹配；未核对其他生成约束", match.Evidence);
        Assert.Contains("PID/IV", match.Evidence);
        Assert.Contains("egg状态", match.Evidence);
        Assert.Contains("槽可选性", match.Evidence);
        Assert.DoesNotContain("不证明实际获取来源", match.Evidence);
        Assert.DoesNotContain("捕获时运行状态未核实", match.Evidence);
    }

    [Theory]
    [InlineData(2, 7, 3)]
    [InlineData(1, 8, 3)]
    [InlineData(1, 7, 1)]
    [InlineData(1, 7, 5)]
    [InlineData(0, 7, 3)]
    [InlineData(1, null, 3)]
    [InlineData(1, 7, null)]
    public void NoCandidateOrMissingInputStaysUnknown(int species, int? location, int? level)
    {
        var match = MercuryEncounterMatcher.Match(species, location, level, Evidence());
        AssertUnknown(match);
        Assert.Contains("不能据此判为非法", match.Evidence);
    }

    [Theory]
    [InlineData("mapsec_id")]
    [InlineData("min_level")]
    [InlineData("max_level")]
    public void MissingRecordFieldIsNotSubstituted(string field)
    {
        var json = FixtureJson();
        var map = json["tables"]!["day"]!["entries"]![0]!;
        var owner = field == "mapsec_id" ? map : map["land"]!["slots"]![0]!;
        owner.AsObject().Remove(field);
        AssertUnknown(MercuryEncounterMatcher.Match(1, 7, 3, Parse(json)));
    }

    [Theory]
    [InlineData(4, 2, 3, false)]
    [InlineData(258, 260, 3, false)]
    [InlineData(2, 4, 259, false)]
    [InlineData(-254, -252, 3, false)]
    [InlineData(258, 260, 259, true)]
    [InlineData(0, 300, 200, true)]
    public void LevelComparisonPreservesIntegersWithoutByteWrapping(int min, int max, int level, bool matches)
    {
        var json = FixtureJson();
        var slot = json["tables"]!["day"]!["entries"]![0]!["land"]!["slots"]![0]!;
        slot["min_level"] = min;
        slot["max_level"] = max;
        var evidence = Parse(json);
        Assert.Equal(min, Assert.Single(evidence.Records).MinLevel);
        Assert.Equal(max, Assert.Single(evidence.Records).MaxLevel);
        var match = MercuryEncounterMatcher.Match(1, 7, level, evidence);
        Assert.Equal(matches ? MercuryCheckStatus.Pass : MercuryCheckStatus.Unknown, match.FieldMatchStatus);
        Assert.Equal(MercuryCheckStatus.Unknown, match.Status);
    }

    [Fact]
    public void DynamicOnlyDoesNotBecomeOrdinaryFieldMatch()
    {
        var json = FixtureJson();
        json["tables"] = new JsonObject();
        json["swarm"]!["entries"] = JsonNode.Parse("[{\"species_id\":1,\"mapsec_id\":7}]");
        var match = MercuryEncounterMatcher.Match(1, 7, 3, Parse(json));
        AssertUnknown(match);
        Assert.Single(match.DynamicRecords);
    }

    [Fact]
    public void NoImportedEvidenceStaysUnknown()
        => AssertUnknown(MercuryEncounterMatcher.Match(1, 7, 3, null));

    [Fact]
    public void NoRomAndNumericDataCannotGrantFieldPass()
    {
        AssertAnalysisUnknown(MercuryLegalityAnalysis.Analyze(Mon(), null, Evidence()));
        AssertAnalysisUnknown(MercuryLegalityAnalysis.Analyze(Mon(), MercuryGameData.NumericOnly(), Evidence()));
    }

    [Fact]
    public void OtherVersionIsRejectedEvenWhenOtherGateFlagsArePresent()
    {
        var result = MercuryLegalityAnalysis.Analyze(Mon(), GateFixture(MercuryRomVersion.V1_0.Sha256), Evidence());
        AssertAnalysisUnknown(result);
        Assert.Contains("仅支持水银1.1", FieldCheck(result).Evidence);
    }

    [Fact]
    public void LoaderRejectsCrossVersionAndCrossShaDeclarations()
    {
        var json = FixtureJson();
        Assert.Throws<InvalidDataException>(() => MercuryEncounterEvidenceLoader.Parse(json.ToJsonString(), MercuryRomVersion.V1_0.Sha256));
        json["sha256"] = MercuryRomVersion.V1_0.Sha256;
        Assert.Throws<InvalidDataException>(() => Parse(json));
        json["sha256"] = new string('0', 64);
        Assert.Throws<InvalidDataException>(() => Parse(json));
    }

    [MercuryRomFact]
    public void VerifiedRomAndAcceptedEvidencePassFieldsButAggregateRemainsUnknown()
    {
        var data = LoadV11();
        var result = MercuryLegalityAnalysis.Analyze(Mon(), data, Evidence());
        Assert.Equal(MercuryCheckStatus.Pass, FieldCheck(result).Status);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Checks.Single(z => z.Code == "encounter.source").Status);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
        Assert.Contains("所列字段约束匹配", FieldCheck(result).Evidence);
        Assert.DoesNotContain(result.Checks, z => z.Status == MercuryCheckStatus.Invalid);
        AssertAnalysisUnknown(MercuryLegalityAnalysis.Analyze(Mon(), data));
    }

    [MercuryRomFact]
    public void AnalyzerAlsoRejectsCrossShaEvidence()
    {
        // White-box corrupt-provenance fixture: the public loader correctly cannot create it.
        // This tests Analyze's independent defensive gate, not ROM extraction or authenticity.
        var constructor = typeof(MercuryEncounterEvidence).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var evidence = (MercuryEncounterEvidence)constructor.Invoke([
            MercuryRomVersion.V1_0.Sha256, "synthetic-test", "{}", "{}", "{}", "{}", Evidence().Records.ToList(),
        ]);
        var result = MercuryLegalityAnalysis.Analyze(Mon(), LoadV11(), evidence);
        AssertAnalysisUnknown(result);
        Assert.Contains("SHA与遭遇来源声明不一致", FieldCheck(result).Evidence);
    }

    private static MercuryPokemon Mon()
    {
        var mon = MercuryPokemon.Create(1, 0);
        mon.MetLocation = 7;
        mon.MetLevel = 3;
        return mon;
    }

    private static MercuryGameData LoadV11()
    {
        var data = MercuryGameData.FromRom(File.ReadAllBytes(Environment.GetEnvironmentVariable(MercuryRomFactAttribute.Variable)!));
        Assert.Same(MercuryRomVersion.V1_1, data.RomVersion);
        return data;
    }

    private static MercuryGameData GateFixture(string sha)
    {
        // Deliberately synthetic and used only for rejection. Bypass loading to isolate the version
        // branch from cache checks; this is NOT verification of a 1.0 ROM or positive data evidence.
        var constructor = typeof(MercuryGameData).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (MercuryGameData)constructor.Invoke([
            sha, "rom", Array.Empty<MercurySpecies>(), new List<MercuryMove>(), new List<MercuryItem>(),
            MercuryGameData.NumericOnly().Text, Array.Empty<string>(), Array.Empty<uint[]>(), new byte[1], null,
        ]);
    }

    private static MercuryLegalityCheck FieldCheck(MercuryLegalityResult result)
        => result.Checks.Single(z => z.Code == "encounter.record-fields");

    private static void AssertAnalysisUnknown(MercuryLegalityResult result)
    {
        Assert.Equal(MercuryCheckStatus.Unknown, FieldCheck(result).Status);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Checks.Single(z => z.Code == "encounter.source").Status);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
    }

    private static void AssertUnknown(MercuryEncounterMatchResult match)
    {
        Assert.Empty(match.Candidates);
        Assert.Equal(MercuryCheckStatus.Unknown, match.FieldMatchStatus);
        Assert.Equal(MercuryCheckStatus.Unknown, match.Status);
    }

    private static MercuryEncounterEvidence Evidence() => Parse(FixtureJson());
    private static MercuryEncounterEvidence Parse(JsonNode json)
        => MercuryEncounterEvidenceLoader.Parse(json.ToJsonString(), MercuryRomVersion.V1_1.Sha256, "synthetic-test");

    // Minimal synthetic schema, not a bundled or extracted ROM encounter table.
    private static JsonNode FixtureJson() => JsonNode.Parse($$"""
        {
          "sha256": "{{MercuryRomVersion.V1_1.Sha256}}",
          "tables": { "day": { "entries": [
            { "mapsec_id": 7, "land": { "slots": [ { "species_id": 1, "min_level": 2, "max_level": 4 } ] } }
          ] } },
          "swarm": { "entries": [] },
          "broadcast": { "entries": [], "fallback_tables": {}, "evidence": {}, "gating": {} },
          "evidence": {}, "unresolved": []
        }
        """)!;
}
