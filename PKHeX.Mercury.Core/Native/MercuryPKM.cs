using System;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Native <see cref="PKM"/> adapter for a Mercury 1.1 Pokémon.
/// <para>
/// The canonical in-memory buffer (<see cref="PKM.Data"/>, 100 bytes) is the actual Mercury party/expanded
/// record (<c>ToPartyBytes</c> layout): expanded 80 bytes at 0x00-0x4F followed by the 20-byte party tail
/// at 0x50-0x63. Every read parses that buffer through <see cref="MercuryPokemon.FromParty"/> and every write
/// re-serializes through <see cref="MercuryPokemon.ToPartyBytes"/>, so there is a single source of truth and
/// no parallel stale copy. <see cref="MercuryPokemon"/> stays the sole binary read/write definition.
/// </para>
/// <para>
/// Stored (boxed) writes use <see cref="MercuryPokemon.ToBoxBytes"/> (58 bytes); party writes use
/// <c>ToPartyBytes</c> (100 bytes). Plaintext, no shuffling, no XOR: <see cref="EncryptStored"/> and
/// <see cref="EncryptParty"/> are no-ops.
/// </para>
/// </summary>
public sealed class MercuryPKM : PKM, IAppliedMarkings3
{
    /// <summary>
    /// Profile used by the parameterless constructor required by the upstream editor's reflection-based blank
    /// creation. Set by the host before any blank <see cref="MercuryPKM"/> is created; when unset the
    /// constructor falls back to <see cref="MercuryGameData.NumericOnly"/> (zero/generic, never retail data).
    /// </summary>
    public static MercuryGameData? DefaultGameData { get; set; }

    private int _statMode;

    /// <summary>Save stat-scaling mode (0, 11, 12 or 13) used by <see cref="LoadStats"/>.</summary>
    public int StatMode
    {
        get => _statMode;
        set
        {
            if (value is not (0 or 11 or 12 or 13))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Stat mode must be 0, 11, 12 or 13.");
            _statMode = value;
        }
    }

    public MercuryGameData GameData { get; }

    public MercuryPKM(MercuryGameData gameData, MercuryPokemon mon, int statMode = 0)
        : base(SIZE_PARTY_CONST)
    {
        GameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        StatMode = statMode;

        var bytes = mon.ToPartyBytes();
        bytes.AsSpan(0, Math.Min(bytes.Length, Data.Length)).CopyTo(Data);
        if (!mon.IsPartyForm)
            InitializePartyPp();
    }

    /// <summary>Blank entity using <see cref="DefaultGameData"/>; only for upstream blank-creation needs.</summary>
    public MercuryPKM() : base(SIZE_PARTY_CONST)
    {
        GameData = DefaultGameData ?? MercuryGameData.NumericOnly();
    }

    /// <summary>Build from an exact 58-byte boxed record.</summary>
    public static MercuryPKM FromStored(MercuryGameData gameData, ReadOnlySpan<byte> box, int statMode = 0)
        => new(gameData, MercuryPokemon.FromBox(box.ToArray()), statMode);

    /// <summary>Build from an exact 100-byte party record.</summary>
    public static MercuryPKM FromParty(MercuryGameData gameData, ReadOnlySpan<byte> party, int statMode = 0)
        => new(gameData, MercuryPokemon.FromParty(party.ToArray()), statMode);

    /// <summary>Current Mercury record (a clone; mutating it does not affect this instance).</summary>
    public MercuryPokemon ToMercuryPokemon() => Mon.Clone();

    // --- canonical buffer bridge -----------------------------------------

    private MercuryPokemon Mon => MercuryPokemon.FromParty(Data.ToArray());

    private void SetMon(MercuryPokemon mon)
    {
        var bytes = mon.ToPartyBytes();
        bytes.AsSpan(0, Math.Min(bytes.Length, Data.Length)).CopyTo(Data);
    }

    /// <summary>For box-origin records, seed current PP from the ROM move table and the stored PP-ups.</summary>
    private void InitializePartyPp()
    {
        var moves = Mon.Moves;
        var ups = Mon.PPUps;
        for (int i = 0; i < 4; i++)
        {
            int pp = moves[i] == 0 ? 0 : GetMovePP(moves[i], ups[i]);
            Data[MercurySaveLayout.ExCurrentPp + i] = (byte)Math.Clamp(pp, 0, 255);
        }
    }

