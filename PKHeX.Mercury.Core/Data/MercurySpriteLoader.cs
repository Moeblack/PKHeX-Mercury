using System;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Renders the native front sprite RGBA preview from the ROM: LZ77 tile data + ROM palette,
/// with independent frame and palette-page selection (default 0/0). The result is a 64x64
/// RGBA8888 buffer (row-major, top-left origin). This does not define a game animation.
/// </summary>
internal static class MercurySpriteLoader
{
    public const int Width = MercuryRomLayout.SpriteWidth;
    public const int Height = MercuryRomLayout.SpriteHeight;

    // --- item icons (consumer 0x08098974) ---
    // Table literal at 0x0809899C holds 0x093C8100; entry = base + item*8 + selector*4,
    // selector 0 = LZ77 tiles, 1 = LZ77 BGR555 palette. The composer 0x0809872C copies
    // 3 rows of 3 8x8 tiles (0x60 bytes/row) -> 24x24, standard 4bpp, index 0 transparent.
    public const int ItemIconWidth = 24;
    public const int ItemIconHeight = 24;
    public const int ItemIconTileBytes = 288;  // 3 * 3 tiles * 32 bytes
    public const int ItemIconPaletteBytes = 32; // 16 colours
    private const int ItemIconTilesPerRow = 3;
    private const uint ItemSpriteTableLiteral = 0x0809899C;

    /// <summary>
    /// True when <paramref name="pid"/> / <paramref name="trainerId"/> select the shiny palette,
    /// per the ROM's four-16-bit-half XOR test (&lt;= 7).
    /// </summary>
    public static bool IsShiny(uint pid, uint trainerId)
    {
        uint xor = ((trainerId >> 16) ^ (trainerId & 0xFFFF) ^ (pid >> 16) ^ (pid & 0xFFFF));
        return xor <= 7;
    }

    public static bool TryRender(byte[] rom, int resourceIndex, uint pid, uint trainerId, out byte[] rgba, int? paletteIndex = null)
        => TryRender(rom, resourceIndex, pid, trainerId, default, out rgba, out _, paletteIndex);

