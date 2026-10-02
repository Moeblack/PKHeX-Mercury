using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PKHeX.Mercury.Core;
using PKHeX.WinForms.Controls;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercurySpeciesTransitionTests
{
    private static readonly ushort[] Sources = [3, 1260, 869, 6, 870, 871, 1261, 404, 910, 1079, 1080, 1081];

    public static TheoryData<int, int, int, int, int, int?> Relations => new()
    {
        { 3, 1260, 253, 1, 0, 3 },
        { 1260, 3, 253, 0, 0, null },
        { 3, 869, 254, 533, 0, 3 },
        { 869, 3, 254, 0, 0, null },
        { 6, 870, 254, 533, 0, 6 },
        { 6, 871, 254, 535, 0, 6 },
        { 6, 1261, 253, 1, 0, 6 },
        { 870, 6, 254, 0, 0, null },
        { 871, 6, 254, 0, 0, null },
        { 1261, 6, 253, 0, 0, null },
        { 404, 910, 254, 277, 1, 404 },
        { 910, 404, 254, 0, 1, null },
        { 1079, 1081, 254, 532, 3, null },
        { 1080, 1081, 254, 532, 3, null },
        { 1081, 1079, 254, 0, 3, null },
        { 1081, 1080, 254, 0, 3, null },
    };

    [Theory]
    [MemberData(nameof(Relations))]
    public void AdmittedRelationPreservesExactRawValues(int source, int target, int method, int param, int aux, int? reverse)
    {
        var mechanism = MercuryFormCatalog.Get((ushort)source);
        Assert.Equal(MercuryFormMechanismKind.ConditionalSpeciesTransition, mechanism.Kind);
        Assert.Equal((ushort)source, mechanism.BaseSpecies); // queried ID, not a canonical form-group base
        Assert.False(mechanism.CanEditStoredForm);
        Assert.Empty(mechanism.Options);
        var relation = Assert.Single(mechanism.Transitions, t => t.Target == target);
        Assert.Equal((ushort)source, relation.Source);
        Assert.Equal((ushort)method, relation.MethodRaw);
        Assert.Equal((ushort)param, relation.ParamRaw);
        Assert.Equal((ushort)aux, relation.AuxRaw);
        Assert.Equal(reverse is null ? (ushort?)null : (ushort)reverse.Value, relation.ReverseTarget);
        Assert.True(relation.PersistenceUnknown);
        Assert.NotEmpty(relation.Evidence);
        if (param == 0)
            Assert.StartsWith("Fallback", relation.Condition);
        else
        {
            Assert.Contains("temporarily writes", relation.Condition);
            Assert.Contains("0x09D3091E restores the original species from sp+0x2E before returning", relation.Condition);
            Assert.Contains(0x09D3091Eu, relation.Evidence);
        }
    }

    [Fact]
    public void CatalogContainsSixteenRelationsAndPreservesRecordOrder()
    {
        Assert.Equal(16, Sources.Sum(s => MercuryFormCatalog.Get(s).Transitions.Count));
        Assert.Equal(new ushort[] { 870, 871, 1261 }, MercuryFormCatalog.Get(6).Transitions.Select(t => t.Target));
        Assert.Equal(new ushort[] { 1079, 1080 }, MercuryFormCatalog.Get(1081).Transitions.Select(t => t.Target));
        Assert.Equal(new uint[] { 0x097AABDA, 0x097AABE2 }, MercuryFormCatalog.Get(1081).Transitions.Select(t => t.Evidence[0]));
        Assert.Equal(new uint[] { 0x0978925A, 0x09789262, 0x0978926A }, MercuryFormCatalog.Get(6).Transitions.Select(t => t.Evidence[0]));
    }

    [Fact]
    public void OrderedReverseIsPathScopedNotAGlobalBase()
    {
        var mechanism = MercuryFormCatalog.Get(1081);
        Assert.Equal((ushort)1081, mechanism.BaseSpecies);
        Assert.Equal((ushort)1080, mechanism.CoveredReverseFinalTarget);
        Assert.Contains("slot 0 target 1079, then slot 1 target 1080", mechanism.SelectionSource);
        Assert.Contains("not a globally unique base species", mechanism.SelectionSource);
        Assert.All(mechanism.Transitions, t => Assert.Null(t.ReverseTarget));
        Assert.Null(Assert.Single(MercuryFormCatalog.Get(1079).Transitions).ReverseTarget);
        Assert.Null(Assert.Single(MercuryFormCatalog.Get(1080).Transitions).ReverseTarget);
        Assert.All(Sources.Where(s => s != 1081), s => Assert.Null(MercuryFormCatalog.Get(s).CoveredReverseFinalTarget));
    }

    [Fact]
    public void OriginalFourRelationsDistinguishTemporaryForwardFromDirectFallback()
    {
        Assert.Equal(2, MercuryFormCatalog.Get(3).Transitions.Count);
        Assert.All(MercuryFormCatalog.Get(3).Transitions, t =>
        {
            Assert.Contains("temporarily writes", t.Condition);
            Assert.Contains("before returning", t.Condition);
            Assert.Contains(0x09D30772u, t.Evidence);
            Assert.Contains(0x09D3091Eu, t.Evidence);
        });
        var fd = Assert.Single(MercuryFormCatalog.Get(1260).Transitions);
        var fe = Assert.Single(MercuryFormCatalog.Get(869).Transitions);
        Assert.Contains("0x09D1EFDC writes", fd.Condition);
        Assert.Contains(0x09D1EFEEu, fd.Evidence);
        Assert.Contains("writes the returned species", fe.Condition);
        Assert.Contains(0x09D4240Cu, fe.Evidence);
        Assert.DoesNotContain("temporarily writes", fd.Condition);
        Assert.DoesNotContain("temporarily writes", fe.Condition);
        Assert.Contains("restore the original species", MercuryFormCatalog.Get(3).SelectionSource);
    }

    [Fact]
    public void NewConditionsKeepAuxAndHeldItemGatesSeparateFromFallback()
    {
        Assert.Contains("mode 0 / auxiliary 0", MercuryFormCatalog.Get(6).Transitions[0].Condition);
        Assert.Contains("Parameter 1 is not an item requirement", MercuryFormCatalog.Get(6).Transitions[2].Condition);
        Assert.Contains("method 254 / auxiliary 1", Assert.Single(MercuryFormCatalog.Get(404).Transitions).Condition);
        Assert.Contains("No held-item-277 requirement", Assert.Single(MercuryFormCatalog.Get(910).Transitions).Condition);
        Assert.Contains("mode 1 / auxiliary 3", Assert.Single(MercuryFormCatalog.Get(1079).Transitions).Condition);
        Assert.Contains("intermediate write", MercuryFormCatalog.Get(1081).Transitions[0].Condition);
        Assert.Contains("last write", MercuryFormCatalog.Get(1081).Transitions[1].Condition);
    }

    [Fact]
    public void MetadataIsReadOnlyAndAddsNoFormWritingCapability()
    {
        Assert.All(typeof(MercurySpeciesTransition).GetProperties(), p => Assert.Null(p.GetSetMethod(true)));
        Assert.All(typeof(MercuryFormMechanism).GetProperties(), p => Assert.Null(p.GetSetMethod(true)));
        foreach (ushort source in Sources)
        {
            var mechanism = MercuryFormCatalog.Get(source);
            var list = Assert.IsAssignableFrom<IList<MercurySpeciesTransition>>(mechanism.Transitions);
            Assert.Throws<NotSupportedException>(() => list.Clear());
            var relation = mechanism.Transitions[0];
            var evidence = Assert.IsAssignableFrom<IList<uint>>(relation.Evidence);
            Assert.Throws<NotSupportedException>(() => evidence[0] = 0);
            var pk = CreatePokemon(source);
            var before = pk.Data.ToArray();
            pk.Form = 1;
            Assert.Equal(before, pk.Data.ToArray());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(217)]
    [InlineData(1253)]
    [InlineData(405)]
    [InlineData(909)]
    [InlineData(406)]
    [InlineData(911)]
    [InlineData(4099)]
    [InlineData(65535)]
    public void UnadmittedIdsAreNotMaskedRemappedOrPromotedByMethod(int species)
    {
        var mechanism = MercuryFormCatalog.Get((ushort)species);
        Assert.Equal((ushort)species, mechanism.BaseSpecies);
        Assert.Equal(MercuryFormMechanismKind.Unresolved, mechanism.Kind);
        Assert.Empty(mechanism.Transitions);
        Assert.Empty(mechanism.Options);
        Assert.False(mechanism.CanEditStoredForm);
    }

    [Fact]
    public void ExistingPidAndResourceCategoriesRemainUnchanged()
    {
        var unown = MercuryFormCatalog.Get(201);
        Assert.Equal(MercuryFormMechanismKind.PidDerived, unown.Kind);
        Assert.True(unown.CanEditStoredForm);
        Assert.Equal(28, unown.Options.Count);
        Assert.Empty(unown.Transitions);
        Assert.Equal(MercuryFormMechanismKind.GenderDependentResource, MercuryFormCatalog.Get(0x1F6).Kind);
        Assert.Equal(MercuryFormMechanismKind.RuntimeDependentResource, MercuryFormCatalog.Get(0x338).Kind);
    }

    [Fact]
    public void LinkedProductionTooltipFormatsAllTransitionSourcesWithoutMutatingBytes()
    {
        // Compile Link includes only the real, control-independent formatting partial.
        // This exercises formatting source, not a WinForms control or interaction scenario.
        foreach (ushort source in Sources)
        {
            var pk = CreatePokemon(source);
            var before = pk.Data.ToArray();
            string tooltip = FormatTooltip(pk);
            Assert.StartsWith(source.ToString("000") + Environment.NewLine, tooltip);
            Assert.Contains("其他持久条件未证", tooltip);
            Assert.Equal(before, pk.Data.ToArray());
            Assert.Equal(source, pk.Species);
            Assert.DoesNotContain("形态机制未查明", tooltip);
            if (MercuryFormCatalog.Get(source).Transitions.Any(t => t.ParamRaw != 0))
                Assert.Contains("临时写入目标，并在返回前恢复原species", tooltip);
        }
        Assert.Contains("道具索引533、aux0", FormatTooltip(CreatePokemon(6)));
        Assert.Contains("道具索引535、aux0", FormatTooltip(CreatePokemon(6)));
        Assert.Contains("6→1261（个体标志及运行条件）", FormatTooltip(CreatePokemon(6)));
        Assert.Contains("道具索引277、aux1", FormatTooltip(CreatePokemon(404)));
        Assert.DoesNotContain("道具索引277", FormatTooltip(CreatePokemon(910)));
        Assert.Contains("道具索引532、aux3", FormatTooltip(CreatePokemon(1079)));
        Assert.Contains("道具索引532、aux3", FormatTooltip(CreatePokemon(1080)));
    }

    [Fact]
    public void OrderedReverseTooltipDoesNotInventUniqueBaseOrTwoAlternativesToSelect()
    {
        string tooltip = FormatTooltip(CreatePokemon(1081));
        Assert.Equal("1081" + Environment.NewLine +
            "已证回退写入顺序：1081→1079；1081→1080。已覆盖的09D423E4路径末次写入1080，不代表全局唯一基础种类；其他持久条件未证", tooltip);
        Assert.DoesNotContain("回退至基础种类", tooltip);
    }

    [Fact]
    public void OtherTooltipCategoriesKeepTheirExistingMeaning()
    {
        Assert.Equal("201" + Environment.NewLine + "形态由PID决定，可通过原生形态选择器编辑", FormatTooltip(CreatePokemon(201)));
        Assert.Equal("001" + Environment.NewLine + "形态机制未查明", FormatTooltip(CreatePokemon(1)));
        Assert.Contains("不是独立持久形态字段", FormatTooltip(CreatePokemon(0x1F6)));
        Assert.Contains("当前存档无法确定该状态", FormatTooltip(CreatePokemon(0x338)));
    }

    private static MercuryPKM CreatePokemon(ushort species)
        => new(MercuryGameData.NumericOnly(), MercuryPokemon.Create(species, 0x12345678));

    private static string FormatTooltip(MercuryPKM pk)
    {
        var method = typeof(PKMEditor).GetMethod("GetMercurySpeciesTooltip", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, [pk]));
    }
}
