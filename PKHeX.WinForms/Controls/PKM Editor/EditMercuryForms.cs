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
            MercuryFormMechanismKind.ConditionalSpeciesTransition => string.Empty,
            _ => "形态机制未查明",
        };
        if (mechanism.Transitions.Count != 0)
        {
            if (description.Length != 0)
                description += Environment.NewLine;
            description += GetMercuryTransitionTooltip(mechanism);
        }
        return pk.Species.ToString("000") + Environment.NewLine + description;
    }

    private static string GetMercuryTransitionTooltip(MercuryFormMechanism mechanism)
    {
        string relations = string.Join("；", mechanism.Transitions
            .Where(t => t.MethodRaw != 254 || t.ParamRaw != 0)
            .Select(t => t.ParamRaw == 0
                ? $"已证表内首匹配回退至种类{t.Target}（bank上下文可由backup覆盖）"
                : t.MethodRaw == 253
                    ? $"{t.Source}→{t.Target}（非零参数标志、个体位及运行条件）"
                    : t.AuxRaw == 2
                        ? $"{t.Source}→{t.Target}（四个已学招式匹配招式索引{t.ParamRaw}、aux2及运行门控）"
                        : $"{t.Source}→{t.Target}（道具索引{t.ParamRaw}、aux{t.AuxRaw}及运行条件）"));
        string description = relations.Length == 0 ? string.Empty : $"已证条件转换：{relations}。";
        if (mechanism.CoveredReverseFinalTarget is { } finalTarget)
        {
            string ordered = string.Join("；", mechanism.Transitions
                .Where(t => t.MethodRaw == 254 && t.ParamRaw == 0)
                .Select(t => $"{t.Source}→{t.Target}"));
            description += $"已证回退写入顺序：{ordered}。已覆盖的09D423E4路径末次写入{finalTarget}，不代表全局唯一基础种类。";
        }
        string writeScope = mechanism.Transitions.Any(t => t.ParamRaw != 0)
            ? "已覆盖的转换函数临时写入目标，并在返回前恢复原species；外层运行门控尚未全部映射，不表示当前满足条件"
            : "已覆盖的接收/复制路径执行上述直接回退";
        return $"{description}{writeScope}；其他持久条件未证";
    }
}
