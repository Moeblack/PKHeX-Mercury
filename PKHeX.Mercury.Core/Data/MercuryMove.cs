namespace PKHeX.Mercury.Core;

/// <summary>A Mercury move definition (internal move index space).</summary>
public sealed class MercuryMove
{
    public int Id { get; init; }

    /// <summary>Display name in the current codec; numeric string when no name profile is loaded.</summary>
    public string Name { get; init; } = string.Empty;

    public byte Type { get; init; }

    public byte Power { get; init; }

    public byte PP { get; init; }

    public byte Accuracy { get; init; }

    /// <summary>Signed priority byte (read as s8 from the ROM battle-move struct).</summary>
    public sbyte Priority { get; init; }
}
