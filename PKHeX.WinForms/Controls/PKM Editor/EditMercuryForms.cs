using System;
using System.Linq;
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
            MercuryFormMechanismKind.ConditionalSpeciesTransition => GetMercuryTransitionTooltip(mechanism),
            _ => "形态机制未查明",
        };
        return pk.Species.ToString("000") + Environment.NewLine + description;
    }

    private static string GetMercuryTransitionTooltip(MercuryFormMechanism mechanism)
    {
        string relations = string.Join("；", mechanism.Transitions.Select(t => t.ParamRaw == 0
            ? $"已证复制前回退至基础种类{t.Target}"
            : t.MethodRaw == 254
                ? $"{t.Source}→{t.Target}（道具索引{t.ParamRaw}及其他运行条件）"
                : $"{t.Source}→{t.Target}（个体标志及运行条件）"));
        return $"已证条件转换：{relations}。已覆盖的接收/复制路径会恢复基础种类；其他持久条件未证";
    }
}
