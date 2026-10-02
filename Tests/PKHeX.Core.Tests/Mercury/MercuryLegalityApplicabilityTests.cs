using System;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using PKHeX.Mercury.Core;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercuryLegalityApplicabilityTests
{
    [Fact]
    public void DiagnosticUnknownDoesNotLowerRequiredPassAndRemainsVisible()
    {
        var required = new MercuryLegalityCheck("required", MercuryCheckStatus.Pass, "Covered.");
        var diagnostic = new MercuryLegalityCheck("diagnostic", MercuryCheckStatus.Unknown, "Gap.", MercuryCheckRole.Diagnostic);
        var result = new MercuryLegalityResult([required, diagnostic]);
        Assert.Equal(MercuryCheckRole.Required, required.Role); // Three-argument callers retain their contract.
        Assert.Equal(MercuryCheckStatus.Pass, result.Status);
        Assert.Contains(diagnostic, result.Checks);
        Assert.Contains("适用检查通过", result.Summary);
        Assert.Contains("不代表完整游戏合法性", result.Summary);
    }

    [Theory]
    [InlineData(MercuryCheckRole.Required, MercuryCheckStatus.Pass)]
    [InlineData(MercuryCheckRole.Required, MercuryCheckStatus.Unknown)]
    [InlineData(MercuryCheckRole.Diagnostic, MercuryCheckStatus.Pass)]
    [InlineData(MercuryCheckRole.Diagnostic, MercuryCheckStatus.Unknown)]
    public void AnyInvalidDominatesRegardlessOfRole(MercuryCheckRole role, MercuryCheckStatus otherStatus)
    {
        var invalid = new MercuryLegalityCheck("invalid", MercuryCheckStatus.Invalid, "Invalid.", role);
        var other = new MercuryLegalityCheck("required", otherStatus, "Covered or unknown.");
        Assert.Equal(MercuryCheckStatus.Invalid, new MercuryLegalityResult([invalid, other]).Status);
        Assert.Equal(MercuryCheckStatus.Invalid, new MercuryLegalityResult([other, invalid]).Status);
        Assert.Equal(MercuryCheckStatus.Invalid, new MercuryLegalityResult([invalid]).Status);
    }

    [Fact]
    public void ZeroRequiredChecksStayUnknownUnlessInvalid()
    {
        Assert.Equal(MercuryCheckStatus.Unknown, new MercuryLegalityResult([]).Status);
        var pass = new MercuryLegalityCheck("diagnostic.pass", MercuryCheckStatus.Pass, "Diagnostic.", MercuryCheckRole.Diagnostic);
        var unknown = new MercuryLegalityCheck("diagnostic.unknown", MercuryCheckStatus.Unknown, "Gap.", MercuryCheckRole.Diagnostic);
        Assert.Equal(MercuryCheckStatus.Unknown, new MercuryLegalityResult([pass]).Status);
        Assert.Equal(MercuryCheckStatus.Unknown, new MercuryLegalityResult([unknown]).Status);
        Assert.Equal(MercuryCheckStatus.Unknown, new MercuryLegalityResult([pass, unknown]).Status);
    }

    [Fact]
    public void RequiredUnknownStillBlocksPass()
    {
        var result = new MercuryLegalityResult([
            new("required.pass", MercuryCheckStatus.Pass, "Covered."),
            new("required.unknown", MercuryCheckStatus.Unknown, "Gap."),
            new("diagnostic", MercuryCheckStatus.Pass, "Diagnostic.", MercuryCheckRole.Diagnostic),
        ]);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ExactReferenceWithoutVerifiedRomStillHasRequiredUnknownRanges(int index)
    {
        var mon = MercuryPokemon.FromBox(Decode(MercuryDistributionCatalog.Entries[index]));
        var before = mon.ToBoxBytes();
        foreach (var data in new MercuryGameData?[] { null, MercuryGameData.NumericOnly() })
        {
            var result = MercuryLegalityAnalysis.Analyze(mon, data);
            AssertRoles(result, exact: true);
            Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
            Assert.All(result.Checks.Where(z => z.Code.EndsWith(".range", StringComparison.Ordinal)),
                z => Assert.Equal(MercuryCheckStatus.Unknown, z.Status));
            Assert.Equal(MercuryCheckStatus.Pass, Check(result, "encounter.source").Status);
            Assert.Equal(before, mon.ToBoxBytes());
        }
    }

    [MercuryRomFact]
    public void AllFourExactReferencesWithVerifiedRomPassOnlyCoveredApplicableChecks()
    {
        var data = LoadV11();
        foreach (var entry in MercuryDistributionCatalog.Entries)
        {
            var bytes = Decode(entry);
            var mon = MercuryPokemon.FromBox(bytes);
            var result = MercuryLegalityAnalysis.Analyze(mon, data);
            AssertRoles(result, exact: true);
            Assert.Equal(MercuryCheckStatus.Pass, result.Status);
            Assert.All(result.Checks.Where(z => z.Role == MercuryCheckRole.Required),
                z => Assert.Equal(MercuryCheckStatus.Pass, z.Status));
            Assert.Equal(MercuryCheckStatus.Pass, Check(result, "distribution.reference").Status);
            Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "encounter.record-fields").Status);
            Assert.Contains(result.Checks, z => z.Role == MercuryCheckRole.Diagnostic && z.Status == MercuryCheckStatus.Unknown);
            Assert.Contains("不证明真实领取或身份", Check(result, "encounter.source").Evidence);
            Assert.Equal(bytes, mon.ToBoxBytes());
        }
    }

    [MercuryRomFact]
    public void ChangedReferenceBytesRestoreRequiredOrdinaryChecksAndUnknownSource()
    {
        var data = LoadV11();
        foreach (var entry in MercuryDistributionCatalog.Entries)
        foreach (int offset in new[] { 0, 4, 54 }) // PID, OT, IV: ranges remain valid.
        {
            var bytes = Decode(entry);
            bytes[offset] ^= 1;
            var mon = MercuryPokemon.FromBox(bytes);
            var before = mon.ToBoxBytes();
            Assert.Null(MercuryDistributionCatalog.Match(mon));
            var result = MercuryLegalityAnalysis.Analyze(mon, data);
            AssertRoles(result, exact: false);
            Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
            Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "encounter.source").Status);
            Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "distribution.reference").Status);
            Assert.All(result.Checks.Where(z => z.Code.EndsWith(".range", StringComparison.Ordinal)),
                z => Assert.Equal(MercuryCheckStatus.Pass, z.Status));
            Assert.Equal(before, mon.ToBoxBytes());
        }
    }

    [MercuryRomFact]
    public void OrdinaryPokemonWithoutEvidenceStillHasRequiredUnknownSource()
    {
        var mon = MercuryPokemon.Create(1, 0);
        var before = mon.ToBoxBytes();
        var result = MercuryLegalityAnalysis.Analyze(mon, LoadV11());
        AssertRoles(result, exact: false);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
        Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "encounter.source").Status);
        Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "encounter.record-fields").Status);
        Assert.Equal(before, mon.ToBoxBytes());
    }

    private static void AssertRoles(MercuryLegalityResult result, bool exact)
    {
        Assert.Equal(MercuryCheckRole.Diagnostic, Check(result, "rng.method1").Role);
        Assert.Equal(MercuryCheckRole.Diagnostic, Check(result, "distribution.reference").Role);
        Assert.Equal(MercuryCheckRole.Required, Check(result, "encounter.source").Role);
        var ordinaryRole = exact ? MercuryCheckRole.Diagnostic : MercuryCheckRole.Required;
        Assert.Equal(ordinaryRole, Check(result, "encounter.record-fields").Role);
        for (int i = 1; i <= 4; i++)
            Assert.Equal(ordinaryRole, Check(result, $"move.{i}.known-learning").Role);
        Assert.All(result.Checks.Where(z => z.Code.EndsWith(".range", StringComparison.Ordinal)),
            z => Assert.Equal(MercuryCheckRole.Required, z.Role));
    }

    private static MercuryGameData LoadV11()
    {
        // The shared attribute skips only an unconfigured input; an explicit bad path must fail.
        var data = MercuryGameData.FromRom(File.ReadAllBytes(Environment.GetEnvironmentVariable(MercuryRomFactAttribute.Variable)!));
        Assert.Same(MercuryRomVersion.V1_1, data.RomVersion);
        return data;
    }

    private static byte[] Decode(MercuryDistributionReference entry) => Base64Url.DecodeFromChars(entry.Pmh1Payload.AsSpan(5));
    private static MercuryLegalityCheck Check(MercuryLegalityResult result, string code) => result.Checks.Single(z => z.Code == code);
}
