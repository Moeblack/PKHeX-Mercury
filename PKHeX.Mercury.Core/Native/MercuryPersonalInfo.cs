using System;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Native <see cref="PersonalInfo"/> view of one Mercury internal species/form entry.
/// <para>
/// Mercury stores forms (mega/gigantamax/regional) as separate internal species indices, so
/// <see cref="FormCount"/> is 1 and <see cref="FormStatsIndex"/> stays 0. All numeric facts come from the
/// ROM-derived <see cref="MercurySpecies"/> record; nothing is filled with retail national-dex data.
/// </para>
/// </summary>
public sealed class MercuryPersonalInfo : PersonalInfo, IPersonalAbility12H
{
    private readonly MercurySpecies _species;

    public MercuryPersonalInfo(MercurySpecies species)
    {
        _species = species ?? throw new ArgumentNullException(nameof(species));
        HP = species.BaseStats.Length > 0 ? species.BaseStats[0] : 0;
        ATK = species.BaseStats.Length > 1 ? species.BaseStats[1] : 0;
        DEF = species.BaseStats.Length > 2 ? species.BaseStats[2] : 0;
        SPE = species.BaseStats.Length > 3 ? species.BaseStats[3] : 0;
        SPA = species.BaseStats.Length > 4 ? species.BaseStats[4] : 0;
        SPD = species.BaseStats.Length > 5 ? species.BaseStats[5] : 0;

        EV_HP = species.EVYield.Length > 0 ? species.EVYield[0] : 0;
        EV_ATK = species.EVYield.Length > 1 ? species.EVYield[1] : 0;
        EV_DEF = species.EVYield.Length > 2 ? species.EVYield[2] : 0;
        EV_SPE = species.EVYield.Length > 3 ? species.EVYield[3] : 0;
        EV_SPA = species.EVYield.Length > 4 ? species.EVYield[4] : 0;
        EV_SPD = species.EVYield.Length > 5 ? species.EVYield[5] : 0;

        Type1 = species.Type1;
        Type2 = species.Type2;
        EggGroup1 = species.EggGroup1;
        EggGroup2 = species.EggGroup2;
        CatchRate = species.CatchRate;
        HatchCycles = species.HatchCycles;
        BaseFriendship = species.BaseFriendship;
        EXPGrowth = species.GrowthRate;
        EscapeRate = species.EscapeRate;
        BaseEXP = species.BaseEXP;
        Color = species.Color;
        Gender = species.GenderRatio;
        Ability1 = species.Abilities.Length > 0 ? species.Abilities[0] : 0;
        Ability2 = species.Abilities.Length > 1 ? species.Abilities[1] : 0;
        AbilityH = species.Abilities.Length > 2 ? species.Abilities[2] : 0;

        AbilityNameIndex1 = species.AbilityNameIndices.Length > 0 ? species.AbilityNameIndices[0] : 0;
        AbilityNameIndex2 = species.AbilityNameIndices.Length > 1 ? species.AbilityNameIndices[1] : 0;
        AbilityNameIndex3 = species.AbilityNameIndices.Length > 2 ? species.AbilityNameIndices[2] : 0;
    }

    /// <summary>True when the ROM table has real base stats for this entry.</summary>
    public bool HasData => _species.HasData;

    public int Ability1 { get; set; }
    public int Ability2 { get; set; }
    public int AbilityH { get; set; }

    /// <summary>Resolved ability-name-pool index for slot 0 (stored id stays in <see cref="GetAbilityAtIndex"/>).</summary>
    public int AbilityNameIndex1 { get; }
    public int AbilityNameIndex2 { get; }
    public int AbilityNameIndex3 { get; }

