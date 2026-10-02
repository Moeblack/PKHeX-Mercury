using System;
using System.Buffers.Binary;
using System.Linq;
using PKHeX.Mercury.Core;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercuryMethod1Tests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(0x12345678u)]
    [InlineData(0xDEADBEEFu)]
    [InlineData(0x80000000u)]
    [InlineData(0xFFFFFFFFu)]
    public void NativeGeneratedVectorsPassOnlyNumericCorrelation(uint seed)
    {
        var reference = Generate(seed);
        Assert.True(MethodFinder.GetLCRNGMethod1Match(reference.PID, reference.IV32, out uint candidate));
        var result = MercuryLegalityAnalysis.Analyze(FromReference(reference), null);
        var check = Rng(result);
        Assert.Equal(MercuryCheckStatus.Pass, check.Status);
        Assert.Contains("符合基础Method1数值相关性，不证明该来源必用此方法，也不代表完整合法", check.Evidence);
        Assert.Contains($"seed=0x{candidate:X8}", check.Evidence);
        Assert.Matches("seed=0x[0-9A-F]{8}，", check.Evidence);
        Assert.Contains("不是已还原的真实捕获种子", check.Evidence);
        AssertSourceUnknown(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeConfirmedNonMatchStaysUnknown(bool numericData)
    {
        var reference = NonMatch();
        Assert.False(MethodFinder.GetLCRNGMethod1Match(reference.PID, reference.IV32, out _));
        var result = MercuryLegalityAnalysis.Analyze(FromReference(reference), Data(numericData));
        var check = Rng(result);
        Assert.Equal(MercuryCheckStatus.Unknown, check.Status);
        Assert.Contains("CFRU同步性格、闪光后处理或IV覆盖可能打破该关系", check.Evidence);
        Assert.Contains("不能据此判为非法", check.Evidence);
        AssertSourceUnknown(result);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void EggAndHiddenAbilityBitsNeverEnterMethod1Input(bool matching, bool egg, bool hidden)
    {
        var reference = matching ? Generate(0x12345678u) : NonMatch();
        Assert.Equal(matching, MethodFinder.GetLCRNGMethod1Match(reference.PID, reference.IV32, out _));
        var mon = FromReference(reference);
        var baseline = Rng(MercuryLegalityAnalysis.Analyze(mon, null));
        mon.IsEgg = egg;
        mon.HiddenAbility = hidden;
        // Mercury's raw boxed IV word contains two flags that must not reach the native matcher.
        uint raw = BinaryPrimitives.ReadUInt32LittleEndian(mon.ToBoxBytes().AsSpan(0x36));
        Assert.Equal((egg ? 0x40000000u : 0u) | (hidden ? 0x80000000u : 0u), raw & 0xC0000000u);
        Assert.Equal(reference.IV32, raw & 0x3FFFFFFFu);
        var result = MercuryLegalityAnalysis.Analyze(mon, null);
        Assert.Equal(baseline.Status, Rng(result).Status);
        Assert.Equal(baseline.Evidence, Rng(result).Evidence);
        AssertSourceUnknown(result);
    }

    [Fact]
    public void SixIvPositionsUseHpAtkDefSpeSpaSpdOrder()
    {
        var reference = Generate(0);
        var mon = FromReference(reference);
        Assert.Equal(new byte[]
        {
            (byte)reference.IV_HP, (byte)reference.IV_ATK, (byte)reference.IV_DEF,
            (byte)reference.IV_SPE, (byte)reference.IV_SPA, (byte)reference.IV_SPD,
        }, mon.IVs);
        Assert.Equal(6, mon.IVs.Distinct().Count()); // Ensure this vector can distinguish every position.
        Assert.Equal(reference.IV32, BinaryPrimitives.ReadUInt32LittleEndian(mon.ToBoxBytes().AsSpan(0x36)));
        Assert.Equal(MercuryCheckStatus.Pass, Rng(MercuryLegalityAnalysis.Analyze(mon, null)).Status);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AnalysisDoesNotChangeBoxPartyOrInputBytes(bool party, bool matching)
    {
        var original = FromReference(matching ? Generate(0xDEADBEEFu) : NonMatch());
        original.IsEgg = true;
        original.HiddenAbility = true;
        byte[] input = party ? original.ToPartyBytes() : original.ToBoxBytes();
        byte[] snapshot = (byte[])input.Clone();
        var mon = party ? MercuryPokemon.FromParty(input) : MercuryPokemon.FromBox(input);
        byte[] boxBefore = mon.ToBoxBytes();
        byte[] partyBefore = mon.ToPartyBytes();
        var result = MercuryLegalityAnalysis.Analyze(mon, null);
        Assert.Equal(matching ? MercuryCheckStatus.Pass : MercuryCheckStatus.Unknown, Rng(result).Status);
        Assert.Equal(snapshot, input);
        Assert.Equal(boxBefore, mon.ToBoxBytes());
        Assert.Equal(partyBefore, mon.ToPartyBytes());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptySpeciesDoesNotInventAnIndividual(bool numericData)
    {
        var mon = FromReference(Generate(0));
        mon.Species = 0; // Even a mathematically matching payload is an empty slot here.
        var result = MercuryLegalityAnalysis.Analyze(mon, Data(numericData));
        Assert.Equal(MercuryCheckStatus.Unknown, Rng(result).Status);
        Assert.Contains("空槽", Rng(result).Evidence);
        AssertSourceUnknown(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithoutRomOnlyTheNumericCheckCanPass(bool numericData)
    {
        var result = MercuryLegalityAnalysis.Analyze(FromReference(Generate(1)), Data(numericData));
        Assert.Equal("rng.method1", Assert.Single(result.Checks, z => z.Status == MercuryCheckStatus.Pass).Code);
        Assert.All(result.Checks.Where(z => z.Code != "rng.method1"), z => Assert.Equal(MercuryCheckStatus.Unknown, z.Status));
        AssertSourceUnknown(result);
    }

    private static PK3 Generate(uint seed)
    {
        uint pid = ClassicEraRNG.GetSequentialPID(ref seed);
        uint iv32 = ClassicEraRNG.GetSequentialIVs(ref seed);
        return new PK3 { PID = pid, IV32 = iv32 };
    }

    private static PK3 NonMatch() => new()
    {
        PID = 0x12345678,
        IV_HP = 1, IV_ATK = 2, IV_DEF = 3, IV_SPE = 4, IV_SPA = 5, IV_SPD = 6,
    };

    private static MercuryPokemon FromReference(PK3 reference)
    {
        var mon = MercuryPokemon.Create(131, 0);
        mon.PID = reference.PID;
        mon.IVs = [(byte)reference.IV_HP, (byte)reference.IV_ATK, (byte)reference.IV_DEF,
            (byte)reference.IV_SPE, (byte)reference.IV_SPA, (byte)reference.IV_SPD];
        return mon;
    }

    private static MercuryGameData? Data(bool numeric) => numeric ? MercuryGameData.NumericOnly() : null;
    private static MercuryLegalityCheck Rng(MercuryLegalityResult result) => result.Checks.Single(z => z.Code == "rng.method1");

    private static void AssertSourceUnknown(MercuryLegalityResult result)
    {
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Checks.Single(z => z.Code == "encounter.source").Status);
        Assert.DoesNotContain(result.Checks, z => z.Status == MercuryCheckStatus.Invalid);
    }
}