    // --- sizes / limits ---------------------------------------------------

    private const int SIZE_PARTY_CONST = 100;

    public override int SIZE_PARTY => 100;
    public override int SIZE_STORED => 58;
    public override EntityContext Context => EntityContext.Gen3;
    public override bool SupportsRetailLegality => false;

    public override ushort MaxMoveID => 1014;
    public override ushort MaxSpeciesID => 1553;
    public override int MaxItemID => 749;
    public override int MaxAbilityID => 254;
    public override int MaxBallID => 255;
    public override GameVersion MaxGameID => (GameVersion)0xF;
    public override int MaxIV => 31;
    public override int MaxEV => 255;
    public override int MaxStringLengthTrainer => 7;
    public override int MaxStringLengthNickname => 10;
    public override int TrashCharCountTrainer => 7;
    public override int TrashCharCountNickname => 10;

    public override PersonalInfo PersonalInfo => GameData.Personal.GetFormEntry(Species, Form);

    public override Span<byte> NicknameTrash => Data.Slice(MercurySaveLayout.BoxNickname, MercurySaveLayout.BoxNicknameLength);
    public override Span<byte> OriginalTrainerTrash => Data.Slice(MercurySaveLayout.BoxOtName, MercurySaveLayout.BoxOtNameLength);

    // --- validity / integrity --------------------------------------------

    public override bool Valid { get => Species != 0; set { } }
    public override bool ChecksumValid => true;
    public override void RefreshChecksum() { }
    protected override void EncryptStored(Span<byte> stored) { }
    protected override void EncryptParty(Span<byte> party) { }

    public override PKM Clone() => new MercuryPKM(GameData, Mon.Clone(), StatMode);

    // --- serialization ----------------------------------------------------

    public override int WriteDecryptedDataStored(Span<byte> destination)
    {
        var box = Mon.ToBoxBytes();
        box.AsSpan(0, Math.Min(box.Length, destination.Length)).CopyTo(destination);
        return SIZE_STORED;
    }

    public override void WriteDecryptedDataParty(Span<byte> stored, Span<byte> party)
    {
        var full = Mon.ToPartyBytes();
        int storedLen = Math.Min(SIZE_STORED, stored.Length);
        full.AsSpan(0, storedLen).CopyTo(stored);
        int partyLen = Math.Min(SIZE_PARTY - SIZE_STORED, party.Length);
        full.AsSpan(SIZE_STORED, partyLen).CopyTo(party);
    }

    // --- identity ---------------------------------------------------------

    public override ushort Species
    {
        get => Mon.Species;
        set { var m = Mon; m.Species = value; SetMon(m); }
    }

    /// <summary>Raw Mercury type override; only values preserved by the party encoding are writable. Not a Tera type.</summary>
    public int TypeOverride
    {
        get => Mon.TypeOverride;
        set
        {
            if (value is not (0 or 31) && (value < 1 || value > 25 || !MercurySaveLayout.IsValidType(value - 1)))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Type override must be a supported Mercury party encoding.");
            var m = Mon;
            m.TypeOverride = value;
            SetMon(m);
        }
    }

    public override uint PID
    {
        get => Mon.PID;
        set { var m = Mon; m.PID = value; SetMon(m); }
    }

    public override uint EncryptionConstant
    {
        get => Mon.PID;
        set { } // Gen3 has no separate encryption constant.
    }

    public override uint ID32
    {
        get => Mon.ID32;
        set { var m = Mon; m.ID32 = value; SetMon(m); }
    }

    public override ushort TID16
    {
        get => (ushort)(Mon.ID32 & 0xFFFF);
        set { var m = Mon; m.ID32 = (m.ID32 & 0xFFFF0000u) | value; SetMon(m); }
    }

    public override ushort SID16
    {
        get => (ushort)(Mon.ID32 >> 16);
        set { var m = Mon; m.ID32 = (m.ID32 & 0xFFFFu) | ((uint)value << 16); SetMon(m); }
    }

    public override uint TSV => (uint)(TID16 ^ SID16) >> 3;
    public override uint PSV => ((PID >> 16) ^ (PID & 0xFFFF)) >> 3;

    public override GameVersion Version
    {
        get => (GameVersion)Mon.MetGame;
        set
        {
            if ((uint)value > 0xF)
                throw new ArgumentOutOfRangeException(nameof(value));
            var mon = Mon;
            mon.MetGame = (byte)value;
            SetMon(mon);
        }
    }

