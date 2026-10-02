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
        if (mechanism.CoveredReverseFinalTarget is { } finalTarget)
        {
            string ordered = string.Join("；", mechanism.Transitions.Select(t => $"{t.Source}→{t.Target}"));
            return $"已证回退写入顺序：{ordered}。已覆盖的09D423E4路径末次写入{finalTarget}，不代表全局唯一基础种类；其他持久条件未证";
        }

        string relations = string.Join("；", mechanism.Transitions.Select(t => t.ParamRaw == 0
            ? $"已证复制前直接回退至种类{t.Target}"
            : t.MethodRaw == 254
                ? $"{t.Source}→{t.Target}（道具索引{t.ParamRaw}、aux{t.AuxRaw}及已证运行条件）"
                : $"{t.Source}→{t.Target}（个体标志及运行条件）"));
        string writeScope = mechanism.Transitions.Any(t => t.ParamRaw != 0)
            ? "已覆盖的转换函数临时写入目标，并在返回前恢复原species；回退条件按各记录区分"
            : "已覆盖的接收/复制路径执行上述直接回退";
        return $"已证条件转换：{relations}。{writeScope}；其他持久条件未证";
    }
}
