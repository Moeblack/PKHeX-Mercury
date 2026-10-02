using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;
using PKHeX.WinForms.Controls;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

public partial class BallBrowser : Form
{
    public BallBrowser() => InitializeComponent();

    public bool WasBallChosen { get; private set; }
    public byte BallChoice { get; private set; }
    private MercuryGameData? _mercuryGameData;

    public void LoadBalls(PKM pk)
    {
        if (pk is MercuryPKM mercury)
        {
            _mercuryGameData = mercury.GameData;
            LoadMercuryBalls();
            return;
        }
        Span<Ball> valid = stackalloc Ball[BallApplicator.MaxBallSpanAlloc];
        var legal = BallApplicator.GetLegalBalls(valid, pk);
        LoadBalls(valid[..legal], pk.MaxBallID);
    }

    private void LoadMercuryBalls()
    {
        // These are stored byte values, not an assertion of encounter legality.
        AutoSize = false;
        ClientSize = LogicalToDeviceUnits(new Size(220, 320));
        flp.AutoSize = false;
        flp.AutoScroll = true;
        int count = 0;
        foreach (var entry in GameInfo.Sources.BallDataSource)
        {
            var view = GetBallView(checked((byte)entry.Value), entry.Text, null);
            flp.Controls.Add(view);
            if (++count % 5 == 0)
                flp.SetFlowBreak(view, true);
        }
    }

    private static readonly Bitmap?[] MercuryBallPreviews = new Bitmap[256];
    private static readonly ConditionalWeakTable<MercuryGameData, Bitmap?[]> MercuryRomBallPreviews = new();

    internal static Bitmap GetMercuryBallPreview(byte value, MercuryGameData? data = null)
    {
        if (data is not null)
        {
            var cache = MercuryRomBallPreviews.GetValue(data, _ => new Bitmap?[256]);
            if (cache[value] is { } cached)
                return cached;
            var rgba = data.GetBallSpriteRgba(value, out int width, out int height);
            if (rgba is not null)
            {
                for (int i = 0; i < rgba.Length; i += 4)
                    (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
                return cache[value] = ImageUtil.GetBitmap(rgba, width, height, PixelFormat.Format32bppArgb);
            }
        }
        if (MercuryBallPreviews[value] is { } existing)
            return existing;
        var image = new Bitmap(32, 24);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(SystemColors.Window);
        TextRenderer.DrawText(graphics, $"#{value}", SystemFonts.MessageBoxFont,
            new Rectangle(0, 0, image.Width, image.Height), SystemColors.WindowText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        MercuryBallPreviews[value] = image;
        return image;
    }

    private void LoadBalls(ReadOnlySpan<Ball> legal, int max)
    {
        Span<bool> flags = new bool[max + 1];
        foreach (var ball in legal)
            flags[(int)ball] = true;

        int countLegal = 0;
        List<PictureBox> controls = [];
        var names = GameInfo.Sources.BallDataSource;
        for (int index = 1; index <= max; index++)
        {
            byte ballID = checked((byte)index);
            var name = GetBallName(ballID, names);
            var pb = GetBallView(ballID, name, flags[ballID]);
            if (Main.Settings.EntityEditor.ShowLegalBallsFirst && flags[ballID])
                controls.Insert(countLegal++, pb);
            else
                controls.Add(pb);
        }

        int countInRow = 0;
        var container = flp.Controls;
        foreach (var pb in controls)
        {
            container.Add(pb);
            const int width = 5; // balls wide
            if (++countInRow != width)
                continue;
            flp.SetFlowBreak(pb, true);
            countInRow = 0;
        }
    }

    private static string GetBallName(byte ballID, IEnumerable<ComboItem> names)
    {
        foreach (var x in names)
        {
            if (x.Value == ballID)
                return x.Text;
        }
        throw new ArgumentOutOfRangeException(nameof(ballID));
    }

    private SelectablePictureBox GetBallView(byte ballID, string name, bool? valid)
    {
        var img = valid.HasValue ? SpriteUtil.GetBallSprite(ballID) : GetMercuryBallPreview(ballID, _mercuryGameData);
        var pb = new SelectablePictureBox
        {
            Size = img.Size,
            Image = img,
            BackgroundImage = valid switch { true => SpriteUtil.Spriter.Set, false => SpriteUtil.Spriter.Delete, null => null },
            BackgroundImageLayout = ImageLayout.Tile,
            Name = name,
            AccessibleDescription = name,
            AccessibleName = name,
            AccessibleRole = AccessibleRole.Graphic,
        };

        pb.MouseEnter += (_, _) => Text = name;
        pb.Click += (_, _) => SelectBall(ballID);
        pb.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
                SelectBall(ballID);
        };
        return pb;
    }

    private void SelectBall(byte b)
    {
        BallChoice = b;
        WasBallChosen = true;
        Close();
    }
}