    /// <summary>
    /// Renders one explicit complete frame/page pair. Invalid selections fail without clamping;
    /// outputs remain empty/default on failure. Resource tails are reported, not rendered as frames/pages.
    /// The optional palette table index is independent of both the tile resource index and page selection.
    /// </summary>
    public static bool TryRender(byte[] rom, int resourceIndex, uint pid, uint trainerId,
        MercurySpriteSelection selection, out byte[] rgba, out MercurySpriteMetadata metadata, int? paletteIndex = null)
    {
        rgba = [];
        metadata = default;
        if (selection.FrameIndex < 0 || selection.PalettePage < 0)
            return false;
        if (resourceIndex < 0 || resourceIndex >= MercuryRomLayout.SpeciesCount)
            return false; // the ROM itself rejects indices >= 1554
        int paletteResourceIndex = paletteIndex ?? resourceIndex;
        if (paletteResourceIndex < 0 || paletteResourceIndex >= MercuryRomLayout.SpeciesCount)
            return false;
        if (!MercuryRomLayout.TryGetFrontPicTable(rom, out uint frontTable))
            return false;
        if (!TryReadEntry(rom, frontTable, resourceIndex, out uint dataPtr, out _))
            return false;
        if (!MercuryRomLayout.IsRomAddress(dataPtr))
            return false;
        if (!MercuryLz77.TryDecompress(rom, dataPtr, out byte[] tiles))
            return false;

        uint paletteSlot = IsShiny(pid, trainerId) ? MercuryRomLayout.ShinyPaletteSlot : MercuryRomLayout.PaletteSlot;
        if (!MercuryRomLayout.TryReadU32(rom, paletteSlot, out uint paletteTable))
            return false;
        if (!MercuryRomLayout.IsRomAddress(paletteTable))
            return false;
        if (!TryReadEntry(rom, paletteTable, paletteResourceIndex, out uint palettePtr, out _))
            return false;
        if (!MercuryRomLayout.IsRomAddress(palettePtr))
            return false;
        if (!MercuryLz77.TryDecompress(rom, palettePtr, out byte[] paletteBytes))
            return false;

        int frameCount = tiles.Length / MercuryRomLayout.SpriteFrameBytes;
        int palettePageCount = paletteBytes.Length / MercuryRomLayout.PaletteBytes;
        if (selection.FrameIndex >= frameCount || selection.PalettePage >= palettePageCount)
            return false;
        MercuryPidSpots.Apply(tiles, resourceIndex, pid);
        int frameOffset = selection.FrameIndex * MercuryRomLayout.SpriteFrameBytes;
        int paletteOffset = selection.PalettePage * MercuryRomLayout.PaletteBytes;

        Span<int> colors = stackalloc int[16];
        for (int i = 0; i < 16; i++)
        {
            int offset = paletteOffset + (2 * i);
            ushort value = (ushort)(paletteBytes[offset] | (paletteBytes[offset + 1] << 8));
            colors[i] = Expand555(value);
        }

        var output = new byte[Width * Height * 4];
        for (int pixel = 0; pixel < Width * Height; pixel++)
        {
            byte packed = tiles[frameOffset + (pixel >> 1)];
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
        metadata = new MercurySpriteMetadata(frameCount, palettePageCount,
            tiles.Length % MercuryRomLayout.SpriteFrameBytes, paletteBytes.Length % MercuryRomLayout.PaletteBytes);
        return true;
    }

    /// <summary>
    /// Item icon from the per-item tiles/palette pointers consumed by 0x08098974. Entry
    /// <c>0x0809899C</c> -> table <c>0x093C8100</c>; <c>base + item*8</c> gives the LZ77 tile block and
    /// <c>+4</c> the LZ77 BGR555 palette. Valid images are 24x24 (3x3 8x8 tiles), index 0 transparent.
    /// </summary>
    public static bool TryRenderItem(byte[] rom, int item, out byte[] rgba)
    {
        rgba = [];
        if (item < 0 || item >= MercuryRomLayout.ItemCount)
            return false;
        if (!MercuryRomLayout.TryReadU32(rom, ItemSpriteTableLiteral, out uint table))
            return false;
        if (!MercuryRomLayout.IsRomAddress(table))
            return false;

        long entry = MercuryRomLayout.ToOffset(table) + (8L * item);
        if (!MercuryRomLayout.TryReadU32Raw(rom, entry, out uint tilesPtr))
            return false;
        if (!MercuryRomLayout.TryReadU32Raw(rom, entry + 4, out uint palettePtr))
            return false;
        if (!MercuryRomLayout.IsRomAddress(tilesPtr) || !MercuryRomLayout.IsRomAddress(palettePtr))
            return false; // the ROM rejects out-of-range pointers; no fallback table
        if (!MercuryLz77.TryDecompress(rom, tilesPtr, out byte[] tiles))
            return false;
        if (tiles.Length < ItemIconTileBytes)
            return false;
        if (!MercuryLz77.TryDecompress(rom, palettePtr, out byte[] palette))
            return false;
        if (palette.Length < ItemIconPaletteBytes)
            return false;

        Span<int> colors = stackalloc int[16];
        for (int i = 0; i < 16; i++)
        {
            ushort value = (ushort)(palette[2 * i] | (palette[(2 * i) + 1] << 8));
            colors[i] = Expand555(value);
        }

        var output = new byte[ItemIconWidth * ItemIconHeight * 4];
        for (int y = 0; y < ItemIconHeight; y++)
        {
            for (int x = 0; x < ItemIconWidth; x++)
            {
                int tile = ((y / 8) * ItemIconTilesPerRow) + (x / 8);
                int offset = (tile * 32) + ((y % 8) * 4) + ((x % 8) / 2);
                byte packed = tiles[offset];
                int colorIndex = (x & 1) == 0 ? packed & 0x0F : packed >> 4;
                int color = colors[colorIndex];
                int dest = ((y * ItemIconWidth) + x) * 4;
                output[dest + 0] = (byte)(color & 0xFF);          // R
                output[dest + 1] = (byte)((color >> 8) & 0xFF);   // G
                output[dest + 2] = (byte)((color >> 16) & 0xFF);  // B
                output[dest + 3] = colorIndex == 0 ? (byte)0 : (byte)0xFF; // index 0 is transparent
            }
        }

        rgba = output;
        return true;
    }

    /// <summary>Type icon drawn by 0x08107D68, with the palette selected by 0x081064BA.</summary>
    public static bool TryRenderType(byte[] rom, byte type, out byte[] rgba, out int width, out int height)
    {
        rgba = [];
        width = height = 0;
        if (type != 9 && !rom.AsSpan(0x1DDA014, 19).Contains(type))
            return false;
        int entry = 0x1CCC35C + (type + 1) * 4;
        width = rom[entry];
        height = rom[entry + 1];
        int tileStart = rom[entry + 2] | (rom[entry + 3] << 8);
        int start = 0xEFC5E4 + tileStart * 32;
        const int palette = 0xEFE9E4;
        rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            // BlitBitmapRectToWindow receives source width 0x80: sixteen 8-pixel tiles per row.
            int offset = start + ((y / 8) * 16 + x / 8) * 32 + (y % 8) * 4 + (x % 8) / 2;
            byte packed = rom[offset];
            int index = (x & 1) == 0 ? packed & 15 : packed >> 4;
            int color = Expand555((ushort)(rom[palette + 2 * index] | (rom[palette + 2 * index + 1] << 8)));
            int dest = (y * width + x) * 4;
            rgba[dest] = (byte)color;
            rgba[dest + 1] = (byte)(color >> 8);
            rgba[dest + 2] = (byte)(color >> 16);
            rgba[dest + 3] = index == 0 ? (byte)0 : (byte)255;
        }
        return true;
    }

