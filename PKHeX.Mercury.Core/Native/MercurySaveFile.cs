using System;
using System.Collections.Generic;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Native <see cref="SaveFile"/> adapter over <see cref="MercurySave"/>.
/// <para>
/// The native PKHeX storage/manipulation API (box get/set, party, move/swap/sort, slot managers) operates on a
/// synthetic contiguous <see cref="BoxBuffer"/> (25 x 30 x 58) and <see cref="PartyBuffer"/> (6 x 100). On
/// export, only records whose bytes actually changed are mapped back to the real save sectors; unchanged
/// bytes, the inactive slot, parasite tails, sectors 28-31 and any 16-byte RTC trailer are preserved
/// verbatim by <see cref="MercurySave.Export"/>.
/// </para>
/// </summary>
public sealed class MercurySaveFile : SaveFile
{
    private MercurySave _backend;
    private MercuryTrainer _trainer;

    private readonly byte[] _boxBuffer;
    private readonly byte[] _partyBuffer;
    private byte[] _boxSnapshot;
    private byte[] _partySnapshot;

    private int _partyCount;
    private bool _trainerDirty;
    private ushort[]? _heldItems;

    public MercuryGameData GameData { get; }

    /// <summary>The underlying Mercury container adapter (mapped sectors, trainer, stat mode).</summary>
    public MercurySave Backend => _backend;

    /// <summary>Save stat-scaling mode (0, 11, 12 or 13) read from the ROM flag/VAR.</summary>
    public int StatMode => _backend.StatMode;

    public MercurySaveFile(byte[] data, MercuryGameData gameData) : base(new byte[0], true)
    {
        ArgumentNullException.ThrowIfNull(data);
        GameData = gameData ?? throw new ArgumentNullException(nameof(gameData));

        _backend = MercurySave.Load(data);
        _boxBuffer = new byte[BoxCount * BoxSlotCount * SIZE_STORED];
        _partyBuffer = new byte[6 * SIZE_PARTY];
        _boxSnapshot = [];
        _partySnapshot = [];
        _trainer = new MercuryTrainer();

        LoadFromBackend();
        Box = 0;
        Party = 0;
    }

    /// <summary>
    /// Create a valid, clearly unsaved blank Mercury container (used only for the native editor startup
    /// structure). Never derived from or written over a user save.
    /// </summary>
    public static MercurySaveFile CreateBlank(MercuryGameData gameData)
    {
        var data = new byte[MercurySaveLayout.SaveBlockSize];
        InitializeBlankSlot(data, 0, 1);
        InitializeBlankSlot(data, MercurySaveLayout.SectionCount, 0);
        return new MercurySaveFile(data, gameData);
    }

    private static void InitializeBlankSlot(byte[] data, int sectorBase, uint counter)
    {
        for (int i = 0; i < MercurySaveLayout.SectionCount; i++)
        {
            int off = (sectorBase + i) * MercurySaveLayout.SectorSize;
            WriteU16(data, off + MercurySaveLayout.SectionIdOffset, (ushort)i);
            WriteU16(data, off + MercurySaveLayout.SectionChecksumOffset, 0);
            WriteU32(data, off + MercurySaveLayout.SectionSignatureOffset, MercurySaveLayout.SectionSignature);
            WriteU32(data, off + MercurySaveLayout.SectionCounterOffset, counter);
        }
    }

    private void LoadFromBackend()
    {
        for (int box = 0; box < BoxCount; box++)
        {
            for (int slot = 0; slot < BoxSlotCount; slot++)
            {
                var bytes = _backend.GetBox(box, slot).ToBoxBytes();
                bytes.AsSpan(0, SIZE_STORED).CopyTo(_boxBuffer.AsSpan((box * BoxSlotCount + slot) * SIZE_STORED));
            }
        }

        for (int slot = 0; slot < 6; slot++)
        {
            var bytes = _backend.GetParty(slot).ToPartyBytes();
            bytes.AsSpan(0, SIZE_PARTY).CopyTo(_partyBuffer.AsSpan(slot * SIZE_PARTY));
        }

        _partyCount = _backend.PartyCount;
        _trainer = _backend.GetTrainer();
        _trainerDirty = false;
        _boxSnapshot = (byte[])_boxBuffer.Clone();
        _partySnapshot = (byte[])_partyBuffer.Clone();
    }

    // --- metadata ---------------------------------------------------------