    public override int Language
    {
        get => Mon.Language;
        set { var m = Mon; m.Language = (byte)value; SetMon(m); }
    }

    // --- text -------------------------------------------------------------

    public override string GetString(ReadOnlySpan<byte> data) => GameData.Text.Decode(data);

    public override int LoadString(ReadOnlySpan<byte> data, Span<char> text)
    {
        var decoded = GameData.Text.Decode(data);
        int length = Math.Min(decoded.Length, text.Length);
        decoded.AsSpan(0, length).CopyTo(text);
        return length;
    }

    public override int SetString(Span<byte> data, ReadOnlySpan<char> text, int length, StringConverterOption option)
    {
        if (option is StringConverterOption.ClearFF)
            data.Fill(MercuryTextCodec.TerminatorByte);
        else if (option is StringConverterOption.ClearZero or StringConverterOption.ClearZeroSafeTerminate)
            data.Clear();

        int budget = data.Length;
        if (budget <= 0)
            return 0;

        string value = text[..Math.Min(Math.Max(length, 0), text.Length)].ToString();
        while (value.Length > 0 && !GameData.Text.CanEncode(value, budget))
            value = value[..^1];

        byte[] encoded;
        try
        {
            encoded = GameData.Text.Encode(value, budget);
        }
        catch (ArgumentException)
        {
            return 0;
        }
        int written = Math.Min(encoded.Length, data.Length);
        encoded.AsSpan(0, written).CopyTo(data);
        return written;
    }