    /// <summary>First ball frame from the tables consumed by 0x09D0CC08 and 0x09D0CB48.</summary>
    public static bool TryRenderBall(byte[] rom, byte ball, out byte[] rgba, out int width, out int height)
    {
        rgba = [];
        width = height = 0;
        // 0..26 are the ball item types; 27 is selected by ItemIdToBallId for item 0xFFFF.
        if (ball > 27)
            return false;
        if (!TryReadEntry(rom, 0x09DDF824, ball, out uint sheet, out ushort declaredSize)
            || !TryReadEntry(rom, 0x09DDF744, ball, out uint palette, out _)
            || !MercuryLz77.TryDecompress(rom, sheet, out byte[] tiles)
            || !MercuryLz77.TryDecompress(rom, palette, out byte[] colors)
            || colors.Length < 32 || tiles.Length < declaredSize)
            return false;

        uint template = 0x09DDF4A4u + (uint)ball * 24;
        if (!MercuryRomLayout.TryReadU32(rom, template + 4, out uint oam)
            || !MercuryRomLayout.TryReadU32(rom, oam, out uint attributes)
            || !MercuryRomLayout.TryReadU32(rom, template + 8, out uint animations)
            || !MercuryRomLayout.TryReadU32(rom, animations, out uint firstAnimation)
            || !MercuryRomLayout.TryReadU32(rom, firstAnimation, out uint frame))
            return false;
        int shape = (int)((attributes >> 14) & 3);
        int size = (int)((attributes >> 30) & 3);
        if (shape == 3 || (attributes & 0x2000) != 0 || (frame & 0xFFFF) >= 0xFFFD)
            return false;
        (width, height) = (shape, size) switch
        {
            (0, 0) => (8, 8), (0, 1) => (16, 16), (0, 2) => (32, 32), (0, 3) => (64, 64),
            (1, 0) => (16, 8), (1, 1) => (32, 8), (1, 2) => (32, 16), (1, 3) => (64, 32),
            (2, 0) => (8, 16), (2, 1) => (8, 32), (2, 2) => (16, 32), (2, 3) => (32, 64),
            _ => (0, 0),
        };
        int start = (int)(frame & 0xFFFF) * 32;
        if (start + width * height / 2 > declaredSize)
            return false;
        rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int sx = (frame & (1u << 22)) != 0 ? width - 1 - x : x;
            int sy = (frame & (1u << 23)) != 0 ? height - 1 - y : y;
            int tile = (sy / 8) * (width / 8) + sx / 8;
            byte packed = tiles[start + tile * 32 + (sy % 8) * 4 + (sx % 8) / 2];
            int index = (sx & 1) == 0 ? packed & 15 : packed >> 4;
            int color = Expand555((ushort)(colors[2 * index] | (colors[2 * index + 1] << 8)));
            int offset = (y * width + x) * 4;
            rgba[offset] = (byte)color;
            rgba[offset + 1] = (byte)(color >> 8);
            rgba[offset + 2] = (byte)(color >> 16);
            rgba[offset + 3] = index == 0 ? (byte)0 : (byte)255;
        }
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
