namespace PKHeX.Mercury.Core;

/// <summary>
/// A Mercury item entry. <see cref="Id"/> is the table index; <see cref="EmbeddedId"/> is the item id
/// stored inside the ROM entry. They are intentionally kept separate because the ROM does not make
/// them equal (index 192/193 embed 255, index 375 embeds 0).
/// </summary>
public sealed class MercuryItem
{
    /// <summary>Table index (0..749).</summary>
    public int Id { get; init; }

    /// <summary>Item id embedded in the ROM entry (<c>+0x0E</c>).</summary>
    public int EmbeddedId { get; init; }

    /// <summary>Display name in the current codec; numeric string when no name profile is loaded.</summary>
    public string Name { get; init; } = string.Empty;
}
