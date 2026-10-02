using System.Collections.Generic;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

/// <summary>
/// Structural validation for Mercury entities using the loaded ROM's facts.
/// <para>
/// Mercury internal ids must never be fed to the retail encounter/learn tables, and HaX must not be used to
/// suppress failures. Only facts that are actually proven by the extracted ROM data are checked here;
/// capture/obtainability is not yet proven, so a structurally clean entity is reported as <b>unverified</b>
/// rather than green-legitimate.
/// </para>
/// </summary>
internal static class MercuryLegality
{
    /// <summary>Returns human-readable structural issues; an empty list means "structurally consistent, obtainability unverified".</summary>
    public static List<string> Evaluate(MercuryPKM pk)
    {
        var issues = new List<string>();
        var data = pk.GameData;

        int species = pk.Species;
        if (species == 0)
            issues.Add("未选择种类。");
        else if ((uint)species >= (uint)data.Species.Count || !data.GetSpecies(species).HasData)
            issues.Add($"种类 {species} 超出 ROM 实际数据范围。");

        if (!IsMoveInRange(pk, pk.Move1) || !IsMoveInRange(pk, pk.Move2) || !IsMoveInRange(pk, pk.Move3) || !IsMoveInRange(pk, pk.Move4))
            issues.Add("存在超出 ROM 招式范围的招式。");

        CheckRange(issues, pk.IV_HP, 0, 31, "HP 个体值");
        CheckRange(issues, pk.IV_ATK, 0, 31, "攻击个体值");
        CheckRange(issues, pk.IV_DEF, 0, 31, "防御个体值");
        CheckRange(issues, pk.IV_SPE, 0, 31, "速度个体值");
        CheckRange(issues, pk.IV_SPA, 0, 31, "特攻个体值");
        CheckRange(issues, pk.IV_SPD, 0, 31, "特防个体值");

        CheckRange(issues, pk.EV_HP, 0, 255, "HP 努力值");
        CheckRange(issues, pk.EV_ATK, 0, 255, "攻击努力值");
        CheckRange(issues, pk.EV_DEF, 0, 255, "防御努力值");
        CheckRange(issues, pk.EV_SPE, 0, 255, "速度努力值");
        CheckRange(issues, pk.EV_SPA, 0, 255, "特攻努力值");
        CheckRange(issues, pk.EV_SPD, 0, 255, "特防努力值");
        if (pk.EVTotal > 510)
            issues.Add($"努力值总计 {pk.EVTotal} 超过 510。");

        if (pk.Nickname.Length > pk.MaxStringLengthNickname)
            issues.Add($"昵称超过 {pk.MaxStringLengthNickname} 字符上限。");

        return issues;
    }

    /// <summary>True when the move id fits inside the ROM's proven move range.</summary>
    public static bool IsMoveInRange(MercuryPKM pk, ushort move)
        => move == 0 || (uint)move < (uint)pk.GameData.Moves.Count;

    private static void CheckRange(List<string> issues, int value, int min, int max, string label)
    {
        if (value < min || value > max)
            issues.Add($"{label} {value} 超出 {min}..{max}。");
    }

    /// <summary>Manual checks report the pause explicitly, without evaluating the entity.</summary>
    public static void Show(IWin32Window owner, MercuryPKM pk) => ShowPaused(owner);

    public static void ShowPaused(IWin32Window owner)
    {
        TaskDialog.ShowDialog(owner, new TaskDialogPage
        {
            Caption = "Mercury",
            Heading = "水银合法性检查已暂停",
            Text = "当前未执行宝可梦合法性检查。未检查不代表合法；原版存档的检查不受影响。",
            Buttons = [TaskDialogButton.OK],
            DefaultButton = TaskDialogButton.OK,
            AllowCancel = true,
            SizeToContent = true,
        });
    }
}