    protected override string ShortSummary => "Mercury 1.1 save";
    public override string Extension => "sav";
    public override IReadOnlyList<string> PKMExtensions => ["mercurypkm", "m3box", "m3pk", "m3party", "m3stored"];
    public override GameVersion Version { get => GameVersion.FR; set { } }
    public override bool ChecksumsValid => true;
    public override string ChecksumInfo => _backend.Warnings.Count == 0 ? "Mercury sections valid" : string.Join("; ", _backend.Warnings);
    /// <summary>A checksum-valid retail interpretation requiring an explicit native host choice.</summary>
    public SaveFile? RetailAlternative { get; set; }
    public override byte Generation => 3;
    public override EntityContext Context => EntityContext.Gen3;

    public override IPersonalTable Personal => GameData.Personal;
    public override int MaxStringLengthTrainer => 7;
    public override int MaxStringLengthNickname => 10;
    public override ushort MaxMoveID => 1014;
    public override ushort MaxSpeciesID => 1553;
    public override int MaxAbilityID => 254;
    public override int MaxItemID => 749;
    public override PlayerBag Inventory => new MercuryPlayerBag(this);
    public override int MaxBallID => 255;
    public override GameVersion MaxGameID => (GameVersion)0xF;
    public override int BoxCount => 25;
    public override int MaxCoins => 999_999_999;

    // --- buffers / offsets ------------------------------------------------

    protected override Span<byte> BoxBuffer => _boxBuffer;
    protected override Span<byte> PartyBuffer => _partyBuffer;

    public override int GetBoxOffset(int box) => box * BoxSlotCount * SIZE_STORED;
    public override int GetPartyOffset(int slot) => slot * SIZE_PARTY;

    public override bool IsPKMPresent(ReadOnlySpan<byte> data)
    {
        if (data.Length < MercurySaveLayout.BoxSpecies + 2)
            return false;
        ushort species = (ushort)(data[MercurySaveLayout.BoxSpecies] | (data[MercurySaveLayout.BoxSpecies + 1] << 8));
        return species != 0;
    }

    // --- party count ------------------------------------------------------

    public override int PartyCount
    {
        get => _partyCount;
        protected set => _partyCount = value;
    }

    // --- PKM access -------------------------------------------------------

    public override Type PKMType => typeof(MercuryPKM);
    public override PKM BlankPKM => new MercuryPKM(GameData, MercuryPokemon.Empty, StatMode);
    public override int SIZE_STORED => 58;
    public override int SIZE_PARTY => 100;
    public override int MaxEV => 255;

    public override ReadOnlySpan<ushort> HeldItems => _heldItems ??= BuildHeldItems();

