namespace PKHeX.Mercury.Core;

/// <summary>
/// Complete-frame/page counts and trailing byte counts from the full decompressed tile block
/// (2048 bytes per frame) and PID/trainer-ID-selected palette block (32 bytes per page).
/// Trailing bytes do not form an additional frame/page. These counts do not define an animation.
/// </summary>
public readonly record struct MercurySpriteMetadata(
    int FrameCount, int PalettePageCount, int TrailingTileBytes, int TrailingPaletteBytes);