    public override int GetStringTerminatorIndex(ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == MercuryTextCodec.TerminatorByte)
                return i;
        }
        return data.Length;
    }

    public override int GetStringLength(ReadOnlySpan<byte> data) => GetStringTerminatorIndex(data);

    public override int GetBytesPerChar() => 1;

    public override string Nickname
    {
        get => GetString(NicknameTrash);
        set => SetString(NicknameTrash, value, MaxStringLengthNickname, StringConverterOption.ClearFF);
    }

    public override bool IsNicknamed
    {
        get => Species != 0 && Nickname != GameData.SpeciesName(Species);
        set { }
    }

    public override string GetDefaultNickname() => Species == 0 ? string.Empty : GameData.SpeciesName(Species);

    public override string OriginalTrainerName
    {
        get => GetString(OriginalTrainerTrash);
        set => SetString(OriginalTrainerTrash, value, MaxStringLengthTrainer, StringConverterOption.ClearFF);
    }

    // --- misc surface fields ---------------------------------------------

    public override int HeldItem
    {
        get => Mon.HeldItem;
        set { var m = Mon; m.HeldItem = (ushort)value; SetMon(m); }
    }

    public override byte Form
    {
        get => Species == 201 ? EntityPID.GetUnownForm3(PID) : (byte)0;
        set
        {
            if (Species != 201)
                return;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, (byte)27);
            // Same form-setting interaction as G3PKM: the form is encoded in the PID.
            while (EntityPID.GetUnownForm3(PID) != value)
                PID = Util.Rand.Rand32();
        }
    }

    public override bool IsEgg
    {
        get => Mon.IsEgg;
        set { var m = Mon; m.IsEgg = value; SetMon(m); }
    }

    public override uint EXP
    {
        get => Mon.Experience;
        set { var m = Mon; m.Experience = value; SetMon(m); }
    }

    public override byte CurrentFriendship
    {
        get => Mon.Friendship;
        set { var m = Mon; m.Friendship = value; SetMon(m); }
    }

    public override byte OriginalTrainerFriendship
    {
        get => Mon.Friendship;
        set { var m = Mon; m.Friendship = value; SetMon(m); }
    }

    public override byte OriginalTrainerGender
    {
        get => Mon.OTGender;
        set { var m = Mon; m.OTGender = value; SetMon(m); }
    }

    public override byte Ball
    {
        get => Mon.Ball;
        set { var m = Mon; m.Ball = value; SetMon(m); }
    }

    public override byte MetLevel
    {
        get => Mon.MetLevel;
        set { var m = Mon; m.MetLevel = value; SetMon(m); }
    }

    public override ushort MetLocation
    {
        get => Mon.MetLocation;
        set { var m = Mon; m.MetLocation = (byte)value; SetMon(m); }
    }

    public override ushort EggLocation
    {
        get => 0; // Mercury has no egg-location field; the native rule is "no such field".
        set { }
    }

    public override bool FatefulEncounter
    {
        get => false; // no proven fateful-encounter flag; origins bits 11-14 are preserved verbatim.
        set { }
    }

    public override byte CurrentHandler
    {
        get => 0;
        set { }
    }

    public override int Characteristic => -1;

    public override int PokerusStrain
    {
        get => Mon.Pokerus >> 4;
        set { var m = Mon; m.Pokerus = (byte)((m.Pokerus & 0x0F) | ((value & 0xF) << 4)); SetMon(m); }
    }

    public override int PokerusDays
    {
        get => Mon.Pokerus & 0x0F;
        set { var m = Mon; m.Pokerus = (byte)((m.Pokerus & 0xF0) | (value & 0xF)); SetMon(m); }
    }

    // --- nature / gender / ability / shiny (interdependent PID) -----------

    public override Nature Nature
    {
        get => (Nature)Mon.Nature;
        set => ApplyPersonality((int)value, Gender, IsShiny, EffectiveSlot());
    }

    public override byte Gender
    {
        get
        {
            var pi = PersonalInfo as MercuryPersonalInfo;
            if (pi is null)
                return 2;
            return (byte)Mon.GetGender(pi.Gender);
        }
        set => ApplyPersonality(Mon.Nature, value, IsShiny, EffectiveSlot());
    }

    public override int Ability
    {
        get
        {
            var pi = PersonalInfo as MercuryPersonalInfo;
            return pi is null ? 0 : pi.GetAbilityAtIndex(EffectiveSlot());
        }
        set
        {
            var pi = PersonalInfo as MercuryPersonalInfo;
            if (pi is null)
                return;
            int index = pi.GetIndexOfAbility(value);
            if (index < 0)
                return;
            ApplyPersonality(Mon.Nature, Gender, IsShiny, index, updateAbility: true);
        }
    }

    public override int AbilityNumber
    {
        get => 1 << EffectiveSlot();
        set => ApplyPersonality(Mon.Nature, Gender, IsShiny, value switch { 1 => 0, 2 => 1, 4 => 2, _ => 0 }, updateAbility: true);
    }

    public override void SetPIDGender(byte gender) => Gender = gender;

    public override void SetPIDNature(Nature nature) => Nature = nature;

    public override void RefreshAbility(int n)
    {
        if ((uint)n > 2)
            return;
        var pi = PersonalInfo as MercuryPersonalInfo;
        int slot = n;
        if (pi is not null)
        {
            if (slot == 2 && pi.GetAbilityAtIndex(2) == 0)
                slot = 0;
            if (slot == 1 && pi.GetAbilityAtIndex(1) == 0)
                slot = 0;
        }
        ApplyPersonality(Mon.Nature, Gender, IsShiny, slot, updateAbility: true);
    }

    public override void SetShiny()
    {
        if (IsShiny)
            return;
        ApplyPersonality(Mon.Nature, Gender, true, EffectiveSlot());
    }

    private int EffectiveSlot()
    {
        var pi = PersonalInfo as MercuryPersonalInfo;
        if (pi is null)
            return 0;
        int stored = Mon.AbilitySlot; // 2 when the hidden marker is set, else PID bit 0
        if (stored == 2)
            return pi.GetAbilityAtIndex(2) != 0 ? 2 : ((Mon.PID & 1) != 0 && pi.GetAbilityAtIndex(1) != 0 ? 1 : 0);
        if (stored == 1)
            return pi.GetAbilityAtIndex(1) != 0 ? 1 : 0;
        return 0;
    }

    private void ApplyPersonality(int nature, int gender, bool shiny, int slot, bool updateAbility = false)
    {
        var pi = PersonalInfo as MercuryPersonalInfo;
        var mon = Mon;
        if (pi is { HasData: true })
        {
            int resolved = slot;
            if (resolved == 2 && pi.GetAbilityAtIndex(2) == 0)
                resolved = ((mon.PID & 1) != 0 && pi.GetAbilityAtIndex(1) != 0) ? 1 : 0;
            if (resolved == 1 && pi.GetAbilityAtIndex(1) == 0)
                resolved = 0;
            try
            {
                // A missing second ability does not forbid odd PIDs. Unown's form is another PID constraint.
                bool constrainParity = resolved != 2 && pi.GetAbilityAtIndex(1) != 0;
                byte? unownForm = Species == 201 ? EntityPID.GetUnownForm3(mon.PID) : null;
                bool changeAbility = updateAbility && resolved != EffectiveSlot();
                mon.SetPersonality(nature, gender, pi.Gender, shiny, resolved, constrainParity, unownForm, changeAbility);
            }
            catch (ArgumentException)
            {
                // Contradictory request (e.g. gender not permitted by the ratio): keep the constrained fields
                // rather than silently corrupting the PID.
                return;
            }
        }
        else
        {
            // No species data: only nature is expressible through the PID high half.
            uint low = mon.PID & 0xFFFF;
            int residual = (nature - (int)(low % 25)) % 25;
            if (residual < 0)
                residual += 25;
            uint residue = (uint)((residual * 16) % 25);
            mon.PID = (residue << 16) | low;
        }
        SetMon(mon);
    }

    // --- markings (single byte at box 0x1B, BPRE-style bit layout) ---------

    public int MarkingCount => 4;

    public byte MarkingValue
    {
        get => Mon.Markings;
        set { var m = Mon; m.Markings = value; SetMon(m); }
    }

    public bool GetMarking(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)MarkingCount);
        return ((MarkingValue >> index) & 1) != 0;
    }

    public void SetMarking(int index, bool value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)MarkingCount);
        MarkingValue = (byte)((MarkingValue & ~(1 << index)) | ((value ? 1 : 0) << index));
    }

    public bool MarkingCircle { get => GetMarking(0); set => SetMarking(0, value); }
    public bool MarkingTriangle { get => GetMarking(2); set => SetMarking(2, value); }
    public bool MarkingSquare { get => GetMarking(1); set => SetMarking(1, value); }
    public bool MarkingHeart { get => GetMarking(3); set => SetMarking(3, value); }

    // --- moves ------------------------------------------------------------

    public override ushort Move1 { get => Mon.Moves[0]; set => SetMoveValue(0, value); }
    public override ushort Move2 { get => Mon.Moves[1]; set => SetMoveValue(1, value); }
    public override ushort Move3 { get => Mon.Moves[2]; set => SetMoveValue(2, value); }
    public override ushort Move4 { get => Mon.Moves[3]; set => SetMoveValue(3, value); }

    private void SetMoveValue(int index, ushort value)
    {
        var m = Mon;
        var moves = m.Moves;
        moves[index] = value;
        m.Moves = moves;
        SetMon(m);
    }

    public override int Move1_PP { get => ReadPp(0); set => WritePp(0, value); }
    public override int Move2_PP { get => ReadPp(1); set => WritePp(1, value); }
    public override int Move3_PP { get => ReadPp(2); set => WritePp(2, value); }
    public override int Move4_PP { get => ReadPp(3); set => WritePp(3, value); }

    private int ReadPp(int index) => Data[MercurySaveLayout.ExCurrentPp + index];
    private void WritePp(int index, int value) => Data[MercurySaveLayout.ExCurrentPp + index] = (byte)Math.Clamp(value, 0, 255);

    public override int Move1_PPUps { get => Mon.PPUps[0]; set => SetPpUp(0, value); }
    public override int Move2_PPUps { get => Mon.PPUps[1]; set => SetPpUp(1, value); }
    public override int Move3_PPUps { get => Mon.PPUps[2]; set => SetPpUp(2, value); }
    public override int Move4_PPUps { get => Mon.PPUps[3]; set => SetPpUp(3, value); }

    private void SetPpUp(int index, int value)
    {
        var m = Mon;
        var ups = m.PPUps;
        ups[index] = (byte)Math.Clamp(value, 0, 3);
        m.PPUps = ups;
        SetMon(m);
    }

    /// <summary>Base PP of a move using the ROM move table (internal move ids), not the retail national table.</summary>
    public override int GetBasePP(ushort move)
    {
        var moves = GameData.Moves;
        // Unknown Mercury move IDs must not be reinterpreted using a retail PP table.
        return move < moves.Count ? moves[move].PP : 0;
    }

    /// <summary>PP of a move using the ROM move table.</summary>
    public override int GetMovePP(ushort move, int ppUpCount) => GetBasePP(move) * (5 + ppUpCount) / 5;

    // --- stat data --------------------------------------------------------

    public override int EV_HP { get => GetEv(0); set => SetEv(0, value); }
    public override int EV_ATK { get => GetEv(1); set => SetEv(1, value); }
    public override int EV_DEF { get => GetEv(2); set => SetEv(2, value); }
    public override int EV_SPE { get => GetEv(3); set => SetEv(3, value); }
    public override int EV_SPA { get => GetEv(4); set => SetEv(4, value); }
    public override int EV_SPD { get => GetEv(5); set => SetEv(5, value); }

    private int GetEv(int index) => Mon.EVs[index];
    private void SetEv(int index, int value)
    {
        var m = Mon;
        var evs = m.EVs;
        evs[index] = (byte)Math.Clamp(value, 0, MaxEV);
        m.EVs = evs;
        SetMon(m);
    }

    public override int IV_HP { get => GetIv(0); set => SetIv(0, value); }
    public override int IV_ATK { get => GetIv(1); set => SetIv(1, value); }
    public override int IV_DEF { get => GetIv(2); set => SetIv(2, value); }
    public override int IV_SPE { get => GetIv(3); set => SetIv(3, value); }
    public override int IV_SPA { get => GetIv(4); set => SetIv(4, value); }
    public override int IV_SPD { get => GetIv(5); set => SetIv(5, value); }

    private int GetIv(int index) => Mon.IVs[index];
    private void SetIv(int index, int value)
    {
        var m = Mon;
        var ivs = m.IVs;
        ivs[index] = (byte)Math.Clamp(value, 0, MaxIV);
        m.IVs = ivs;
        SetMon(m);
    }

    public override int Status_Condition
    {
        get => (int)ReadU32(Data, MercurySaveLayout.PartyStatus);
        set => WriteU32(Data, MercurySaveLayout.PartyStatus, (uint)value);
    }

    public override byte Stat_Level
    {
        get => Data[MercurySaveLayout.PartyLevel];
        set => Data[MercurySaveLayout.PartyLevel] = value;
    }

    public override int Stat_HPMax { get => ReadU16(Data, MercurySaveLayout.PartyMaxHp); set => WriteU16(Data, MercurySaveLayout.PartyMaxHp, (ushort)value); }
    public override int Stat_HPCurrent { get => ReadU16(Data, MercurySaveLayout.PartyHp); set => WriteU16(Data, MercurySaveLayout.PartyHp, (ushort)value); }
    public override int Stat_ATK { get => ReadU16(Data, MercurySaveLayout.PartyAtk); set => WriteU16(Data, MercurySaveLayout.PartyAtk, (ushort)value); }
    public override int Stat_DEF { get => ReadU16(Data, MercurySaveLayout.PartyDef); set => WriteU16(Data, MercurySaveLayout.PartyDef, (ushort)value); }
    public override int Stat_SPE { get => ReadU16(Data, MercurySaveLayout.PartySpe); set => WriteU16(Data, MercurySaveLayout.PartySpe, (ushort)value); }
    public override int Stat_SPA { get => ReadU16(Data, MercurySaveLayout.PartySpA); set => WriteU16(Data, MercurySaveLayout.PartySpA, (ushort)value); }
    public override int Stat_SPD { get => ReadU16(Data, MercurySaveLayout.PartySpD); set => WriteU16(Data, MercurySaveLayout.PartySpD, (ushort)value); }

    // --- level / stats (ROM growth + ROM stat modes) ----------------------

    public override byte CurrentLevel
    {
        get
        {
            try
            {
                return GameData.GetLevel(Species, EXP);
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentOutOfRangeException)
            {
                return Stat_Level;
            }
        }
        set
        {
            Stat_Level = value;
            try
            {
                EXP = GameData.GetExperience(Species, value);
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentOutOfRangeException)
            {
                // No growth tables loaded; keep the stored EXP.
            }
        }
    }

    public override void LoadStats(IBaseStat p, Span<ushort> stats)
    {
        int[] baseStats = [p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD];
        var computed = MercuryPokemon.ComputeStats(baseStats, CurrentLevel, StatMode, Mon.IVs, Mon.EVs, (int)Nature, Species == 0x12F);
        for (int i = 0; i < stats.Length && i < computed.Length; i++)
            stats[i] = computed[i];
    }

    // --- helpers ----------------------------------------------------------

    private static ushort ReadU16(Span<byte> data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

    private static void WriteU16(Span<byte> data, int offset, ushort value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)(value >> 8);
    }

    private static uint ReadU32(Span<byte> data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static void WriteU32(Span<byte> data, int offset, uint value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
