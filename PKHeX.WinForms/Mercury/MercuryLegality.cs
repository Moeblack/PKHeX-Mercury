using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

/// <summary>Presentation of Mercury-only evidence-scoped checks; never a retail legality verdict.</summary>
internal static class MercuryLegality
{
    /// <summary>Compatibility report: includes unknown checks as well as invalid fields.</summary>
    public static List<string> Evaluate(MercuryPKM pk)
        => MercuryLegalityAnalysis.Analyze(pk, MercuryEncounterContext.TryGet(pk.GameData)).Checks
            .Where(z => z.Status != MercuryCheckStatus.Pass)
            .Select(z => $"[{z.Code}] {RoleText(z.Role)} / {StatusText(z.Status)}: {z.Evidence}").ToList();

    public static void Show(IWin32Window owner, MercuryPKM pk)
    {
        var result = MercuryLegalityAnalysis.Analyze(pk, MercuryEncounterContext.TryGet(pk.GameData));
        string report = string.Join(Environment.NewLine + Environment.NewLine,
            result.Checks.Select(z => $"[{z.Code}] {RoleText(z.Role)} / {StatusText(z.Status)}{Environment.NewLine}{z.Evidence}"));
        TaskDialog.ShowDialog(owner, new TaskDialogPage
        {
            Caption = "Mercury",
            Heading = $"水银已覆盖检查：{StatusText(result.Status)}",
            Text = result.Summary + "\n辅助诊断的Unknown不阻止适用检查通过；任何Invalid仍优先。\n未运行原版LegalityAnalysis；不提供完整合法绿勾。",
            Expander = new TaskDialogExpander
            {
                CollapsedButtonText = "逐项证据与缺口",
                ExpandedButtonText = "收起逐项报告",
                Expanded = true,
                Text = report,
            },
            Buttons = [TaskDialogButton.OK],
            DefaultButton = TaskDialogButton.OK,
            AllowCancel = true,
            SizeToContent = true,
        });
    }

    private static string RoleText(MercuryCheckRole role) => role switch
    {
        MercuryCheckRole.Diagnostic => "Diagnostic 辅助诊断（不单独决定总结果）",
        _ => "Required 适用检查",
    };

    private static string StatusText(MercuryCheckStatus status) => status switch
    {
        MercuryCheckStatus.Invalid => "Invalid（无效）",
        MercuryCheckStatus.Pass => "Pass（已覆盖检查通过）",
        _ => "Unknown（未知）",
    };

    public static void ShowBulk(IWin32Window owner, MercurySaveFile save)
    {
        var result = MercuryBulkLegalityAnalysis.Analyze(save, MercuryEncounterContext.TryGet(save.GameData));
        if (result.Count == 0)
        {
            TaskDialog.ShowDialog(owner, new TaskDialogPage
            {
                Caption = "Mercury",
                Heading = "水银批量已覆盖检查",
                Text = result.Summary,
                Buttons = [TaskDialogButton.OK],
                DefaultButton = TaskDialogButton.OK,
                AllowCancel = true,
            });
            return;
        }

        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, result.Summary,
                "是否将逐槽完整检查报告复制到剪贴板？辅助诊断Unknown仍逐项保留；未运行零售批量规则。") != DialogResult.Yes)
            return;

        WinFormsUtil.SetClipboardText(result.Report());
        WinFormsUtil.Asterisk();
    }

    public static void ShowPaused(IWin32Window owner)
    {
        TaskDialog.ShowDialog(owner, new TaskDialogPage
        {
            Caption = "Mercury",
            Heading = "水银批量来源检查：Unknown（未知）",
            Text = "尚未实现批量来源检查，未知不代表合法。请在单只宝可梦的合法性报告入口查看已覆盖字段、逐项证据和缺口。原版存档的检查不受影响。",
            Buttons = [TaskDialogButton.OK],
            DefaultButton = TaskDialogButton.OK,
            AllowCancel = true,
            SizeToContent = true,
        });
    }
}