    private ushort[] BuildHeldItems()
    {
        var items = GameData.Items;
        var result = new ushort[items.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = (ushort)items[i].Id;
        return result;
    }

    protected override PKM GetPKM(Memory<byte> data)
    {
        if (data.Length >= SIZE_PARTY)
            return MercuryPKM.FromParty(GameData, data.Span[..SIZE_PARTY], StatMode);
        return MercuryPKM.FromStored(GameData, data.Span[..SIZE_STORED], StatMode);
    }

    protected override void DecryptPKM(Span<byte> data) { } // Mercury records are plaintext.

    // --- text -------------------------------------------------------------

    public override string GetString(ReadOnlySpan<byte> data) => GameData.Text.Decode(data);

    public override int LoadString(ReadOnlySpan<byte> data, Span<char> text)
    {
        var decoded = GameData.Text.Decode(data);
        int length = Math.Min(decoded.Length, text.Length);
        decoded.AsSpan(0, length).CopyTo(text);
        return length;
    }

    public override int SetString(Span<byte> destBuffer, ReadOnlySpan<char> value, int maxLength, StringConverterOption option)
    {
        if (option is StringConverterOption.ClearFF)
            destBuffer.Fill(MercuryTextCodec.TerminatorByte);
        else if (option is StringConverterOption.ClearZero or StringConverterOption.ClearZeroSafeTerminate)
            destBuffer.Clear();

        int budget = Math.Min(maxLength, destBuffer.Length);
        if (budget <= 0)
            return 0;

        string text = value.ToString();
        while (text.Length > 0 && !GameData.Text.CanEncode(text, budget))
            text = text[..^1];

        byte[] encoded;
        try
        {
            encoded = GameData.Text.Encode(text, budget);
        }
        catch (ArgumentException)
        {
            return 0;
        }
        int written = Math.Min(encoded.Length, destBuffer.Length);
        encoded.AsSpan(0, written).CopyTo(destBuffer);
        return written;
    }

    // --- trainer ----------------------------------------------------------

    public override byte Gender
    {
        get => _trainer.Gender;
        set { _trainer.Gender = value; _trainerDirty = true; }
    }

    public override uint ID32
    {
        get => _trainer.ID32;
        set { _trainer.ID32 = value; _trainerDirty = true; }
    }

    public override ushort TID16
    {
        get => (ushort)(_trainer.ID32 & 0xFFFF);
        set { _trainer.ID32 = (_trainer.ID32 & 0xFFFF0000u) | value; _trainerDirty = true; }
    }

    public override ushort SID16
    {
        get => (ushort)(_trainer.ID32 >> 16);
        set { _trainer.ID32 = (_trainer.ID32 & 0xFFFFu) | ((uint)value << 16); _trainerDirty = true; }
    }

    public override string OT
    {
        get => GameData.Text.Decode(_trainer.NameBytes);
        set
        {
            _trainer.NameBytes = EncodeTrainerName(value ?? string.Empty);
            _trainerDirty = true;
        }
    }

    private byte[] EncodeTrainerName(string value)
    {
        while (value.Length > 0 && !GameData.Text.CanEncode(value, MercurySaveLayout.TrainerNameLength))
            value = value[..^1];
        try
        {
            return GameData.Text.Encode(value, MercurySaveLayout.TrainerNameLength);
        }
        catch (ArgumentException)
        {
            return new byte[MercurySaveLayout.TrainerNameLength];
        }
    }

    public override uint Money
    {
        get => _trainer.Money;
        set { _trainer.Money = value; _trainerDirty = true; }
    }

    public override int PlayedHours
    {
        get => _trainer.PlayedHours;
        set { _trainer.PlayedHours = (ushort)Math.Clamp(value, 0, 0xFFFF); _trainerDirty = true; }
    }

    public override int PlayedMinutes
    {
        get => _trainer.PlayedMinutes;
        set { _trainer.PlayedMinutes = (byte)Math.Clamp(value, 0, 59); _trainerDirty = true; }
    }

    public override int PlayedSeconds
    {
        get => _trainer.PlayedSeconds;
        set { _trainer.PlayedSeconds = (byte)Math.Clamp(value, 0, 59); _trainerDirty = true; }
    }

    /// <summary>Extended Mercury coin count (u32, not XOR-encrypted).</summary>
    public uint Coins
    {
        get => _trainer.Coins;
        set { _trainer.Coins = value; _trainerDirty = true; }
    }

    // --- staging / export -------------------------------------------------

    protected override void SetChecksums() => FlushToBackend();

    protected override Memory<byte> GetFinalData()
    {
        FlushToBackend();
        return _backend.Export();
    }

    public override void CopyChangesFrom(SaveFile sav)
    {
        if (sav is MercurySaveFile other)
        {
            other.FlushToBackend();
            _backend = MercurySave.Load(other._backend.Export());
            LoadFromBackend();
            State.Edited = true;
            return;
        }
        base.CopyChangesFrom(sav);
    }

    protected override SaveFile CloneInternal()
    {
        FlushToBackend();
        return new MercurySaveFile(_backend.Export(), GameData);
    }

    private void FlushToBackend()
    {
        for (int box = 0; box < BoxCount; box++)
        {
            for (int slot = 0; slot < BoxSlotCount; slot++)
            {
                int offset = (box * BoxSlotCount + slot) * SIZE_STORED;
                if (RegionEquals(_boxBuffer, offset, _boxSnapshot, offset, SIZE_STORED))
                    continue;
                var mon = MercuryPokemon.FromBox(_boxBuffer.AsSpan(offset, SIZE_STORED).ToArray());
                _backend.SetBox(box, slot, mon);
            }
        }

        bool partyChanged = _partyCount != _backend.PartyCount
            || !RegionEquals(_partyBuffer, 0, _partySnapshot, 0, _partyBuffer.Length);
        if (partyChanged)
        {
            while (_backend.PartyCount > _partyCount)
                _backend.DeleteParty(_backend.PartyCount - 1);

            for (int i = 0; i < _partyCount; i++)
            {
                var mon = MercuryPokemon.FromParty(_partyBuffer.AsSpan(i * SIZE_PARTY, SIZE_PARTY).ToArray());
                _backend.SetParty(i, mon);
            }
        }

        if (_trainerDirty)
        {
            _backend.SetTrainer(_trainer);
            _trainerDirty = false;
        }

        Array.Copy(_boxBuffer, _boxSnapshot, _boxBuffer.Length);
        Array.Copy(_partyBuffer, _partySnapshot, _partyBuffer.Length);
    }

    private static bool RegionEquals(byte[] a, int aOffset, byte[] b, int bOffset, int length)
        => a.AsSpan(aOffset, length).SequenceEqual(b.AsSpan(bOffset, length));

    // --- low-level helpers ------------------------------------------------

    private static void WriteU16(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteU32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