    public override byte[] Write()
    {
        var data = new byte[28];
        data[0] = (byte)HP;
        data[1] = (byte)ATK;
        data[2] = (byte)DEF;
        data[3] = (byte)SPE;
        data[4] = (byte)SPA;
        data[5] = (byte)SPD;
        data[6] = Type1;
        data[7] = Type2;
        data[8] = CatchRate;
        data[9] = (byte)BaseEXP;
        data[0x0A] = (byte)((EV_HP & 3) | ((EV_ATK & 3) << 2) | ((EV_DEF & 3) << 4) | ((EV_SPE & 3) << 6));
        data[0x0B] = (byte)((EV_SPA & 3) | ((EV_SPD & 3) << 2));
        // 0x0C/0x0E held-item fields are not part of the PersonalInfo editing surface; left zero.
        data[0x10] = Gender;
        data[0x11] = HatchCycles;
        data[0x12] = BaseFriendship;
        data[0x13] = EXPGrowth;
        data[0x14] = (byte)EggGroup1;
        data[0x15] = (byte)EggGroup2;
        data[0x16] = (byte)Ability1;
        data[0x17] = (byte)Ability2;
        data[0x18] = (byte)EscapeRate;
        data[0x19] = (byte)(Color & 0x7F);
        data[0x1A] = (byte)AbilityH;
        return data;
    }

    public override int HP { get; set; }
    public override int ATK { get; set; }
    public override int DEF { get; set; }
    public override int SPE { get; set; }
    public override int SPA { get; set; }
    public override int SPD { get; set; }
    public override int EV_HP { get; set; }
    public override int EV_ATK { get; set; }
    public override int EV_DEF { get; set; }
    public override int EV_SPE { get; set; }
    public override int EV_SPA { get; set; }
    public override int EV_SPD { get; set; }
    public override byte Type1 { get; set; }
    public override byte Type2 { get; set; }
    public override int EggGroup1 { get; set; }
    public override int EggGroup2 { get; set; }
    public override byte CatchRate { get; set; }
    public override byte Gender { get; set; }
    public override byte HatchCycles { get; set; }
    public override byte BaseFriendship { get; set; }
    public override byte EXPGrowth { get; set; }
    public override int EscapeRate { get; set; }
    public override int BaseEXP { get; set; }
    public override int Color { get; set; }

    /// <summary>Number of distinct usable ability slots for the species (1, 2 or 3).</summary>
    public override int AbilityCount
    {
        get
        {
            if (AbilityH != 0 && AbilityH != Ability1 && AbilityH != Ability2)
                return 3;
            if (Ability2 != 0 && Ability2 != Ability1)
                return 2;
            return 1;
        }
    }

    public override int GetAbilityAtIndex(int abilityIndex) => abilityIndex switch
    {
        0 => Ability1,
        1 => Ability2,
        2 => AbilityH,
        _ => 0,
    };

    public override int GetIndexOfAbility(int abilityID)
    {
        if (abilityID == Ability1)
            return 0;
        if (abilityID == Ability2 && Ability2 != 0)
            return 1;
        if (abilityID == AbilityH && AbilityH != 0)
            return 2;
        return -1;
    }
}

/// <summary>
/// Native <see cref="IPersonalTable"/> over the Mercury internal species table. Out-of-range lookups return a
/// shared empty entry (all-zero), mirroring the upstream contract for tables with sparse entries.
/// </summary>
public sealed class MercuryPersonalTable : IPersonalTable
{
    private readonly MercuryPersonalInfo[] _entries;
    private readonly bool[] _present;
    private static readonly MercuryPersonalInfo Empty = new(new MercurySpecies { Id = -1, HasData = false });

    public MercuryPersonalTable(MercuryGameData gameData)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        int count = gameData.Species.Count;
        _entries = new MercuryPersonalInfo[count];
        _present = new bool[count];
        for (int i = 0; i < count; i++)
        {
            _entries[i] = new MercuryPersonalInfo(gameData.Species[i]);
            _present[i] = gameData.Species[i].HasData;
        }
        MaxSpeciesID = (ushort)Math.Max(0, count - 1);
    }

    public ushort MaxSpeciesID { get; }

    public int Count => _entries.Length;

    public PersonalInfo this[int index] => (uint)index < (uint)_entries.Length ? _entries[index] : Empty;

    public PersonalInfo this[ushort species, byte form] => this[(int)species];

    public int GetFormIndex(ushort species, byte form) => species;

    public PersonalInfo GetFormEntry(ushort species, byte form) => this[species];

    public bool IsSpeciesInGame(ushort species)
        => (uint)species < (uint)_present.Length && _present[species];

    public bool IsPresentInGame(ushort species, byte form) => IsSpeciesInGame(species);
}
