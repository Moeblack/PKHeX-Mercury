using System;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Renders the native front sprite RGBA preview from the ROM: LZ77 tile data + ROM palette,
/// first frame, first palette page. The result is a 64x64 RGBA8888 buffer (row-major, top-left origin),
/// matching the verified research sprite output byte-for-byte (see docs/mercury-rom-profile.md).
/// </summary>
internal static class MercurySpriteLoader
{
    public const int Width = MercuryRomLayout.SpriteWidth;
    public const int Height = MercuryRomLayout.SpriteHeight;

    /// <summary>
    /// True when <paramref name="pid"/> / <paramref name="trainerId"/> select the shiny palette,
    /// per the ROM's four-16-bit-half XOR test (&lt;= 7).
    /// </summary>
    public static bool IsShiny(uint pid, uint trainerId)
    {
        uint xor = ((trainerId >> 16) ^ (trainerId & 0xFFFF) ^ (pid >> 16) ^ (pid & 0xFFFF));
        return xor <= 7;
    }

    public static bool TryRender(byte[] rom, int resourceIndex, uint pid, uint trainerId, out byte[] rgba)
    {
        rgba = [];
        if (resourceIndex < 0 || resourceIndex >= MercuryRomLayout.SpeciesCount)
            return false; // the ROM itself rejects indices >= 1554
        if (!MercuryRomLayout.TryGetFrontPicTable(rom, out uint frontTable))
            return false;
        if (!TryReadEntry(rom, frontTable, resourceIndex, out uint dataPtr, out _))
            return false;
        if (!MercuryRomLayout.IsRomAddress(dataPtr))
            return false;
        if (!MercuryLz77.TryDecompress(rom, dataPtr, out byte[] tiles))
            return false;
        if (tiles.Length < MercuryRomLayout.SpriteFrameBytes)
            return false;

        uint paletteSlot = IsShiny(pid, trainerId) ? MercuryRomLayout.ShinyPaletteSlot : MercuryRomLayout.PaletteSlot;
        if (!MercuryRomLayout.TryReadU32(rom, paletteSlot, out uint paletteTable))
            return false;
        if (!MercuryRomLayout.IsRomAddress(paletteTable))
            return false;
        if (!TryReadEntry(rom, paletteTable, resourceIndex, out uint palettePtr, out _))
            return false;
        if (!MercuryRomLayout.IsRomAddress(palettePtr))
            return false;
        if (!MercuryLz77.TryDecompress(rom, palettePtr, out byte[] paletteBytes))
            return false;
        if (paletteBytes.Length < MercuryRomLayout.PaletteBytes)
            return false;

        Span<int> colors = stackalloc int[16];
        for (int i = 0; i < 16; i++)
        {
            ushort value = (ushort)(paletteBytes[2 * i] | (paletteBytes[(2 * i) + 1] << 8));
            colors[i] = Expand555(value);
        }

        var output = new byte[Width * Height * 4];
        for (int pixel = 0; pixel < Width * Height; pixel++)
        {
            byte packed = tiles[pixel >> 1];
            int colorIndex = (pixel & 1) == 0 ? packed & 0x0F : packed >> 4;
            int x = (pixel % 8) + (((pixel / 64) % 8) * 8);
            int y = ((pixel / 8) % 8) + ((pixel / 512) * 8);
            int offset = ((y * Width) + x) * 4;
            int color = colors[colorIndex];
            output[offset + 0] = (byte)(color & 0xFF);          // R
            output[offset + 1] = (byte)((color >> 8) & 0xFF);   // G
            output[offset + 2] = (byte)((color >> 16) & 0xFF);  // B
            output[offset + 3] = colorIndex == 0 ? (byte)0 : (byte)0xFF; // index 0 is transparent
        }

        rgba = output;
        return true;
    }

    private static bool TryReadEntry(byte[] rom, uint table, int index, out uint dataPtr, out ushort size)
    {
        dataPtr = 0;
        size = 0;
        long offset = MercuryRomLayout.ToOffset(table) + (8L * index);
        if (!MercuryRomLayout.TryReadU32Raw(rom, offset, out dataPtr))
            return false;
        if (!MercuryRomLayout.TryReadU16Raw(rom, offset + 4, out size))
            return false;
        return true;
    }

    /// <summary>BGR555 -&gt; RGB888 packed as 0x00BBGGRR (little-endian RGBA friendly).</summary>
    private static int Expand555(ushort value)
    {
        int r = value & 0x1F;
        int g = (value >> 5) & 0x1F;
        int b = (value >> 10) & 0x1F;
        r = (r << 3) | (r >> 2);
        g = (g << 3) | (g >> 2);
        b = (b << 3) | (b >> 2);
        return r | (g << 8) | (b << 16);
    }
}
