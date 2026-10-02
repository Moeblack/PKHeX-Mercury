using System;

namespace PKHeX.Mercury.Core;

/// <summary>
/// A Mercury 1.1 Pokémon record.
/// <para>
/// The canonical data is the 58-byte boxed record proven by ROM 0x09D54A2C (field read),
/// 0x09D54CC8 (field write), 0x09D548D0 (box -> expanded) and 0x09D54AFC (expanded -> box).
/// The 100-byte party record is the 80-byte expanded form followed by a 20-byte party tail.
/// </para>
/// <para>
/// Party-origin instances keep the original 100-byte template plus an initial canonical snapshot so that
/// unedited bytes (expanded 0x1C/0x1D/0x2B, contest, ribbons, mail, ...) survive a round trip; only fields
/// that actually changed are mapped back. See docs/mercury-save-format.md.
/// </para>
/// </summary>
public sealed class MercuryPokemon
{
    /// <summary>Size of one boxed record (.m3box).</summary>
    public const int BoxSize = MercurySaveLayout.BoxMonSize;

    /// <summary>Size of one party record.</summary>
    public const int PartySize = MercurySaveLayout.PartyMonSize;

    private readonly byte[] _box;             // 58-byte canonical record
    private readonly byte[] _snapshot;        // canonical record as constructed, for change detection
    private byte[]? _partyTemplate;           // original 100-byte record (party-origin only)
    private readonly byte[] _partyTail;       // 20 bytes (0x50-0x63 of the party record)
    private readonly byte[] _partyExtra;      // 6 contest (0x3E) + 4 ribbons (0x4C) of the expanded form
    private readonly byte[] _partyPp;         // 4 current PP (0x34 of the expanded form)

    private bool _isPartyForm;

    private MercuryPokemon(byte[] box, byte[] snapshot, byte[]? partyTemplate, byte[] partyTail, byte[] partyExtra, byte[] partyPp, bool partyForm)
    {
        _box = box;
        _snapshot = snapshot;
        _partyTemplate = partyTemplate;
        _partyTail = partyTail;
        _partyExtra = partyExtra;
        _partyPp = partyPp;
        _isPartyForm = partyForm;
    }

    /// <summary>An empty (all zero) Pokémon.</summary>
    public static MercuryPokemon Empty => new(new byte[BoxSize], new byte[BoxSize], null, new byte[PartyTailLength], new byte[10], new byte[4], false);

    private static int PartyTailLength => MercurySaveLayout.PartyTailLength;

    /// <summary>Create a Pokémon from an exact 58-byte boxed record (<c>.m3box</c>); an 80-byte retail .pk3 is rejected.</summary>
    public static MercuryPokemon FromBox(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != BoxSize)
            throw new ArgumentException($"Box record must be exactly {BoxSize} bytes (got {data.Length}); retail .pk3 is not a Mercury record.", nameof(data));

