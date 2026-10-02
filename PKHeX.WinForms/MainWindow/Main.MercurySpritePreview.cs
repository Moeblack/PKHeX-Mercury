using System;
using System.Windows.Forms;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

public partial class Main
{
    private void InitializeMercurySpritePreview()
    {
        var menu = dragout.ContextMenuStrip!;
        var item = new ToolStripMenuItem
        {
            Name = "Menu_MercurySpritePreview",
            Visible = false,
            Enabled = false,
        };
        menu.Items.Add(item);
        menu.Opening += (_, _) =>
        {
            item.Text = WinFormsTranslator.TranslateText("Mercury.SpritePreview.Title", "Mercury sprite resources", CurrentLanguage);
            bool available = PKME_Tabs.Entity is MercuryPKM { Species: not 0 };
            item.Visible = item.Enabled = available;
        };
        item.Click += OpenMercurySpritePreview;
    }

    private void OpenMercurySpritePreview(object? sender, EventArgs e)
    {
        if (PKME_Tabs.Entity is not MercuryPKM { Species: not 0 } pk)
            return;

        // Do not prepare the editor: merely opening a resource viewer must not commit pending fields.
        using var preview = new MercurySpritePreview((MercuryPKM)pk.Clone());
        preview.ShowDialog(this);
    }
}
