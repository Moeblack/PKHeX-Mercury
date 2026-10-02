using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>
/// 由 MercuryGameData 返回的 RGBA 原始像素构造 Bitmap，并按需缩放缓存。
/// 契约明确：无解析结果返回 null，不伪造灰色占位图。
/// </summary>
internal static class SpriteCache
{
    private static readonly Dictionary<(int Species, uint Pid, uint Id32, int W, int H), Bitmap?> Raw = [];
    private static readonly Dictionary<(Bitmap Src, int Size), Bitmap> Scaled = [];

    public static Bitmap? Get(MercuryPokemon mon)
        => Get(mon.Species, mon.PID, mon.ID32);

    public static Bitmap? Get(int species, uint pid, uint id32)
    {
        if (!AppState.HasRomData || species <= 0)
            return null;

        byte[]? rgba;
        int w, h;
        try
        {
            rgba = AppState.Data.GetSpriteRgba(species, pid, id32, out w, out h);
        }
        catch
        {
            return null;
        }

        if (rgba is null || w <= 0 || h <= 0 || rgba.Length < w * h * 4)
            return null;

        var key = (species, pid, id32, w, h);
        if (Raw.TryGetValue(key, out var cached))
            return cached;

        Bitmap? bmp = null;
        try
        {
            bmp = FromRgba(rgba, w, h);
        }
        catch
        {
            bmp = null;
        }

        Raw[key] = bmp;
        return bmp;
    }

    public static Bitmap? GetScaled(int species, uint pid, uint id32, int size)
    {
        var src = Get(species, pid, id32);
        return src is null ? null : Scale(src, size);
    }

    private static Bitmap Scale(Bitmap src, int size)
    {
        var key = (src, size);
        if (Scaled.TryGetValue(key, out var cached))
            return cached;

        var dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dst))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.Clear(Color.Transparent);
            var scale = Math.Min((float)size / src.Width, (float)size / src.Height);
            var w = Math.Max(1, (int)(src.Width * scale));
            var h = Math.Max(1, (int)(src.Height * scale));
            g.DrawImage(src, (size - w) / 2, (size - h) / 2, w, h);
        }
        Scaled[key] = dst;
        return dst;
    }

    private static Bitmap FromRgba(byte[] rgba, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                var srcRow = y * width * 4;
                var dstRow = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var s = srcRow + (x * 4);
                    var d = dstRow + (x * 4);
                    // 目标为 BGRA（小端 32bppArgb），来源约定 RGBA8888。
                    buffer[d + 0] = rgba[s + 2];
                    buffer[d + 1] = rgba[s + 1];
                    buffer[d + 2] = rgba[s + 0];
                    buffer[d + 3] = rgba[s + 3];
                }
            }
            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        // 预乘 alpha，保证透明像素显示正常。
        return bmp;
    }

    public static void Clear()
    {
        foreach (var b in Raw.Values)
            b?.Dispose();
        Raw.Clear();
        foreach (var b in Scaled.Values)
            b.Dispose();
        Scaled.Clear();
    }
}
