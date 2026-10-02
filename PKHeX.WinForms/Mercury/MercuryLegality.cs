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
        => MercuryLegalityAnalysis.Analyze(pk).Checks
            .Where(z => z.Status != MercuryCheckStatus.Pass)
            .Select(z => $"[{z.Code}] {z.Status}: {z.Evidence}").ToList();

    public static void Show(IWin32Window owner, MercuryPKM pk)
    {
        var result = MercuryLegalityAnalysis.Analyze(pk);
        string report = string.Join(Environment.NewLine + Environment.NewLine,
            result.Checks.Select(z => $"[{z.Code}] {StatusText(z.Status)}{Environment.NewLine}{z.Evidence}"));
        TaskDialog.ShowDialog(owner, new TaskDialogPage
        {
            Caption = "Mercury",
            Heading = $"水银已覆盖检查：{StatusText(result.Status)}",
            Text = result.Summary + "\n未运行原版LegalityAnalysis；不提供完整合法绿勾。",
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

    private static string StatusText(MercuryCheckStatus status) => status switch
    {
        MercuryCheckStatus.Invalid => "Invalid（无效）",
        MercuryCheckStatus.Pass => "Pass（已检查字段通过）",
        _ => "Unknown（未知）",
    };

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
