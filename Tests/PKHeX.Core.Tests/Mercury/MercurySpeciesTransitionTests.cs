using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PKHeX.Mercury.Core;
using PKHeX.WinForms.Controls;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercurySpeciesTransitionTests
{
    // Fixed snapshot of validated all-transition-records.json; no runtime ROM or artifact dependency.
    private static readonly (ushort Source, ushort Target, ushort Method, ushort Param, ushort Aux, byte Slot, uint Address)[] Expected =
    [
        (3, 869, 254, 533, 0, 0, 0x097890DA),
        (3, 1260, 253, 1, 0, 1, 0x097890E2),
        (6, 870, 254, 533, 0, 0, 0x0978925A),
        (6, 871, 254, 535, 0, 1, 0x09789262),
        (6, 1261, 253, 1, 0, 2, 0x0978926A),
        (9, 872, 254, 533, 0, 0, 0x097893DA),
        (9, 1262, 253, 1, 0, 1, 0x097893E2),
        (12, 1263, 253, 1, 0, 0, 0x0978955A),
        (15, 873, 254, 533, 0, 0, 0x097896DA),
        (18, 874, 254, 533, 0, 0, 0x0978985A),
        (25, 1264, 253, 1, 0, 2, 0x09789BEA),
        (26, 270, 254, 533, 0, 0, 0x09789C5A),
        (36, 271, 254, 533, 0, 0, 0x0978A15A),
        (52, 1265, 253, 1, 0, 1, 0x0978A962),
        (65, 875, 254, 533, 0, 0, 0x0978AFDA),
        (68, 1266, 253, 1, 0, 0, 0x0978B15A),
        (71, 266, 254, 533, 0, 0, 0x0978B2DA),
        (80, 876, 254, 533, 0, 0, 0x0978B75A),
        (94, 877, 254, 533, 0, 0, 0x0978BE5A),
        (94, 1267, 253, 1, 0, 1, 0x0978BE62),
        (99, 1268, 253, 1, 0, 0, 0x0978C0DA),
        (115, 878, 254, 533, 0, 0, 0x0978C8DA),
        (121, 276, 254, 533, 0, 0, 0x0978CBDA),
        (127, 879, 254, 533, 0, 0, 0x0978CEDA),
        (130, 880, 254, 533, 0, 0, 0x0978D05A),
        (131, 1269, 253, 1, 0, 0, 0x0978D0DA),
        (133, 1270, 253, 1, 0, 8, 0x0978D21A),
        (142, 881, 254, 533, 0, 0, 0x0978D65A),
        (143, 1271, 253, 1, 0, 0, 0x0978D6DA),
        (149, 1403, 254, 533, 0, 0, 0x0978D9DA),
        (150, 882, 254, 546, 0, 0, 0x0978DA5A),
        (150, 883, 254, 547, 0, 1, 0x0978DA62),
        (154, 263, 254, 533, 0, 0, 0x0978DC5A),
        (157, 264, 254, 533, 0, 0, 0x0978DDDA),
        (160, 265, 254, 533, 0, 0, 0x0978DF5A),
        (181, 884, 254, 533, 0, 0, 0x0978E9DA),
        (189, 1405, 254, 533, 0, 0, 0x0978EDDA),
        (208, 885, 254, 533, 0, 0, 0x0978F75A),
        (212, 886, 254, 533, 0, 0, 0x0978F95A),
        (214, 887, 254, 533, 0, 0, 0x0978FA5A),
        (225, 1407, 254, 533, 0, 0, 0x0978FFDA),
        (227, 272, 254, 533, 0, 0, 0x097900DA),
        (229, 888, 254, 533, 0, 0, 0x097901DA),
        (248, 889, 254, 533, 0, 0, 0x09790B5A),
        (262, 662, 254, 0, 0, 0, 0x0979125A),
        (263, 154, 254, 0, 0, 0, 0x097912DA),
        (264, 157, 254, 0, 0, 0, 0x0979135A),
        (265, 160, 254, 0, 0, 0, 0x097913DA),
        (266, 71, 254, 0, 0, 0, 0x0979145A),
        (267, 613, 254, 0, 0, 0, 0x097914DA),
        (268, 583, 254, 0, 0, 0, 0x0979155A),
        (269, 657, 254, 0, 0, 0, 0x097915DA),
        (270, 26, 254, 0, 0, 0, 0x0979165A),
        (271, 36, 254, 0, 0, 0, 0x097916DA),
        (272, 227, 254, 0, 0, 0, 0x0979175A),
        (273, 451, 254, 0, 0, 0, 0x097917DA),
        (274, 1509, 254, 0, 0, 0, 0x0979185A),
        (276, 121, 254, 0, 0, 0, 0x0979195A),
        (279, 890, 254, 533, 0, 0, 0x09791ADA),
        (282, 891, 254, 533, 0, 0, 0x09791C5A),
        (285, 892, 254, 533, 0, 0, 0x09791DDA),
        (322, 894, 254, 533, 0, 0, 0x0979305A),
        (331, 899, 254, 533, 0, 0, 0x097934DA),
        (338, 898, 254, 533, 0, 0, 0x0979385A),
        (340, 900, 254, 533, 0, 0, 0x0979395A),
        (347, 904, 254, 533, 0, 0, 0x09793CDA),
        (355, 895, 254, 533, 0, 0, 0x097940DA),
        (357, 897, 254, 533, 0, 0, 0x097941DA),
        (359, 901, 254, 533, 0, 0, 0x097942DA),
        (376, 903, 254, 533, 0, 0, 0x09794B5A),
        (378, 902, 254, 533, 0, 0, 0x09794C5A),
        (384, 896, 254, 533, 0, 0, 0x09794F5A),
        (394, 893, 254, 533, 0, 0, 0x0979545A),
        (397, 905, 254, 533, 0, 0, 0x097955DA),
        (400, 906, 254, 533, 0, 0, 0x0979575A),
        (404, 910, 254, 277, 1, 0, 0x0979595A),
        (405, 909, 254, 276, 1, 0, 0x097959DA),
        (406, 911, 254, 559, 2, 0, 0x09795A5A),
        (407, 907, 254, 533, 0, 0, 0x09795ADA),
        (408, 908, 254, 533, 0, 0, 0x09795B5A),
        (411, 1402, 254, 533, 0, 0, 0x09795CDA),
        (451, 273, 254, 533, 0, 0, 0x097970DA),
        (481, 912, 254, 533, 0, 0, 0x09797FDA),
        (498, 913, 254, 533, 0, 0, 0x0979885A),
        (501, 914, 254, 533, 0, 0, 0x097989DA),
        (513, 915, 254, 533, 0, 0, 0x09798FDA),
        (528, 916, 254, 533, 0, 0, 0x0979975A),
        (531, 1491, 254, 533, 0, 0, 0x097998DA),
        (583, 268, 254, 533, 0, 0, 0x0979B2DA),
        (584, 917, 254, 533, 0, 0, 0x0979B35A),
        (613, 267, 254, 533, 0, 0, 0x0979C1DA),
        (622, 1272, 253, 1, 0, 0, 0x0979C65A),
        (657, 269, 254, 533, 0, 0, 0x0979D7DA),
        (662, 262, 254, 533, 0, 0, 0x0979DA5A),
        (827, 918, 254, 533, 0, 0, 0x097A2CDA),
        (869, 3, 254, 0, 0, 0, 0x097A41DA),
        (870, 6, 254, 0, 0, 0, 0x097A425A),
        (871, 6, 254, 0, 0, 0, 0x097A42DA),
        (872, 9, 254, 0, 0, 0, 0x097A435A),
        (873, 15, 254, 0, 0, 0, 0x097A43DA),
        (874, 18, 254, 0, 0, 0, 0x097A445A),
        (875, 65, 254, 0, 0, 0, 0x097A44DA),
        (876, 80, 254, 0, 0, 0, 0x097A455A),
        (877, 94, 254, 0, 0, 0, 0x097A45DA),
        (878, 115, 254, 0, 0, 0, 0x097A465A),
        (879, 127, 254, 0, 0, 0, 0x097A46DA),
        (880, 130, 254, 0, 0, 0, 0x097A475A),
        (881, 142, 254, 0, 0, 0, 0x097A47DA),
        (882, 150, 254, 0, 0, 0, 0x097A485A),
        (883, 150, 254, 0, 0, 0, 0x097A48DA),
        (884, 181, 254, 0, 0, 0, 0x097A495A),
        (885, 208, 254, 0, 0, 0, 0x097A49DA),
        (886, 212, 254, 0, 0, 0, 0x097A4A5A),
        (887, 214, 254, 0, 0, 0, 0x097A4ADA),
        (888, 229, 254, 0, 0, 0, 0x097A4B5A),
        (889, 248, 254, 0, 0, 0, 0x097A4BDA),
        (890, 279, 254, 0, 0, 0, 0x097A4C5A),
        (891, 282, 254, 0, 0, 0, 0x097A4CDA),
        (892, 285, 254, 0, 0, 0, 0x097A4D5A),
        (893, 394, 254, 0, 0, 0, 0x097A4DDA),
        (894, 322, 254, 0, 0, 0, 0x097A4E5A),
        (895, 355, 254, 0, 0, 0, 0x097A4EDA),
        (896, 384, 254, 0, 0, 0, 0x097A4F5A),
        (897, 357, 254, 0, 0, 0, 0x097A4FDA),
        (898, 338, 254, 0, 0, 0, 0x097A505A),
        (899, 331, 254, 0, 0, 0, 0x097A50DA),
        (900, 340, 254, 0, 0, 0, 0x097A515A),
        (901, 359, 254, 0, 0, 0, 0x097A51DA),
        (902, 378, 254, 0, 0, 0, 0x097A525A),
        (903, 376, 254, 0, 0, 0, 0x097A52DA),
        (904, 347, 254, 0, 0, 0, 0x097A535A),
        (905, 397, 254, 0, 0, 0, 0x097A53DA),
        (906, 400, 254, 0, 0, 0, 0x097A545A),
        (907, 407, 254, 0, 0, 0, 0x097A54DA),
        (908, 408, 254, 0, 0, 0, 0x097A555A),
        (909, 405, 254, 0, 1, 0, 0x097A55DA),
        (910, 404, 254, 0, 1, 0, 0x097A565A),
        (911, 406, 254, 0, 2, 0, 0x097A56DA),
        (912, 481, 254, 0, 0, 0, 0x097A575A),
        (913, 498, 254, 0, 0, 0, 0x097A57DA),
        (914, 501, 254, 0, 0, 0, 0x097A585A),
        (915, 513, 254, 0, 0, 0, 0x097A58DA),
        (916, 528, 254, 0, 0, 0, 0x097A595A),
        (917, 584, 254, 0, 0, 0, 0x097A59DA),
        (918, 827, 254, 0, 0, 0, 0x097A5A5A),
        (1079, 1081, 254, 532, 3, 0, 0x097AAADA),
        (1080, 1081, 254, 532, 3, 0, 0x097AAB5A),
        (1081, 1079, 254, 0, 3, 0, 0x097AABDA),
        (1081, 1080, 254, 0, 3, 1, 0x097AABE2),
        (1084, 1273, 253, 1, 0, 0, 0x097AAD5A),
        (1085, 1264, 253, 1, 0, 0, 0x097AADDA),
        (1086, 1264, 253, 1, 0, 0, 0x097AAE5A),
        (1087, 1264, 253, 1, 0, 0, 0x097AAEDA),
        (1088, 1264, 253, 1, 0, 0, 0x097AAF5A),
        (1089, 1264, 253, 1, 0, 0, 0x097AAFDA),
        (1090, 1264, 253, 1, 0, 0, 0x097AB05A),
        (1091, 1264, 253, 1, 0, 0, 0x097AB0DA),
        (1092, 1264, 253, 1, 0, 0, 0x097AB15A),
        (1093, 1264, 253, 1, 0, 0, 0x097AB1DA),
        (1094, 1264, 253, 1, 0, 0, 0x097AB25A),
        (1095, 1264, 253, 1, 0, 0, 0x097AB2DA),
        (1096, 1264, 253, 1, 0, 0, 0x097AB35A),
        (1097, 1264, 253, 1, 0, 0, 0x097AB3DA),
        (1098, 1264, 253, 1, 0, 0, 0x097AB45A),
        (1099, 1264, 253, 1, 0, 0, 0x097AB4DA),
        (1104, 1274, 253, 1, 0, 0, 0x097AB75A),
        (1107, 1275, 253, 1, 0, 0, 0x097AB8DA),
        (1110, 1276, 253, 1, 0, 0, 0x097ABA5A),
        (1115, 1277, 253, 1, 0, 0, 0x097ABCDA),
        (1118, 1278, 253, 1, 0, 0, 0x097ABE5A),
        (1126, 1279, 253, 1, 0, 0, 0x097AC25A),
        (1131, 1280, 253, 1, 0, 0, 0x097AC4DA),
        (1133, 1281, 253, 1, 0, 0, 0x097AC5DA),
        (1134, 1282, 253, 1, 0, 0, 0x097AC65A),
        (1136, 1283, 253, 1, 0, 0, 0x097AC75A),
        (1141, 1284, 253, 1, 0, 0, 0x097AC9DA),
        (1143, 1286, 253, 1, 0, 0, 0x097ACADA),
        (1150, 1287, 253, 1, 0, 0, 0x097ACE5A),
        (1153, 1288, 253, 1, 0, 0, 0x097ACFDA),
        (1161, 1289, 253, 1, 0, 0, 0x097AD3DA),
        (1162, 1401, 254, 533, 0, 0, 0x097AD45A),
        (1171, 1290, 253, 1, 0, 0, 0x097AD8DA),
        (1176, 1291, 253, 1, 0, 0, 0x097ADB5A),
        (1184, 1292, 253, 1, 0, 0, 0x097ADF5A),
        (1193, 1285, 253, 1, 0, 0, 0x097AE3DA),
        (1196, 1289, 253, 1, 0, 0, 0x097AE55A),
        (1197, 1289, 253, 1, 0, 0, 0x097AE5DA),
        (1198, 1289, 253, 1, 0, 0, 0x097AE65A),
        (1199, 1289, 253, 1, 0, 0, 0x097AE6DA),
        (1200, 1289, 253, 1, 0, 0, 0x097AE75A),
        (1201, 1289, 253, 1, 0, 0, 0x097AE7DA),
        (1208, 1293, 253, 1, 0, 0, 0x097AEB5A),
        (1260, 3, 253, 0, 0, 0, 0x097B055A),
        (1261, 6, 253, 0, 0, 0, 0x097B05DA),
        (1262, 9, 253, 0, 0, 0, 0x097B065A),
        (1263, 12, 253, 0, 0, 0, 0x097B06DA),
        (1264, 25, 253, 0, 0, 0, 0x097B075A),
        (1265, 52, 253, 0, 0, 0, 0x097B07DA),
        (1266, 68, 253, 0, 0, 0, 0x097B085A),
        (1267, 94, 253, 0, 0, 0, 0x097B08DA),
        (1268, 99, 253, 0, 0, 0, 0x097B095A),
        (1269, 131, 253, 0, 0, 0, 0x097B09DA),
        (1270, 133, 253, 0, 0, 0, 0x097B0A5A),
        (1271, 143, 253, 0, 0, 0, 0x097B0ADA),
        (1272, 622, 253, 0, 0, 0, 0x097B0B5A),
        (1273, 1084, 253, 0, 0, 0, 0x097B0BDA),
        (1274, 1104, 253, 0, 0, 0, 0x097B0C5A),
        (1275, 1107, 253, 0, 0, 0, 0x097B0CDA),
        (1276, 1110, 253, 0, 0, 0, 0x097B0D5A),
        (1277, 1115, 253, 0, 0, 0, 0x097B0DDA),
        (1278, 1118, 253, 0, 0, 0, 0x097B0E5A),
        (1279, 1126, 253, 0, 0, 0, 0x097B0EDA),
        (1280, 1131, 253, 0, 0, 0, 0x097B0F5A),
        (1281, 1133, 253, 0, 0, 0, 0x097B0FDA),
        (1282, 1134, 253, 0, 0, 0, 0x097B105A),
        (1283, 1136, 253, 0, 0, 0, 0x097B10DA),
        (1284, 1141, 253, 0, 0, 0, 0x097B115A),
        (1285, 1193, 253, 0, 0, 0, 0x097B11DA),
        (1286, 1143, 253, 0, 0, 0, 0x097B125A),
        (1287, 1150, 253, 0, 0, 0, 0x097B12DA),
        (1288, 1153, 253, 0, 0, 0, 0x097B135A),
        (1289, 1161, 253, 0, 0, 0, 0x097B13DA),
        (1290, 1171, 253, 0, 0, 0, 0x097B145A),
        (1291, 1176, 253, 0, 0, 0, 0x097B14DA),
        (1292, 1184, 253, 0, 0, 0, 0x097B155A),
        (1293, 1208, 253, 0, 0, 0, 0x097B15DA),
        (1401, 1162, 254, 0, 0, 0, 0x097B4BDA),
        (1402, 411, 254, 0, 0, 0, 0x097B4C5A),
        (1403, 149, 254, 0, 0, 0, 0x097B4CDA),
        (1405, 189, 254, 0, 0, 0, 0x097B4DDA),
        (1407, 225, 254, 0, 0, 0, 0x097B4EDA),
        (1491, 531, 254, 0, 0, 0, 0x097B78DA),
        (1509, 274, 254, 533, 0, 0, 0x097B81DA),
    ];

    private static readonly ushort[] Sources = Expected.Select(r => r.Source).Distinct().ToArray();

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
    public void CompleteCatalogMatchesIndependentPhysicalSnapshot()
    {
        Assert.Equal("V1_1", MercuryFormCatalog.VersionKey);
        Assert.Equal("1db2aef9a247eade27165180e76f394f9681843af5ed0b40acc9bc85b8e71c0d", MercuryFormCatalog.SourceTableSha256);
        Assert.Equal(233, Expected.Length);
        Assert.Equal(226, Sources.Length);
        var actual = Enumerable.Range(0, ushort.MaxValue + 1)
            .SelectMany(s => MercuryFormCatalog.Get((ushort)s).Transitions).ToArray();
        Assert.Equal(233, actual.Length);
        Assert.Equal(226, actual.Select(t => t.Source).Distinct().Count());
        Assert.Equal(Expected, actual.Select(t => (t.Source, t.Target, t.MethodRaw, t.ParamRaw, t.AuxRaw, t.PhysicalSlot, t.RecordAddress)).ToArray());
        Assert.Equal(233, actual.Select(t => t.RecordAddress).Distinct().Count());
        foreach (var t in actual)
        {
            Assert.InRange(t.PhysicalSlot, (byte)0, (byte)15);
            Assert.Contains(t.RecordAddress, t.Evidence);
            Assert.True(t.PersistenceUnknown);
            if (t.ParamRaw == 0)
            {
                Assert.StartsWith("Fallback", t.Condition);
                Assert.Null(t.ReverseTarget);
                Assert.DoesNotContain("temporarily writes", t.Condition);
            }
            else
            {
                Assert.Contains("temporarily writes", t.Condition);
                Assert.Contains("0x09D3091E restores the original species from sp+0x2E before returning", t.Condition);
                Assert.Contains(0x09D3091Eu, t.Evidence);
                var reverse = Expected.Where(r => r.Source == t.Target && r.Method == t.MethodRaw && r.Param == 0).ToArray();
                Assert.Equal(reverse.Length == 1 ? (ushort?)reverse[0].Target : null, t.ReverseTarget);
            }
        }
    }

    [Theory]
    [InlineData(253, 0, 55, 34)]
    [InlineData(254, 0, 67, 67)]
    [InlineData(254, 1, 2, 2)]
    [InlineData(254, 2, 1, 1)]
    [InlineData(254, 3, 2, 2)]
    public void FixedMethodAndAuxiliaryCensus(int method, int aux, int forward, int reverse)
    {
        var records = Sources.SelectMany(s => MercuryFormCatalog.Get(s).Transitions).ToArray();
        Assert.Equal(89, records.Count(t => t.MethodRaw == 253));
        Assert.Equal(144, records.Count(t => t.MethodRaw == 254));
        Assert.Equal(72, records.Count(t => t.MethodRaw == 254 && t.ParamRaw != 0));
        Assert.Equal(72, records.Count(t => t.MethodRaw == 254 && t.ParamRaw == 0));
        Assert.Equal(forward, records.Count(t => t.MethodRaw == method && t.AuxRaw == aux && t.ParamRaw != 0));
        Assert.Equal(reverse, records.Count(t => t.MethodRaw == method && t.AuxRaw == aux && t.ParamRaw == 0));
    }

    [Fact]
    public void OrderedReverseIsPathScopedAndAppliesToEvery254FallbackSource()
    {
        Assert.Equal(new ushort[] { 869, 1260 }, MercuryFormCatalog.Get(3).Transitions.Select(t => t.Target));
        Assert.Equal(new ushort[] { 870, 871, 1261 }, MercuryFormCatalog.Get(6).Transitions.Select(t => t.Target));
        var mechanism = MercuryFormCatalog.Get(1081);
        Assert.Equal((ushort)1081, mechanism.BaseSpecies);
        Assert.Equal(new ushort[] { 1079, 1080 }, mechanism.Transitions.Select(t => t.Target));
        Assert.Equal(new byte[] { 0, 1 }, mechanism.Transitions.Select(t => t.PhysicalSlot));
        Assert.Equal(new uint[] { 0x097AABDA, 0x097AABE2 }, mechanism.Transitions.Select(t => t.RecordAddress));
        Assert.Equal((ushort)1080, mechanism.CoveredReverseFinalTarget);
        Assert.All(mechanism.Transitions, t => Assert.Null(t.ReverseTarget));
        Assert.Null(Assert.Single(MercuryFormCatalog.Get(1079).Transitions).ReverseTarget);
        Assert.Null(Assert.Single(MercuryFormCatalog.Get(1080).Transitions).ReverseTarget);
        Assert.Equal((ushort)3, MercuryFormCatalog.Get(869).CoveredReverseFinalTarget);
        Assert.Null(MercuryFormCatalog.Get(1260).CoveredReverseFinalTarget);
        foreach (ushort source in Sources)
        {
            var reverse = Expected.Where(t => t.Source == source && t.Method == 254 && t.Param == 0).ToArray();
            Assert.Equal(reverse.Length == 0 ? null : (ushort?)reverse[^1].Target,
                MercuryFormCatalog.Get(source).CoveredReverseFinalTarget);
        }
    }

    [Fact]
    public void TwentyOne253ReverseMismatchesAreNotForcedBackToSource()
    {
        var sources = Enumerable.Range(1085, 15).Concat(Enumerable.Range(1196, 6)).ToArray();
        Assert.Equal(21, sources.Length);
        foreach (int source in sources)
        {
            var relation = Assert.Single(MercuryFormCatalog.Get((ushort)source).Transitions, t => t.MethodRaw == 253 && t.ParamRaw != 0);
            Assert.Equal((ushort)(source <= 1099 ? 1264 : 1289), relation.Target);
            Assert.Equal((ushort)(source <= 1099 ? 25 : 1161), relation.ReverseTarget);
            Assert.NotEqual((ushort?)source, relation.ReverseTarget);
        }
        var mismatches = Sources.SelectMany(s => MercuryFormCatalog.Get(s).Transitions)
            .Where(t => t.ParamRaw != 0 && t.ReverseTarget is not null && t.ReverseTarget != t.Source).ToArray();
        Assert.Equal(sources, mismatches.Select(t => (int)t.Source).ToArray());
        Assert.All(mismatches, t => Assert.Equal((ushort)253, t.MethodRaw));
    }

    [Fact]
    public void AuxiliaryTwoUsesMoveSlotsNotAnItemRequirement()
    {
        var forward = Assert.Single(MercuryFormCatalog.Get(406).Transitions);
        Assert.Equal((ushort)911, forward.Target);
        Assert.Equal((ushort)254, forward.MethodRaw);
        Assert.Equal((ushort)559, forward.ParamRaw);
        Assert.Equal((ushort)2, forward.AuxRaw);
        Assert.Equal((byte)0, forward.PhysicalSlot);
        Assert.Equal(0x09795A5Au, forward.RecordAddress);
        Assert.Equal((ushort)406, forward.ReverseTarget);
        Assert.Contains("moves", forward.Condition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("four", forward.Condition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item index", forward.Condition, StringComparison.OrdinalIgnoreCase);
        var reverse = Assert.Single(MercuryFormCatalog.Get(911).Transitions);
        Assert.Equal((ushort)406, reverse.Target);
        Assert.Equal((ushort)254, reverse.MethodRaw);
        Assert.Equal((ushort)0, reverse.ParamRaw);
        Assert.Equal((ushort)2, reverse.AuxRaw);
        Assert.Equal((byte)0, reverse.PhysicalSlot);
        Assert.Equal(0x097A56DAu, reverse.RecordAddress);
        Assert.StartsWith("Fallback", reverse.Condition);
        Assert.Null(reverse.ReverseTarget);
        Assert.Equal((ushort)406, MercuryFormCatalog.Get(911).CoveredReverseFinalTarget);
        string tooltip = FormatTooltip(CreatePokemon(406));
        Assert.Contains("招式索引559", tooltip);
        Assert.DoesNotContain("道具索引559", tooltip);
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
        string flagged = FormatTooltip(CreatePokemon(6));
        Assert.Contains("6→1261", flagged);
        Assert.Contains("非零参数标志", flagged);
        Assert.Contains("运行条件", flagged);
        Assert.Contains("道具索引277、aux1", FormatTooltip(CreatePokemon(404)));
        Assert.DoesNotContain("道具索引277", FormatTooltip(CreatePokemon(910)));
        Assert.Contains("道具索引532、aux3", FormatTooltip(CreatePokemon(1079)));
        Assert.Contains("道具索引532、aux3", FormatTooltip(CreatePokemon(1080)));
    }

    [Fact]
    public void OrderedReverseTooltipDoesNotInventUniqueBaseOrTwoAlternativesToSelect()
    {
        string tooltip = FormatTooltip(CreatePokemon(1081));
        Assert.Contains("已证回退写入顺序", tooltip);
        Assert.Contains("1081→1079", tooltip);
        Assert.Contains("1081→1080", tooltip);
        Assert.True(tooltip.IndexOf("1081→1079", StringComparison.Ordinal) < tooltip.IndexOf("1081→1080", StringComparison.Ordinal));
        Assert.Contains("末次写入1080", tooltip);
        Assert.Contains("全局唯一基础种类", tooltip);
        Assert.Contains("其他持久条件未证", tooltip);
        string single = FormatTooltip(CreatePokemon(869));
        Assert.Contains("已证回退写入顺序", single);
        Assert.Contains("869→3", single);
        Assert.Contains("末次写入3", single);
        Assert.Contains("全局唯一基础种类", single);
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

    [Fact]
    public void CompleteCatalogHasFixedIndependentCanonicalHash()
    {
        var records = Enumerable.Range(0, 1554)
            .SelectMany(s => MercuryFormCatalog.Get((ushort)s).Transitions);
        string canonical = string.Join("\n", records.Select(t => FormattableString.Invariant(
            $"{t.Source},{t.Target},{t.MethodRaw},{t.ParamRaw},{t.AuxRaw},{t.PhysicalSlot},{t.RecordAddress:X8}")));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        Assert.Equal("8FE8AE694BF2B4D221B2D758FEA793A75EA09CBDB3825B84903DB23D92E4B672", hash);
    }

    [Theory]
    [InlineData(201)]
    [InlineData(0x1F6)]
    [InlineData(0x1F7)]
    [InlineData(0x23E)]
    [InlineData(0x285)]
    [InlineData(0x286)]
    [InlineData(0x308)]
    [InlineData(0x338)]
    public void SyntheticOverlapRetainsResourceMechanismAndAddsTransitionContext(int species)
    {
        var original = MercuryFormCatalog.Get((ushort)species);
        Assert.Empty(original.Transitions);
        Assert.DoesNotContain((ushort)species, Sources);
        var method = typeof(MercuryFormMechanism).GetMethod("WithTransitions", BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(MercurySpeciesTransition[]), typeof(ushort?)], null);
        Assert.NotNull(method);
        var transitions = MercuryFormCatalog.Get(1081).Transitions.ToArray();
        var combined = Assert.IsType<MercuryFormMechanism>(method.Invoke(original, [transitions, (ushort?)1080]));
        Assert.NotSame(original, combined);
        Assert.Equal(original.BaseSpecies, combined.BaseSpecies);
        Assert.Equal(original.Kind, combined.Kind);
        Assert.Equal(original.SelectionSource, combined.SelectionSource);
        Assert.Equal(original.Options.ToArray(), combined.Options.ToArray());
        Assert.Equal(original.RequiredContext | MercuryFormContext.TransitionConditions, combined.RequiredContext);
        Assert.Equal(original.CanEditStoredForm, combined.CanEditStoredForm);
        Assert.Equal(transitions, combined.Transitions.ToArray());
        Assert.Equal((ushort)1080, combined.CoveredReverseFinalTarget);
        Assert.Empty(original.Transitions);
        Assert.Null(original.CoveredReverseFinalTarget);
        Assert.Equal(MercuryFormContext.None, original.RequiredContext & MercuryFormContext.TransitionConditions);
        var list = Assert.IsAssignableFrom<IList<MercurySpeciesTransition>>(combined.Transitions);
        Assert.Throws<NotSupportedException>(() => list.Clear());
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
