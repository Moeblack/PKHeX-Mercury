namespace PKHeX.Mercury.Core;

/// <summary>
/// Independent zero-based selections within a sprite's decompressed tiles and palette.
/// The default selects frame 0 and palette page 0; no animation or frame/page pairing is implied.
/// Normal versus shiny is selected separately by PID and trainer ID.
/// </summary>
public readonly record struct MercurySpriteSelection(int FrameIndex, int PalettePage);