        var box = (byte[])data.Clone();
        return new MercuryPokemon(box, (byte[])box.Clone(), null, new byte[PartyTailLength], new byte[10], new byte[4], false);
    }

    /// <summary>Create a Pokémon from an exact 100-byte party record.</summary>
    public static MercuryPokemon FromParty(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != PartySize)
            throw new ArgumentException($"Party record must be exactly {PartySize} bytes (got {data.Length}).", nameof(data));

        var box = new byte[BoxSize];
        Compress(data, box);

        var tail = new byte[PartyTailLength];
        Buffer.BlockCopy(data, MercurySaveLayout.PartyTail, tail, 0, PartyTailLength);

        var extra = new byte[10];
        Buffer.BlockCopy(data, MercurySaveLayout.ExContest, extra, 0, 6);
        Buffer.BlockCopy(data, MercurySaveLayout.ExRibbons, extra, 6, 4);

        var pp = new byte[4];
        Buffer.BlockCopy(data, MercurySaveLayout.ExCurrentPp, pp, 0, 4);

        return new MercuryPokemon(box, (byte[])box.Clone(), (byte[])data.Clone(), tail, extra, pp, true);
    }

    /// <summary>Create a minimal boxed Pokémon of the given internal species id.</summary>
    public static MercuryPokemon Create(ushort species, uint trainerId)
    {
        var box = new byte[BoxSize];
        WriteU16(box, MercurySaveLayout.BoxSpecies, species);
        WriteU32(box, MercurySaveLayout.BoxOtId, trainerId);
        box[MercurySaveLayout.BoxFlags] = MercurySaveLayout.FlagHasSpecies;
        box[MercurySaveLayout.BoxLanguage] = 2; // English default; caller may change.
        return new MercuryPokemon(box, (byte[])box.Clone(), null, new byte[PartyTailLength], new byte[10], new byte[4], false);
    }

    /// <summary>True when the canonical record is entirely zero.</summary>
    public bool IsEmpty
    {
        get
        {
            foreach (var b in _box)
            {
                if (b != 0)
                    return false;
            }
            return true;
        }
    }

    /// <summary>True when this instance still carries party-tail/template data (from <see cref="FromParty"/> or <see cref="ToParty"/>).</summary>
    public bool IsPartyForm => _isPartyForm;

    public MercuryPokemon Clone() => new(
        (byte[])_box.Clone(),
        (byte[])_snapshot.Clone(),
        _partyTemplate is null ? null : (byte[])_partyTemplate.Clone(),
        (byte[])_partyTail.Clone(),
        (byte[])_partyExtra.Clone(),
        (byte[])_partyPp.Clone(),
        _isPartyForm);

    /// <summary>Export the canonical 58-byte boxed record.</summary>
    public byte[] ToBoxBytes() => (byte[])_box.Clone();

    /// <summary>
    /// Export a 100-byte party record. Party-origin instances rebuild from the original template and only map
    /// back fields whose canonical value changed, so unrelated bytes are preserved; box-origin instances use the
    /// ROM 0x09D548D0 expansion.
    /// </summary>
    public byte[] ToPartyBytes()
    {
        var dest = _partyTemplate is null ? RomExpanded(PartySize) : LosslessExpanded();
        Buffer.BlockCopy(_partyExtra, 0, dest, MercurySaveLayout.ExContest, 6);
        Buffer.BlockCopy(_partyExtra, 6, dest, MercurySaveLayout.ExRibbons, 4);
        Buffer.BlockCopy(_partyPp, 0, dest, MercurySaveLayout.ExCurrentPp, 4);
        Buffer.BlockCopy(_partyTail, 0, dest, MercurySaveLayout.PartyTail, PartyTailLength);
        return dest;
    }

    /// <summary>Convert this instance to box form, dropping party-only data.</summary>
    public void ToBox()
    {
        Array.Clear(_partyTail);
        Array.Clear(_partyExtra);
        Array.Clear(_partyPp);
        _partyTemplate = null;
        _isPartyForm = false;
    }

    /// <summary>Convert this instance to party form and compute the party stats from the supplied base stats and stat mode.</summary>
    public void ToParty(int[] baseStats, byte level, int mode = 0)
    {
        RecalculatePartyStats(baseStats, level, mode);
        _isPartyForm = true;
    }

    // --- scalar fields ----------------------------------------------------

    public uint PID
    {
        get => ReadU32(_box, MercurySaveLayout.BoxPid);
        set => WriteU32(_box, MercurySaveLayout.BoxPid, value);
    }

    public uint ID32
    {
        get => ReadU32(_box, MercurySaveLayout.BoxOtId);
        set => WriteU32(_box, MercurySaveLayout.BoxOtId, value);
    }

    public ushort Species
    {
        get => ReadU16(_box, MercurySaveLayout.BoxSpecies);
        set
        {
            ushort current = ReadU16(_box, MercurySaveLayout.BoxSpecies);
            if (current == value)
                return; // ordinary edit: keep every other byte, including the type-override field
            WriteU16(_box, MercurySaveLayout.BoxSpecies, value);
            // The ROM recomputes the type override (0x09D60F2C) only when it decodes to 0, so clear the
            // 5-bit field on an actual species change to avoid a stale type on the new species.
            _box[MercurySaveLayout.BoxFlags] &= MercurySaveLayout.FlagBits;
        }
    }

    public ushort HeldItem
    {
        get => ReadU16(_box, MercurySaveLayout.BoxHeldItem);
        set => WriteU16(_box, MercurySaveLayout.BoxHeldItem, value);
    }

    public uint Experience
    {
        get => ReadU32(_box, MercurySaveLayout.BoxExperience);
        set => WriteU32(_box, MercurySaveLayout.BoxExperience, value);
    }

    public byte Language
    {
        get => _box[MercurySaveLayout.BoxLanguage];
        set => _box[MercurySaveLayout.BoxLanguage] = value;
    }

    public byte Friendship
    {
        get => _box[MercurySaveLayout.BoxFriendship];
        set => _box[MercurySaveLayout.BoxFriendship] = value;
    }

    public byte Pokerus
    {
        get => _box[MercurySaveLayout.BoxPokerus];
        set => _box[MercurySaveLayout.BoxPokerus] = value;
    }

    public byte Markings
    {
        get => _box[MercurySaveLayout.BoxMarkings];
        set => _box[MercurySaveLayout.BoxMarkings] = value;
    }

    public byte MetLocation
    {
        get => _box[MercurySaveLayout.BoxMetLocation];
        set => _box[MercurySaveLayout.BoxMetLocation] = value;
    }

    /// <summary>
    /// Ball: full byte at box 0x26 (expanded 0x2A). ROM field 0x26 resolves to 0x09CCE574 which reads
    /// <c>Growth[0xA]</c> (0x09D069A0); origins bits 11-14 are used for other data and are preserved untouched.
    /// </summary>
    public byte Ball
    {
        get => _box[MercurySaveLayout.BoxBall];
        set => _box[MercurySaveLayout.BoxBall] = value;
    }

    public byte MetLevel
    {
        get => (byte)(Origins & 0x7F);
        set => Origins = (ushort)((Origins & ~0x7F) | (value & 0x7F));
    }

    public byte MetGame
    {
        get => (byte)((Origins >> 7) & 0xF);
        set => Origins = (ushort)((Origins & ~(0xF << 7)) | ((value & 0xF) << 7));
    }

    public byte OTGender
    {
        get => (byte)((Origins >> 15) & 1);
        set => Origins = (ushort)((Origins & ~(1 << 15)) | ((value & 1) << 15));
    }

    private ushort Origins
    {
        get => ReadU16(_box, MercurySaveLayout.BoxOrigins);
        set => WriteU16(_box, MercurySaveLayout.BoxOrigins, value);
    }

    /// <summary>
    /// Egg flag. Getter reads IV-word bit 30 (ROM MON_DATA_IS_EGG field 0x2D -> 0x08040154); setter keeps the
    /// header flags bit 2 and IV-word bit 30 in sync and never touches the hidden-ability bit 31.
    /// </summary>
    public bool IsEgg
    {
        get => (ReadU32(_box, MercurySaveLayout.BoxIvs) & MercurySaveLayout.IvEggFlag) != 0;
        set
        {
            var ivs = ReadU32(_box, MercurySaveLayout.BoxIvs);
            ivs = value ? ivs | MercurySaveLayout.IvEggFlag : ivs & ~MercurySaveLayout.IvEggFlag;
            WriteU32(_box, MercurySaveLayout.BoxIvs, ivs);

            var flags = _box[MercurySaveLayout.BoxFlags];
            _box[MercurySaveLayout.BoxFlags] = (byte)(value
                ? flags | MercurySaveLayout.FlagIsEgg
                : flags & ~MercurySaveLayout.FlagIsEgg);
        }
    }

    /// <summary>
    /// Hidden ability marker = bit 31 of the IV word (ROM GetAbilityBySpecies 0x09D07C68 reads <c>mon[0x4B]</c> bit 7).
    /// </summary>
    public bool HiddenAbility
    {
        get => (ReadU32(_box, MercurySaveLayout.BoxIvs) & MercurySaveLayout.IvHiddenAbilityFlag) != 0;
        set
        {
            var ivs = ReadU32(_box, MercurySaveLayout.BoxIvs);
            ivs = value ? ivs | MercurySaveLayout.IvHiddenAbilityFlag : ivs & ~MercurySaveLayout.IvHiddenAbilityFlag;
            WriteU32(_box, MercurySaveLayout.BoxIvs, ivs);
        }
    }

    /// <summary>
    /// Ability slot: 0 = first ability, 1 = second ability, 2 = hidden ability.
    /// Effective ROM rule (0x09D07C68): hidden when the IV-word bit 31 is set and the species has a hidden ability,
    /// otherwise second ability when PID bit 0 is set and the species has a second ability, otherwise the first ability.
    /// This property only exposes the storage marker; it cannot know whether the species actually has a second
    /// or hidden ability, so the effective slot is decided by the data/UI layer from the species ability table.
    /// </summary>
    public int AbilitySlot
    {
        get => HiddenAbility ? 2 : (int)(PID & 1);
        set
        {
            switch (value)
            {
                case 0:
                case 1:
                    HiddenAbility = false;
                    PID = (PID & ~1u) | (uint)value;
                    break;
                case 2:
                    HiddenAbility = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Ability slot must be 0, 1 or 2.");
            }
        }
    }

    /// <summary>
    /// Raw 5-bit type-override field (0x13 bits 3-7; the expanded form stores it at 0x1E).
    /// Read-only: ordinary edits preserve it, an explicit species change clears it so the ROM derives the new type.
    /// </summary>
    public int TypeOverride => _box[MercurySaveLayout.BoxFlags] >> 3;

    /// <summary>Nature index 0-24 derived from <c>PID % 25</c>.</summary>
    public int Nature => (int)(PID % 25);

    /// <summary>Gen3 shiny test on four 16-bit halves: <c>(pidLo ^ pidHi ^ tid ^ sid) &lt; 8</c>.</summary>
    public bool IsShiny
    {
        get
        {
            uint pid = PID;
            uint id = ID32;
            uint x = ((pid >> 16) ^ (pid & 0xFFFF) ^ (id >> 16) ^ (id & 0xFFFF)) & 0xFFFF;
            return x < 8;
        }
    }

    // --- arrays -----------------------------------------------------------

    public byte[] NicknameBytes
    {
        get => Slice(_box, MercurySaveLayout.BoxNickname, MercurySaveLayout.BoxNicknameLength);
        set => SetFixed(_box, MercurySaveLayout.BoxNickname, MercurySaveLayout.BoxNicknameLength, value);
    }

    public byte[] OTNameBytes
    {
        get => Slice(_box, MercurySaveLayout.BoxOtName, MercurySaveLayout.BoxOtNameLength);
        set => SetFixed(_box, MercurySaveLayout.BoxOtName, MercurySaveLayout.BoxOtNameLength, value);
    }

    /// <summary>Four 10-bit move ids packed at 0x27-0x2B.</summary>
    public ushort[] Moves
    {
        get
        {
            ulong bits = ReadPackedMoves(_box);
            var moves = new ushort[4];
            for (int i = 0; i < 4; i++)
                moves[i] = (ushort)((bits >> (10 * i)) & 0x3FF);
            return moves;
        }
        set
        {
            if (value is null || value.Length != 4)
                throw new ArgumentException("Moves must have exactly 4 entries.", nameof(value));
            if (value.Any(move => move > 0x3FF))
                throw new ArgumentOutOfRangeException(nameof(value), "Move IDs must fit 10 bits (0..1023).");
            WritePackedMoves(_box, value);
        }
    }

    // Order: HP, Atk, Def, Spe, SpA, SpD (matches the ROM substructure order).
    public byte[] IVs
    {
        get
        {
            uint word = ReadU32(_box, MercurySaveLayout.BoxIvs);
            var ivs = new byte[6];
            for (int i = 0; i < 6; i++)
                ivs[i] = (byte)((word >> (5 * i)) & 0x1F);
            return ivs;
        }
        set
        {
            if (value is null || value.Length != 6)
                throw new ArgumentException("IVs must have exactly 6 entries.", nameof(value));
            if (value.Any(iv => iv > 31))
                throw new ArgumentOutOfRangeException(nameof(value), "IVs must be 0..31.");
            uint word = ReadU32(_box, MercurySaveLayout.BoxIvs) & 0xC000_0000u; // keep bits 30-31
            for (int i = 0; i < 6; i++)
                word |= (uint)(value[i] & 0x1F) << (5 * i);
            WriteU32(_box, MercurySaveLayout.BoxIvs, word);
        }
    }

    public byte[] EVs
    {
        get => Slice(_box, MercurySaveLayout.BoxEvs, 6);
        set
        {
            if (value is null || value.Length != 6)
                throw new ArgumentException("EVs must have exactly 6 entries.", nameof(value));
            for (int i = 0; i < 6; i++)
                _box[MercurySaveLayout.BoxEvs + i] = value[i];
        }
    }

    /// <summary>Four 2-bit PP-up counts (packed byte at 0x24).</summary>
    public byte[] PPUps
    {
        get
        {
            byte packed = _box[MercurySaveLayout.BoxPpUps];
            var ups = new byte[4];
            for (int i = 0; i < 4; i++)
                ups[i] = (byte)((packed >> (2 * i)) & 0x3);
            return ups;
        }
        set
        {
            if (value is null || value.Length != 4)
                throw new ArgumentException("PPUps must have exactly 4 entries.", nameof(value));
            if (value.Any(ppUp => ppUp > 3))
                throw new ArgumentOutOfRangeException(nameof(value), "PP-up counts must be 0..3.");
            byte packed = 0;
            for (int i = 0; i < 4; i++)
                packed |= (byte)((value[i] & 0x3) << (2 * i));
            _box[MercurySaveLayout.BoxPpUps] = packed;
        }
    }

    /// <summary>Current PP stored in the party expanded form. Set before <see cref="ToPartyBytes"/> for box-origin records.</summary>
    public byte[] PartyCurrentPp
    {
        get => (byte[])_partyPp.Clone();
        set
        {
            if (value is null || value.Length != 4)
                throw new ArgumentException("PartyCurrentPp must have exactly 4 entries.", nameof(value));
            Buffer.BlockCopy(value, 0, _partyPp, 0, 4);
        }
    }

    /// <summary>Six party stats in order (max HP, Atk, Def, Spe, SpA, SpD).</summary>
    public ushort[] PartyStats
    {
        get
        {
            var stats = new ushort[6];
            stats[0] = ReadU16(_partyTail, MercurySaveLayout.PartyMaxHp - MercurySaveLayout.PartyTail);
            stats[1] = ReadU16(_partyTail, MercurySaveLayout.PartyAtk - MercurySaveLayout.PartyTail);
            stats[2] = ReadU16(_partyTail, MercurySaveLayout.PartyDef - MercurySaveLayout.PartyTail);
            stats[3] = ReadU16(_partyTail, MercurySaveLayout.PartySpe - MercurySaveLayout.PartyTail);
            stats[4] = ReadU16(_partyTail, MercurySaveLayout.PartySpA - MercurySaveLayout.PartyTail);
            stats[5] = ReadU16(_partyTail, MercurySaveLayout.PartySpD - MercurySaveLayout.PartyTail);
            return stats;
        }
    }

    public byte PartyLevel
    {
        get => _partyTail[MercurySaveLayout.PartyLevel - MercurySaveLayout.PartyTail];
        set => _partyTail[MercurySaveLayout.PartyLevel - MercurySaveLayout.PartyTail] = value;
    }

    // --- derived helpers --------------------------------------------------

    /// <summary>
    /// Derive the gender from the PID low byte and a species gender ratio.
    /// Single-gender constants are the ROM values: 0x00 = male only, 0xFE = female only, 0xFF = genderless.
    /// </summary>
    public int GetGender(byte genderRatio) => GenderFromLow((byte)(PID & 0xFF), genderRatio);

    private static int GenderFromLow(byte low, byte genderRatio)
    {
        if (genderRatio == 0xFF)
            return 2; // genderless
        if (genderRatio == 0x00)
            return 0; // male only
        if (genderRatio == 0xFE)
            return 1; // female only
        return (low < genderRatio) ? 1 : 0;
    }

    /// <summary>
    /// Recompute the six party stats and the current HP from base stats (HP, Atk, Def, Spe, SpA, SpD), the level
    /// and the save stat-scaling mode (0 = normal, 11/12/13 = ROM modes 0x09D070F0; see docs/mercury-save-format.md).
    /// Status, mail, PP and every other party byte are preserved. Shedinja (species 0x12F) always gets max HP 1.
    /// </summary>
    public void RecalculatePartyStats(int[] baseStats, byte level, int mode = 0)
    {
        ArgumentNullException.ThrowIfNull(baseStats);
        if (baseStats.Length < 6)
            throw new ArgumentException("baseStats must have at least 6 entries (HP, Atk, Def, Spe, SpA, SpD).", nameof(baseStats));
        if (mode is not (0 or 11 or 12 or 13))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Stat mode must be 0, 11, 12 or 13.");

        var ivs = IVs;
        var evs = EVs;
        int nature = Nature;
        int up = NatureUp[nature];
        int down = NatureDown[nature];

        int hpBase = baseStats[0];
        int bst = 0;
        for (int i = 0; i < 6; i++)
            bst += baseStats[i];

        bool shedinja = Species == 0x12F;
        bool scaled = mode == 12 && bst <= 350; // ROM gate 0x09D07344 (BST <= 350) selects the 4*base path
        int mode11Denominator = bst - hpBase;

        var stats = new ushort[6];
        for (int i = 0; i < 6; i++)
        {
            int @base = baseStats[i];
            int iv = ivs[i];
            int ev = evs[i] / 4;

            if (i == 0)
            {
                int value;
                if (shedinja)
                {
                    value = 1;
                }
                else
                {
                    int hpBase2 = mode switch
                    {
                        13 => 200,                                    // effective base 100 (2 * 100)
                        12 when scaled => 2 * ((hpBase * 2) & 0xFF),  // 0x09D071A8..AA
                        _ => 2 * hpBase,
                    };
                    value = ((hpBase2 + iv + ev) * level / 100) + level + 10;
                }
                stats[i] = (ushort)Math.Clamp(value, 0, 0xFFFF);
                continue;
            }

            int stat;
            if (mode == 13)
            {
                stat = ((200 + iv + ev) * level / 100) + 5; // 0x09D072E2
            }
            else if (scaled)
            {
                stat = ((4 * @base + iv + ev) * level / 100) + 5; // 0x09D07350
            }
            else if (mode == 11 && !shedinja && mode11Denominator != 0)
            {
                int effective = (int)Math.Min(255, (long)@base * (600 - hpBase) / mode11Denominator); // 0x09D07212..22
                stat = ((2 * effective + iv + ev) * level / 100) + 5;
            }
            else
            {
                stat = ((2 * @base + iv + ev) * level / 100) + 5; // 0x09D073A4
            }

            if (up == i - 1)
                stat = stat * 110 / 100;
            else if (down == i - 1)
                stat = stat * 90 / 100;
            stats[i] = (ushort)Math.Clamp(stat, 0, 0xFFFF);
        }

        // Current HP update mirrors ROM 0x09D073F6..0x09D07438.
        ushort oldHp = ReadU16(_partyTail, MercurySaveLayout.PartyHp - MercurySaveLayout.PartyTail);
        ushort oldMax = ReadU16(_partyTail, MercurySaveLayout.PartyMaxHp - MercurySaveLayout.PartyTail);
        int newMax = stats[0];
        int newHp;
        if (oldHp == 0 && oldMax == 0)
            newHp = newMax;
        else if (oldHp == 0)
            newHp = 0;
        else
            newHp = newMax >= oldMax ? oldHp + (newMax - oldMax) : oldHp;
        newHp = Math.Clamp(newHp, 0, newMax);

        WriteU16(_partyTail, MercurySaveLayout.PartyHp - MercurySaveLayout.PartyTail, (ushort)newHp);
        WriteU16(_partyTail, MercurySaveLayout.PartyMaxHp - MercurySaveLayout.PartyTail, stats[0]);
        WriteU16(_partyTail, MercurySaveLayout.PartyAtk - MercurySaveLayout.PartyTail, stats[1]);
        WriteU16(_partyTail, MercurySaveLayout.PartyDef - MercurySaveLayout.PartyTail, stats[2]);
        WriteU16(_partyTail, MercurySaveLayout.PartySpe - MercurySaveLayout.PartyTail, stats[3]);
        WriteU16(_partyTail, MercurySaveLayout.PartySpA - MercurySaveLayout.PartyTail, stats[4]);
        WriteU16(_partyTail, MercurySaveLayout.PartySpD - MercurySaveLayout.PartyTail, stats[5]);
        PartyLevel = level;
    }

    /// <summary>
    /// Find a PID satisfying the requested nature (%25), gender (per gender ratio), shiny state and ability slot.
    /// Low 16 bits are enumerated 0..65535 and filtered by gender/ability; the high half is constructed from the
    /// nature residue (non-shiny) or from the shiny xor (shiny), so the search is a complete constraint solve.
    /// The current PID is kept when it already satisfies everything.
    /// Throws <see cref="ArgumentException"/> when the constraints are contradictory.
    /// </summary>
    public void SetPersonality(int nature, int gender, byte genderRatio, bool shiny, int abilitySlot)
    {
        if (nature is < 0 or > 24)
            throw new ArgumentOutOfRangeException(nameof(nature), nature, "Nature must be 0-24.");
        if (gender is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(gender), gender, "Gender must be 0 (male), 1 (female) or 2 (genderless).");
        if (abilitySlot is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(abilitySlot), abilitySlot, "Ability slot must be 0, 1 or 2.");

        if (genderRatio == 0xFF && gender != 2)
            throw new ArgumentException("Species is genderless (0xFF), gender must be 2.", nameof(gender));
        if (genderRatio == 0x00 && gender != 0)
            throw new ArgumentException("Species is male-only (0x00), gender must be 0.", nameof(gender));
        if (genderRatio == 0xFE && gender != 1)
            throw new ArgumentException("Species is female-only (0xFE), gender must be 1.", nameof(gender));
        if (gender == 2 && genderRatio != 0xFF)
            throw new ArgumentException("Genderless is only possible for species with gender ratio 0xFF.", nameof(gender));

        if (IsPidAcceptable(PID, nature, gender, genderRatio, shiny, abilitySlot))
            return;

        bool constrainAbility = abilitySlot != 2;
        uint wantAbility = (uint)abilitySlot;
        uint id = ID32;
        uint tid = id & 0xFFFF;
        uint sid = id >> 16;

        for (uint low = 0; low <= 0xFFFF; low++)
        {
            if (constrainAbility && (low & 1) != wantAbility)
                continue;
            if (GenderFromLow((byte)low, genderRatio) != gender)
                continue;

            if (shiny)
            {
                uint baseXor = (low ^ tid ^ sid) & 0xFFFF;
                for (uint n = 0; n < 8; n++)
                {
                    uint hi = (baseXor ^ n) & 0xFFFF;
                    uint pid = (hi << 16) | low;
                    if (pid % 25 != (uint)nature)
                        continue;
                    Accept(pid, abilitySlot);
                    return;
                }
            }
            else
            {
                // 65536 % 25 == 11 and 11^-1 mod 25 == 16: choose hi so that (hi*11 + low) % 25 == nature.
                int residual = (nature - (int)(low % 25)) % 25;
                if (residual < 0)
                    residual += 25;
                uint residue = (uint)((residual * 16) % 25);
                for (uint hi = residue; hi <= 0xFFFF; hi += 25)
                {
                    uint pid = (hi << 16) | low;
                    uint xor = ((pid >> 16) ^ (pid & 0xFFFF) ^ tid ^ sid) & 0xFFFF;
                    if (xor < 8)
                        continue; // avoid an unintended shiny
                    Accept(pid, abilitySlot);
                    return;
                }
            }
        }

        throw new ArgumentException("No PID satisfies the requested nature/gender/shiny/ability constraints.", nameof(nature));
    }

    private bool IsPidAcceptable(uint pid, int nature, int gender, byte genderRatio, bool shiny, int abilitySlot)
    {
        if (pid % 25 != (uint)nature)
            return false;
        if (abilitySlot != 2 && (pid & 1) != (uint)abilitySlot)
            return false;
        if (GenderFromLow((byte)(pid & 0xFF), genderRatio) != gender)
            return false;
        uint id = ID32;
        bool isShiny = (((pid >> 16) ^ (pid & 0xFFFF) ^ (id >> 16) ^ (id & 0xFFFF)) & 0xFFFF) < 8;
        return isShiny == shiny;
    }

    private void Accept(uint pid, int abilitySlot)
    {
        PID = pid;
        HiddenAbility = abilitySlot == 2;
    }

    // --- packing helpers --------------------------------------------------

    /// <summary>ROM 0x09D548D0: 58-byte boxed record -> 80-byte expanded form plus the 20-byte party tail.</summary>
    private byte[] RomExpanded(int totalLength)
    {
        var dest = new byte[totalLength];
        Buffer.BlockCopy(_box, 0, dest, 0, 0x1C);
        WriteU16(dest, MercurySaveLayout.ExTypeOverride, EncodeTypeOverride((byte)(_box[MercurySaveLayout.BoxFlags] >> 3)));
        Buffer.BlockCopy(_box, MercurySaveLayout.BoxSpecies, dest, MercurySaveLayout.ExSpecies, 11); // 0x1C-0x26 -> 0x20-0x2A
        var moves = ReadPackedMoves(_box);
        for (int i = 0; i < 4; i++)
            WriteU16(dest, MercurySaveLayout.ExMoves + (2 * i), (ushort)((moves >> (10 * i)) & 0x3FF));
        Buffer.BlockCopy(_box, MercurySaveLayout.BoxEvs, dest, MercurySaveLayout.ExEvs, 6);
        Buffer.BlockCopy(_box, MercurySaveLayout.BoxPokerus, dest, MercurySaveLayout.ExPokerus, 8); // 0x32-0x39 -> 0x44-0x4B
        return dest;
    }

    /// <summary>
    /// Rebuild a 100-byte party record from the original template, applying only canonical fields that changed.
    /// Header flags keep their low 3 bits separate from the 5-bit type override (re-encoded only when changed);
    /// each move is written only when its own 10-bit id changed. Unknown expanded bytes stay from the template.
    /// </summary>
    private byte[] LosslessExpanded()
    {
        var dest = (byte[])_partyTemplate!.Clone();

        if (RegionChanged(0x00, 0x12))
            Buffer.BlockCopy(_box, 0x00, dest, 0x00, 0x12);
        if (_box[MercurySaveLayout.BoxLanguage] != _snapshot[MercurySaveLayout.BoxLanguage])
            dest[0x12] = _box[MercurySaveLayout.BoxLanguage];

        byte curFlags = _box[MercurySaveLayout.BoxFlags];
        byte snapFlags = _snapshot[MercurySaveLayout.BoxFlags];
        if ((curFlags & MercurySaveLayout.FlagBits) != (snapFlags & MercurySaveLayout.FlagBits))
            dest[MercurySaveLayout.BoxFlags] = (byte)((dest[MercurySaveLayout.BoxFlags] & ~MercurySaveLayout.FlagBits) | (curFlags & MercurySaveLayout.FlagBits));
        if ((curFlags >> 3) != (snapFlags >> 3))
            WriteU16(dest, MercurySaveLayout.ExTypeOverride, EncodeTypeOverride((byte)(curFlags >> 3)));

        if (RegionChanged(0x14, 0x08))
            Buffer.BlockCopy(_box, 0x14, dest, 0x14, 0x08); // OT name + markings
        if (RegionChanged(MercurySaveLayout.BoxSpecies, 0x0B))
            Buffer.BlockCopy(_box, MercurySaveLayout.BoxSpecies, dest, MercurySaveLayout.ExSpecies, 0x0B); // species..ball

        var moves = Moves;
        ulong snapMoves = ReadPackedMoves(_snapshot);
        for (int i = 0; i < 4; i++)
        {
            if (moves[i] != (ushort)((snapMoves >> (10 * i)) & 0x3FF))
                WriteU16(dest, MercurySaveLayout.ExMoves + (2 * i), moves[i]);
        }

        if (RegionChanged(MercurySaveLayout.BoxEvs, 6))
            Buffer.BlockCopy(_box, MercurySaveLayout.BoxEvs, dest, MercurySaveLayout.ExEvs, 6);
        if (RegionChanged(MercurySaveLayout.BoxPokerus, 8))
            Buffer.BlockCopy(_box, MercurySaveLayout.BoxPokerus, dest, MercurySaveLayout.ExPokerus, 8);

        return dest;
    }

    private bool RegionChanged(int offset, int length)
    {
        for (int i = 0; i < length; i++)
        {
            if (_box[offset + i] != _snapshot[offset + i])
                return true;
        }
        return false;
    }

    private static void Compress(byte[] party, byte[] box)
    {
        // Mirror of ROM 0x09D54AFC.
        Array.Clear(box);
        Buffer.BlockCopy(party, 0, box, 0, 0x1C);
        byte typeOverride = DecodeTypeOverride(ReadU16(party, MercurySaveLayout.ExTypeOverride));
        box[MercurySaveLayout.BoxFlags] = (byte)((party[MercurySaveLayout.BoxFlags] & MercurySaveLayout.FlagBits) | (typeOverride << 3));
        Buffer.BlockCopy(party, MercurySaveLayout.ExSpecies, box, MercurySaveLayout.BoxSpecies, 11); // 0x20-0x2A -> 0x1C-0x26
        Buffer.BlockCopy(party, MercurySaveLayout.ExEvs, box, MercurySaveLayout.BoxEvs, 6);
        Buffer.BlockCopy(party, MercurySaveLayout.ExPokerus, box, MercurySaveLayout.BoxPokerus, 8); // 0x44-0x4B -> 0x32-0x39

        var moves = new ushort[4];
        for (int i = 0; i < 4; i++)
            moves[i] = ReadU16(party, MercurySaveLayout.ExMoves + (2 * i));
        WritePackedMoves(box, moves);
    }

    /// <summary>ROM 0x09D60E98: encode the 5-bit type override field into the expanded form.</summary>
    private static ushort EncodeTypeOverride(byte bits)
    {
        if (bits == 0)
            return 0;
        if (bits == 0x1F)
            return 0xA51F;
        return MercurySaveLayout.IsValidType(bits - 1) ? (ushort)(0xA500 | bits) : (ushort)0;
    }

    /// <summary>ROM 0x09D60E64: decode the expanded type override into the 5-bit boxed field.</summary>
    private static byte DecodeTypeOverride(ushort value)
    {
        if ((value & 0xFF00) != 0xA500)
            return 0;
        byte slot = (byte)(value & 0xFF);
        if (slot is 0 or 0x1F)
            return slot;
        return MercurySaveLayout.IsValidType(slot - 1) ? slot : (byte)0;
    }

    private static ulong ReadPackedMoves(byte[] box)
    {
        int o = MercurySaveLayout.BoxMovesPacked;
        return box[o]
            | ((ulong)box[o + 1] << 8)
            | ((ulong)box[o + 2] << 16)
            | ((ulong)box[o + 3] << 24)
            | ((ulong)box[o + 4] << 32);
    }

    private static void WritePackedMoves(byte[] box, ulong bits)
    {
        int o = MercurySaveLayout.BoxMovesPacked;
        box[o] = (byte)(bits & 0xFF);
        box[o + 1] = (byte)((bits >> 8) & 0xFF);
        box[o + 2] = (byte)((bits >> 16) & 0xFF);
        box[o + 3] = (byte)((bits >> 24) & 0xFF);
        box[o + 4] = (byte)((bits >> 32) & 0xFF);
    }

    private static void WritePackedMoves(byte[] box, ushort[] moves)
    {
        ulong bits = ((ulong)moves[0] & 0x3FFUL)
            | (((ulong)moves[1] & 0x3FFUL) << 10)
            | (((ulong)moves[2] & 0x3FFUL) << 20)
            | (((ulong)moves[3] & 0x3FFUL) << 30);
        WritePackedMoves(box, bits);
    }

    private static void SetFixed(byte[] target, int offset, int length, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > length)
            throw new ArgumentException($"Value must be at most {length} bytes.", nameof(value));
        Array.Clear(target, offset, length);
        Buffer.BlockCopy(value, 0, target, offset, value.Length);
    }

    private static byte[] Slice(byte[] source, int offset, int length)
    {
        var result = new byte[length];
        Buffer.BlockCopy(source, offset, result, 0, length);
        return result;
    }

    private static ushort ReadU16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

    private static void WriteU16(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)(value >> 8);
    }

    private static uint ReadU32(byte[] data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static void WriteU32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    // Nature 0-24 -> increased/decreased stat slot: 0=Atk, 1=Def, 2=Spe, 3=SpA, 4=SpD, -1 = neutral.
    private static readonly int[] NatureUp =
    [
        -1, 0, 0, 0, 0,
        1, -1, 1, 1, 1,
        2, 2, -1, 2, 2,
        3, 3, 3, -1, 3,
        4, 4, 4, 4, -1,
    ];

    private static readonly int[] NatureDown =
    [
        -1, 1, 2, 3, 4,
        0, -1, 2, 3, 4,
        0, 1, -1, 3, 4,
        0, 1, 2, -1, 4,
        0, 1, 2, 3, -1,
    ];
}
