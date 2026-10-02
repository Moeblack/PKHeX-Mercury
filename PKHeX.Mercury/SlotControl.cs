using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>箱子/队伍中的单格图文槽位。</summary>
internal sealed class SlotControl : Control
{
    private MercuryPokemon? _mon;
    private bool _selected;

    public SlotRef Reference { get; set; }
    public bool IsParty { get; init; }

    public SlotControl()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Margin = new Padding(2);
        BackColor = SystemColors.Window;
        TabStop = false;
    }

    public void SetMon(MercuryPokemon? mon)
    {
        _mon = mon;
        Invalidate();
    }

    public MercuryPokemon? Mon => _mon;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        var back = _selected ? Color.FromArgb(0xC8, 0xE4, 0xFF) : SystemColors.Window;
        using (var b = new SolidBrush(back))
            g.FillRectangle(b, rect);

        using (var pen = new Pen(Color.FromArgb(0x9A, 0x9A, 0x9A)))
            g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);

        if (_mon is null || _mon.IsEmpty)
        {
            using var empty = new SolidBrush(Color.FromArgb(0xB0, 0xB0, 0xB0));
            var text = IsParty ? "空" : "空";
            var size = g.MeasureString(text, Font);
            g.DrawString(text, Font, empty, (rect.Width - size.Width) / 2, (rect.Height - size.Height) / 2);
            return;
        }

        var sizePx = Math.Max(24, Math.Min(rect.Width - 8, rect.Height - 26));
        var sprite = SpriteCache.GetScaled(_mon.Species, _mon.PID, _mon.ID32, sizePx);
        if (sprite is not null)
        {
            g.DrawImage(sprite, (rect.Width - sizePx) / 2, 2, sizePx, sizePx);
        }
        else
        {
            using var noImg = new SolidBrush(Color.FromArgb(0x66, 0x66, 0x66));
            var tag = $"#{_mon.Species}";
            var s = g.MeasureString(tag, Font);
            g.DrawString(tag, Font, noImg, (rect.Width - s.Width) / 2, 8);
        }

        var name = UiText.MonLabel(_mon);
        using var fg = new SolidBrush(SystemColors.ControlText);
        var nameRect = new RectangleF(2, rect.Height - 22, rect.Width - 4, 14);
        using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, Alignment = StringAlignment.Center })
            g.DrawString(name, Font, fg, nameRect, sf);

        var level = _mon.PartyLevel;
        if (level == 0 && AppState.HasGrowthTables)
        {
            try { level = AppState.Data.GetLevel(_mon.Species, _mon.Experience); }
            catch { level = 0; }
        }
        if (level > 0)
        {
            using var sub = new Font(Font.FontFamily, Font.Size - 1.5f);
            using var sf2 = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, Alignment = StringAlignment.Center };
            g.DrawString($"Lv.{level}", sub, fg, new RectangleF(2, rect.Height - 12, rect.Width - 4, 12), sf2);
        }
    }
}
