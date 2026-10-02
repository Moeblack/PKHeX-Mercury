using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

internal static class MercuryDataPackSetup
{
    private static string L(string key, string fallback)
        => WinFormsTranslator.TranslateText($"Mercury.{key}", fallback, Main.CurrentLanguage);

    internal static async Task InstallAsync(IWin32Window? owner, ToolStripMenuItem? item, string targetDirectory,
        Action<MercuryGameData> publish)
    {
        using var dialog = new FolderBrowserDialog { Description = L("SelectInstallPack", "Select a local Mercury data pack to install as the default") };
        if (dialog.ShowDialog(owner) != DialogResult.OK)
            return;
        if (Directory.Exists(targetDirectory) && WinFormsUtil.Prompt(MessageBoxButtons.OKCancel,
                L("ReplacePackConfirm", "Replace the installed default data pack? The previous directory will be kept as a uniquely named backup."),
                targetDirectory) != DialogResult.OK)
            return;

        using var progress = new Form
        {
            Text = L("InstallPackWorking", "Installing the default data pack"),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(480, 110),
            MaximizeBox = false,
            MinimizeBox = false,
            ControlBox = false,
            ShowInTaskbar = false,
        };
        progress.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, Padding = new Padding(12),
            Text = L("InstallPackDetail", "Validating and copying the local pack. No ROM is copied and no network is used. Any previous default pack is retained as a backup."),
        });
        progress.Controls.Add(new ProgressBar { Dock = DockStyle.Bottom, Style = ProgressBarStyle.Marquee });
        if (item is not null)
            item.Enabled = false;
        MercuryDataPackInstallResult? installed;
        MercuryGameData data;
        try
        {
            progress.Show(owner);
            (installed, data) = await Task.Run(() =>
            {
                var result = new MercuryDataPackInstaller().Install(dialog.SelectedPath, targetDirectory)!;
                return (result, MercuryGameData.LoadPack(result.Directory));
            });
        }
        catch (Exception error)
        {
            progress.Hide();
            WinFormsUtil.Error(L("InstallPackFailed", "Default pack installation/loading failed. The active data has not been replaced. See the error for rollback and backup details."), error);
            return;
        }
        finally
        {
            progress.Close();
            if (item is not null)
                item.Enabled = true;
        }
        publish(data);
        WinFormsUtil.Alert(L("InstallPackDone", "The data pack is installed as the default for future starts."),
            installed!.Directory, string.Format(L("InstallPackBackup", "Previous pack backup: {0}"),
                installed.BackupDirectory ?? L("InstallPackNoBackup", "none (first installation)")));
    }
}
