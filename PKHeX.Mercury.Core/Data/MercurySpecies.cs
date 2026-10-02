using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

/// <summary>
/// A Mercury internal species/form entry (internal index space, not national dex).
/// </summary>
public sealed class MercurySpecies
{
    /// <summary>Mercury internal species index (not national dex).</summary>
    public int Id { get; init; }

    /// <summary>Display name in the current codec; numeric string when no name profile is loaded.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Base stats in the fixed order HP, Atk, Def, Spe, SpA, SpD.</summary>
    public int[] BaseStats { get; init; } = new int[6];

    /// <summary>Gender ratio byte (0 = male only, 0xFE = female only, 0xFF = genderless).</summary>
    public byte GenderRatio { get; init; }

    /// <summary>Growth-rate index 0..5 into the ROM experience tables.</summary>
    public byte GrowthRate { get; init; }

    public byte BaseFriendship { get; init; }

    /// <summary>Three stored ability ids (ability1, ability2, hidden). 0 = none.</summary>
    public int[] Abilities { get; init; } = new int[3];

    /// <summary>Resolved ability-name-pool indices for the three stored abilities.</summary>
    public int[] AbilityNameIndices { get; init; } = new int[3];

    public IReadOnlyList<MercuryLearnMove> LevelUpMoves { get; init; } = [];

    /// <summary>TM/HM move ids learnable by this species.</summary>
    public IReadOnlyList<int> MachineMoves { get; init; } = [];

    /// <summary>Move-tutor move ids learnable by this species.</summary>
    public IReadOnlyList<int> TutorMoves { get; init; } = [];

    /// <summary>False for placeholder/empty species with no ROM data (e.g. NULL learnset slots).</summary>
    public bool HasData { get; init; }
}
