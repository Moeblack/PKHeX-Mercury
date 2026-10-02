using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using PKHeX.Drawing;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

/// <summary>Read-only resource selection for one editor snapshot, not a game animation or saved form.</summary>
internal sealed class MercurySpritePreview : Form
{
    private readonly MercuryPKM _snapshot;
    private readonly PictureBox _picture = new() { Name = "PB_Sprite", Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly ComboBox _frames = new() { Name = "CB_Frame", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
    private readonly ComboBox _pages = new() { Name = "CB_PalettePage", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
    private readonly Label _counts = new() { Name = "L_Counts", AutoSize = true };
    private readonly Label _trailing = new() { Name = "L_Trailing", AutoSize = true };
    private readonly Label _status = new() { Name = "L_Status", AutoSize = true };
    private MercurySpriteSelection _selection;
    private bool? _runtimeState;
    private bool _initializing = true;

    public MercurySpritePreview(MercuryPKM snapshot)
    {
        _snapshot = snapshot;
        Text = L("Title", "Mercury sprite resources");
        Name = nameof(MercurySpritePreview);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(500, 500);
        MinimumSize = new Size(440, 480);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;

        bool showRuntime = snapshot.Species == 0x338;
        int pictureRow = showRuntime ? 4 : 3;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = pictureRow + 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < layout.RowCount; i++)
            layout.RowStyles.Add(new RowStyle(i == pictureRow ? SizeType.Percent : SizeType.AutoSize, i == pictureRow ? 100 : 0));
        Controls.Add(layout);

        uint pid = snapshot.PID;
        uint trainerId = snapshot.ID32;
        uint shinyXor = (pid >> 16) ^ (pid & 0xFFFF) ^ (trainerId >> 16) ^ (trainerId & 0xFFFF);
        string palette = shinyXor <= 7 ? L("Shiny", "Shiny palette") : L("Normal", "Normal palette");
        var identity = new Label
        {
            Name = "L_Identity", AutoSize = true,
            Text = string.Format(CultureInfo.CurrentCulture, L("Identity", "Species {0} | PID {1:X8} | OTID {2:X8} | {3}"), snapshot.Species, pid, trainerId, palette),
            MaximumSize = new Size(460, 0),
        };
        AddWide(layout, identity, 0);
        layout.Controls.Add(new Label { AutoSize = true, Text = L("Frame", "Resource frame") }, 0, 1);
        layout.Controls.Add(_frames, 1, 1);
        layout.Controls.Add(new Label { AutoSize = true, Text = L("Page", "Palette page") }, 0, 2);
        layout.Controls.Add(_pages, 1, 2);
        if (showRuntime)
        {
            var states = new ComboBox { Name = "CB_RuntimeState", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = nameof(RuntimeStateOption.Text) };
            states.Items.AddRange(new object[]
            {
                new RuntimeStateOption(L("RuntimeDefault", "Unspecified (default resource)"), null),
                new RuntimeStateOption(L("RuntimeZero", "State bit 0"), false),
                new RuntimeStateOption(L("RuntimeOne", "State bit 1"), true),
            });
            states.SelectedIndex = 0;
            states.SelectedIndexChanged += RuntimeStateChanged;
            layout.Controls.Add(new Label { AutoSize = true, Text = L("Runtime", "Runtime image preview") }, 0, 3);
            layout.Controls.Add(states, 1, 3);
        }
        AddWide(layout, _picture, pictureRow);
        AddWide(layout, _counts, pictureRow + 1);
        AddWide(layout, _trailing, pictureRow + 2);
        AddWide(layout, _status, pictureRow + 3);
        AddWide(layout, new Label
        {
            Name = "L_Note", AutoSize = true, MaximumSize = new Size(460, 0),
            Text = L("Note", "Independent resource frames and palettes; not game animation or saved forms."),
        }, pictureRow + 4);
        var close = new Button { AutoSize = true, Text = L("Close", "Close"), DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Right };
        AddWide(layout, close, pictureRow + 5);
        CancelButton = close;
        _frames.SelectedIndexChanged += SelectionChanged;
        _pages.SelectedIndexChanged += SelectionChanged;
        ReloadResources();
    }

    private static void AddWide(TableLayoutPanel layout, Control control, int row)
    {
        layout.Controls.Add(control, 0, row);
        layout.SetColumnSpan(control, 2);
    }

    private static string L(string key, string fallback)
        => WinFormsTranslator.TranslateText($"Mercury.SpritePreview.{key}", fallback, Main.CurrentLanguage);

    private sealed record RuntimeStateOption(string Text, bool? Value);

    private void RuntimeStateChanged(object? sender, EventArgs e)
    {
        if (_initializing || sender is not ComboBox { SelectedItem: RuntimeStateOption option })
            return;
        _runtimeState = option.Value;
        ReloadResources();
    }

    private void ReloadResources()
    {
        _initializing = true;
        try
        {
            _selection = default;
            _frames.Items.Clear();
            _pages.Items.Clear();
            _frames.Enabled = _pages.Enabled = false;
            _counts.Text = _trailing.Text = _status.Text = string.Empty;
            ReplaceImage(null);
            LoadResources();
        }
        finally
        {
            _initializing = false;
        }
    }

    private void LoadResources()
    {
        if (!_snapshot.GameData.HasSpriteResources)
        {
            _status.Text = L("NoResources", "Cannot render: this snapshot has neither ROM nor data-pack sprite resources.");
            return;
        }

        var rgba = _snapshot.GameData.GetSpriteRgba(_snapshot.Species, _snapshot.PID, _snapshot.ID32,
            default, out int width, out int height, out var metadata, runtimeState: _runtimeState);
        if (rgba is null)
        {
            ShowUnavailable();
            return;
        }

        for (int i = 0; i < metadata.FrameCount; i++)
            _frames.Items.Add(i + 1);
        for (int i = 0; i < metadata.PalettePageCount; i++)
            _pages.Items.Add(i + 1);
        _frames.SelectedIndex = _pages.SelectedIndex = 0;
        _frames.Enabled = _pages.Enabled = true;
        _counts.Text = string.Format(CultureInfo.CurrentCulture,
            L("Counts", "Complete resource frames: {0}; palette pages: {1}"), metadata.FrameCount, metadata.PalettePageCount);
        if (metadata.TrailingTileBytes != 0 || metadata.TrailingPaletteBytes != 0)
        {
            _trailing.Text = string.Format(CultureInfo.CurrentCulture,
                L("Trailing", "Unselectable trailing bytes — tiles: {0}; palette: {1}"), metadata.TrailingTileBytes, metadata.TrailingPaletteBytes);
        }
        ReplaceImage(ToBitmap(rgba, width, height));
    }

    private void SelectionChanged(object? sender, EventArgs e)
    {
        if (_initializing || _frames.SelectedIndex < 0 || _pages.SelectedIndex < 0)
            return;
        _selection = new MercurySpriteSelection(_frames.SelectedIndex, _pages.SelectedIndex);
        var rgba = _snapshot.GameData.GetSpriteRgba(_snapshot.Species, _snapshot.PID, _snapshot.ID32,
            _selection, out int width, out int height, out _, runtimeState: _runtimeState);
        if (rgba is null)
        {
            ShowUnavailable();
            return;
        }
        _status.Text = string.Empty;
        ReplaceImage(ToBitmap(rgba, width, height));
    }

    private void ShowUnavailable()
    {
        ReplaceImage(null);
        _status.Text = L("Unavailable", "Cannot render this selection: the ROM resource is unavailable, undecodable, or has no complete frame/palette page.");
        _status.MaximumSize = new Size(460, 0);
    }

    private static Bitmap ToBitmap(byte[] rgba, int width, int height)
    {
        // The data API owns no shared image cache; this byte array and bitmap belong to this window.
        for (int i = 0; i < rgba.Length; i += 4)
            (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
        return ImageUtil.GetBitmap(rgba, width, height);
    }

    private void ReplaceImage(Bitmap? image)
    {
        var previous = _picture.Image;
        _picture.Image = image;
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ReplaceImage(null);
        base.Dispose(disposing);
    }
}
