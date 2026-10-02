using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PKHeX.Mercury.Core;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercuryDistributionReferenceTests
{
    private static readonly string[] Labels = ["梦想成为假面骑士的芭瓢虫", "竹兰的圆陆鲨", "壮壮妈", "Lv.5 新叶喵"];
    private static readonly ushort[][] Moves = [[33, 48, 0, 0], [349, 44, 10, 1], [33, 43, 52, 0], [10, 39, 595, 0]];

    [Theory]
    [InlineData(0, 165, 48u, 54321u, 100u, 23, 25, true)]
    [InlineData(1, 496, 128u, 1u, 134u, 3, 1, true)]
    [InlineData(2, 1411, 540u, 54321u, 134u, 15, 26, false)]
    [InlineData(3, 1408, 78u, 54321u, 134u, 3, 23, true)]
    public void PublishedPayloadsAgreeWithFixedMetadataAndStorageRanges(int index, int species, uint pid, uint ot,
        uint experience, int nature, int ball, bool hidden)
    {
        Assert.Equal(4, MercuryDistributionCatalog.Entries.Count);
        var entry = MercuryDistributionCatalog.Entries[index];
        Assert.Equal(Labels[index], entry.Label);
        Assert.Equal("274fcd4dbd5ad7c06af9705c5f76796d853a275c", entry.SourceCommit);
        Assert.Equal($"https://github.com/Sum-Light/azoth-wiki/blob/{entry.SourceCommit}/docs/distribution/index.md", entry.SourceUrl);
        byte[] bytes = Decode(entry);
        Assert.Equal(58, bytes.Length);
        var mon = MercuryPokemon.FromBox(bytes);
        Assert.Equal(bytes, mon.ToBoxBytes());
        Assert.Equal(species, mon.Species);
        Assert.Equal(pid, mon.PID);
        Assert.Equal(ot, mon.ID32);
        Assert.Equal(experience, mon.Experience);
        Assert.Equal(nature, mon.Nature);
        Assert.Equal(ball, mon.Ball);
        Assert.Equal(hidden, mon.HiddenAbility);
        Assert.False(mon.IsEgg);
        Assert.Equal(0, mon.MetLocation);
        Assert.Equal(0, mon.MetLevel); // Current level on the page is not a stored met-level constraint.
        Assert.Equal(0, mon.HeldItem);
        Assert.Equal(50, mon.Friendship);
        Assert.Equal(Moves[index], mon.Moves);
        Assert.All(mon.IVs, iv => Assert.Equal(31, iv));
        Assert.All(mon.EVs, ev => Assert.Equal(0, ev));

        // Check the existing Mercury identifier-shape contract without pretending NumericOnly is ROM evidence.
        var numeric = MercuryGameData.NumericOnly();
        Assert.InRange((int)mon.Species, 1, numeric.Species.Count - 1);
        Assert.InRange((int)mon.HeldItem, 0, numeric.Items.Count - 1);
        Assert.All(mon.Moves, move => Assert.InRange((int)move, 0, numeric.Moves.Count - 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ExactPublishedContentMatchesWithoutRomButNotCompleteLegality(int index)
    {
        var entry = MercuryDistributionCatalog.Entries[index];
        var mon = MercuryPokemon.FromBox(Decode(entry));
        Assert.Same(entry, MercuryDistributionCatalog.Match(mon));
        foreach (var data in new MercuryGameData?[] { null, MercuryGameData.NumericOnly() })
        {
            var result = MercuryLegalityAnalysis.Analyze(mon, data);
            Assert.Equal(MercuryCheckStatus.Pass, Check(result, "distribution.reference").Status);
            Assert.Equal(MercuryCheckStatus.Pass, Check(result, "encounter.source").Status);
            Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
            Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "species.range").Status);
            Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "encounter.record-fields").Status);
            Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "move.1.known-learning").Status);
            Assert.NotEqual(MercuryCheckStatus.Invalid, Check(result, "rng.method1").Status);
            Assert.DoesNotContain(result.Checks, z => z.Status == MercuryCheckStatus.Invalid);
            Assert.Contains(entry.Label, Check(result, "distribution.reference").Evidence);
            Assert.Contains(entry.SourceUrl, Check(result, "distribution.reference").Evidence);
            Assert.Contains("不证明真实领取或身份，不是密码学认证", Check(result, "distribution.reference").Evidence);
            Assert.Contains("公开配信原始内容来源匹配", Check(result, "encounter.source").Evidence);
        }
    }

    [Theory]
    [InlineData(0)] // one PID byte
    [InlineData(4)] // one OT byte
    [InlineData(54)] // one IV byte
    public void OneByteChangeStaysUnknownNotInvalid(int offset)
    {
        foreach (var entry in MercuryDistributionCatalog.Entries)
        {
            var bytes = Decode(entry);
            bytes[offset] ^= 1;
            AssertNotMatched(MercuryPokemon.FromBox(bytes));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ChangedExperienceIsNotGuessedAsSameDistribution(int index)
    {
        var mon = MercuryPokemon.FromBox(Decode(MercuryDistributionCatalog.Entries[index]));
        mon.Experience++;
        var result = AssertNotMatched(mon);
        Assert.Contains("升级、改名或进化后内容可能不同", Check(result, "distribution.reference").Evidence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ExistingPartyToBoxNormalizationPreservesPublishedRecords(int index)
    {
        var entry = MercuryDistributionCatalog.Entries[index];
        byte[] input = Decode(entry);
        var boxed = MercuryPokemon.FromBox(input);
        byte[] partyBytes = boxed.ToPartyBytes();
        byte[] partySnapshot = (byte[])partyBytes.Clone();
        var party = MercuryPokemon.FromParty(partyBytes);
        Assert.Equal(input, party.ToBoxBytes());
        Assert.Same(entry, MercuryDistributionCatalog.Match(party));
        var result = MercuryLegalityAnalysis.Analyze(party, null);
        Assert.Equal(MercuryCheckStatus.Pass, Check(result, "distribution.reference").Status);
        Assert.Equal(input, party.ToBoxBytes());
        Assert.Equal(partySnapshot, party.ToPartyBytes());
        Assert.Equal(partySnapshot, partyBytes);
        Assert.Equal(Decode(entry), input);
    }

    [Fact]
    public void ReferenceBytesAndCatalogCannotBeChangedThroughPublicApi()
    {
        var entry = MercuryDistributionCatalog.Entries[0];
        Assert.All(typeof(MercuryDistributionReference).GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
        {
            Assert.Equal(typeof(string), property.PropertyType);
            Assert.Null(property.SetMethod);
        });
        var list = Assert.IsAssignableFrom<IList<MercuryDistributionReference>>(MercuryDistributionCatalog.Entries);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = list[1]);
        byte[] externalBytes = Decode(entry);
        var mon = MercuryPokemon.FromBox(externalBytes);
        externalBytes[0] ^= 1;
        byte[] exported = mon.ToBoxBytes();
        exported[4] ^= 1;
        Assert.Same(entry, MercuryDistributionCatalog.Match(mon));
        Assert.Same(entry, MercuryDistributionCatalog.Match(MercuryPokemon.FromBox(Decode(entry))));
    }

    [Fact]
    public void InvalidCoveredFieldStillDominatesPositiveReferenceChecks()
    {
        var mon = MercuryPokemon.FromBox(Decode(MercuryDistributionCatalog.Entries[0]));
        var result = MercuryLegalityAnalysis.Analyze(mon, null);
        // Exercise the unchanged aggregator with an independently covered Invalid field.
        var withInvalid = new MercuryLegalityResult(result.Checks.Append(
            new MercuryLegalityCheck("covered-field.invalid", MercuryCheckStatus.Invalid, "Covered invalid field.")));
        Assert.Equal(MercuryCheckStatus.Invalid, withInvalid.Status);
        Assert.Equal(MercuryCheckStatus.Pass, Check(withInvalid, "distribution.reference").Status);
        Assert.Equal(MercuryCheckStatus.Pass, Check(withInvalid, "encounter.source").Status);
    }

    private static byte[] Decode(MercuryDistributionReference entry) => Base64Url.DecodeFromChars(entry.Pmh1Payload.AsSpan(5));
    private static MercuryLegalityCheck Check(MercuryLegalityResult result, string code) => result.Checks.Single(z => z.Code == code);

    private static MercuryLegalityResult AssertNotMatched(MercuryPokemon mon)
    {
        Assert.Null(MercuryDistributionCatalog.Match(mon));
        byte[] before = mon.ToBoxBytes();
        var result = MercuryLegalityAnalysis.Analyze(mon, null);
        Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "distribution.reference").Status);
        Assert.Equal(MercuryCheckStatus.Unknown, Check(result, "encounter.source").Status);
        Assert.Equal(MercuryCheckStatus.Unknown, result.Status);
        Assert.DoesNotContain(result.Checks, z => z.Status == MercuryCheckStatus.Invalid);
        Assert.Equal(before, mon.ToBoxBytes());
        return result;
    }
}
