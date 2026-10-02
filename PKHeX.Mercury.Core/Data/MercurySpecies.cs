using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>Primary type id; secondary type is <see cref="Type2"/> (equal to <see cref="Type1"/> when single-typed).</summary>
    public byte Type1 { get; set; }

    /// <summary>Secondary type id.</summary>
    public byte Type2 { get; set; }

    /// <summary>Effort-value yield in the fixed order HP, Atk, Def, Spe, SpA, SpD.</summary>
    public int[] EVYield { get; set; } = new int[6];

    /// <summary>Species egg group 1 (ROM value, 0 = none).</summary>
    public int EggGroup1 { get; set; }

    /// <summary>Species egg group 2 (ROM value, 0 = none).</summary>
    public int EggGroup2 { get; set; }

    /// <summary>Catch rate (0..255).</summary>
    public byte CatchRate { get; set; }

    /// <summary>Egg hatch cycles (ROM value).</summary>
    public byte HatchCycles { get; set; }

    /// <summary>Base experience yield awarded on defeat.</summary>
    public int BaseEXP { get; set; }

    /// <summary>Body color index (ROM value).</summary>
    public int Color { get; set; }

    /// <summary>Safari/flee rate (ROM value).</summary>
    public int EscapeRate { get; set; }

    /// <summary>Three stored ability ids (ability1, ability2, hidden). 0 = none.</summary>
    public int[] Abilities { get; init; } = new int[3];

    /// <summary>Resolved ability-name-pool indices for the three stored abilities.</summary>
    public int[] AbilityNameIndices { get; init; } = new int[3];

    private readonly IReadOnlyList<MercuryLearnMove> _levelUpMoves = Array.AsReadOnly(Array.Empty<MercuryLearnMove>());

    /// <summary>Snapshot of the original paired learn entries; order and duplicate moves are preserved.</summary>
    public IReadOnlyList<MercuryLearnMove> LevelUpMoves
    {
        get => _levelUpMoves;
        init => _levelUpMoves = Array.AsReadOnly(value.ToArray());
    }

    /// <summary>TM/HM move ids learnable by this species.</summary>
    public IReadOnlyList<int> MachineMoves { get; init; } = [];

    /// <summary>Move-tutor move ids learnable by this species.</summary>
    public IReadOnlyList<int> TutorMoves { get; init; } = [];

    /// <summary>False for placeholder/empty species with no ROM data (e.g. NULL learnset slots).</summary>
    public bool HasData { get; init; }
}
