using System;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms.Controls;

public partial class PKMEditor
{
    private static string GetMercurySpeciesTooltip(MercuryPKM pk)
    {
        var mechanism = MercuryFormCatalog.Get(pk.Species);
        string description = mechanism.Kind switch
        {
            MercuryFormMechanismKind.PidDerived => "形态由PID决定，可通过原生形态选择器编辑",
            MercuryFormMechanismKind.GenderDependentResource => "图像差异由性别/PID决定，不是独立持久形态字段",
            MercuryFormMechanismKind.RuntimeDependentResource => "图像受运行时状态影响，当前存档无法确定该状态，不写入形态字段",
            _ => "形态机制未查明",
        };
        return pk.Species.ToString("000") + Environment.NewLine + description;
    }
}
