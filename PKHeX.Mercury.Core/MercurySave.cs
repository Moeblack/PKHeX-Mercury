using System;
using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Mercury 1.1 save file (BPRE-based, 32 flash sectors). Container layout and record layouts
/// are derived from the exact ROM build 131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd;
/// see docs/mercury-save-format.md for the instruction evidence.
/// </summary>
public sealed class MercurySave
{
    /// <summary>Number of storage boxes (ROM GetBoxedMonPtr 0x09D549A4 rejects box &gt; 0x18 == 24).</summary>
    public const int BoxCount = MercurySaveLayout.BoxCount;

    /// <summary>Slots per box (ROM rejects slot &gt; 0x1D == 29).</summary>
    public const int BoxCapacity = MercurySaveLayout.BoxCapacity;

    private const int MaxPartySize = MercurySaveLayout.PartyCount;

    private static readonly int[] SaveBlock2Ids = [0];
    private static readonly int[] SaveBlock1Ids = [1, 2, 3, 4];
    private static readonly int[] StorageIds = [5, 6, 7, 8, 9, 10, 11, 12, 13];

    private readonly byte[] _data;
    private readonly int _slot;
    private readonly int[] _sectorOfId = new int[MercurySaveLayout.SectionCount];
    private readonly bool[] _dirty = new bool[MercurySaveLayout.SectionCount];
    private readonly List<string> _warnings = [];
    private bool _anyEdit;

    private MercurySave(byte[] data, int slot, int[] sectorOfId, IEnumerable<string> warnings)
    {
        _data = data;
        _slot = slot;
        Array.Copy(sectorOfId, _sectorOfId, sectorOfId.Length);
        _warnings.AddRange(warnings);
    }

    /// <summary>Non-fatal issues found while loading; empty when the save is fully consistent.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Active slot index (0 or 1).</summary>
    public int ActiveSlot => _slot;

    /// <summary>Section counter of the active slot (wrap-aware).</summary>
    public uint SaveCounter { get; private set; }

    /// <summary>True once any <c>Set*</c>/<c>Delete*</c> call has been made.</summary>
    public bool IsDirty => _anyEdit;

    /// <summary>Number of Pokémon currently in the party (SaveBlock1+0x34).</summary>
    public int PartyCount
    {
        get
        {
            int count = _data[SectionOffset(1, 0) + MercurySaveLayout.PartyCountOffset];
            if (count > MaxPartySize)
                throw new InvalidDataException($"Invalid Mercury party count: {count}.");
            return count;
        }
    }

    // --- loading ----------------------------------------------------------

    /// <summary>
    /// Load a Mercury save. Accepts a raw 0x20000-byte dump, or 0x20000 + a 16-byte RTC trailer.
    /// Validates the signature, the complete unique 14-section set, all section checksums and uniform counters.
    /// </summary>
    public static MercurySave Load(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != MercurySaveLayout.SaveBlockSize && data.Length != MercurySaveLayout.SaveBlockSize + 16)
            throw new InvalidDataException($"Unexpected Mercury save size {data.Length}; expected {MercurySaveLayout.SaveBlockSize} or {MercurySaveLayout.SaveBlockSize + 16} bytes.");

        var copy = (byte[])data.Clone();
        var warnings = new List<string>();

        var slotA = Evaluate(copy, 0);
        var slotB = Evaluate(copy, MercurySaveLayout.SectionCount);

        SlotInfo chosen;
        if (slotA.Valid && slotB.Valid)
        {
            chosen = IsNewer(slotB.Counter, slotA.Counter) ? slotB : slotA;
        }
        else if (slotA.Valid)
        {
            chosen = slotA;
            if (IsNewer(slotB.Counter, slotA.Counter))
                warnings.Add($"Latest save slot (counter {slotB.Counter}) is invalid; fell back to the previous valid slot (counter {slotA.Counter}).");
        }
        else if (slotB.Valid)
        {
            chosen = slotB;
            if (IsNewer(slotA.Counter, slotB.Counter))
                warnings.Add($"Latest save slot (counter {slotA.Counter}) is invalid; fell back to the previous valid slot (counter {slotB.Counter}).");
        }
        else
        {
            throw new InvalidDataException(
                $"No valid Mercury save slot. Slot 0: {slotA.Reason}; slot 1: {slotB.Reason}.");
        }

        var save = new MercurySave(copy, chosen.Base / MercurySaveLayout.SectionCount, chosen.SectorOfId, warnings)
        {
            SaveCounter = chosen.Counter,
        };
        _ = save.PartyCount;
        return save;
    }

    private static bool IsNewer(uint candidate, uint current) => (int)(candidate - current) > 0;

    private readonly struct SlotInfo
    {
        public required bool Valid { get; init; }
        public required int Base { get; init; }
        public required uint Counter { get; init; }
        public required int[] SectorOfId { get; init; }
        public required string Reason { get; init; }
    }

    private static SlotInfo Evaluate(byte[] data, int sectorBase)
    {
        var sectorOfId = new int[MercurySaveLayout.SectionCount];
        Array.Fill(sectorOfId, -1);

        uint counter = 0;
        bool haveCounter = false;
        bool uniform = true;
        bool complete = true;
        string? reason = null;

        for (int i = 0; i < MercurySaveLayout.SectionCount; i++)
        {
            int sector = sectorBase + i;
            int off = sector * MercurySaveLayout.SectorSize;
            uint signature = ReadU32(data, off + MercurySaveLayout.SectionSignatureOffset);
            ushort id = ReadU16(data, off + MercurySaveLayout.SectionIdOffset);
            uint sectionCounter = ReadU32(data, off + MercurySaveLayout.SectionCounterOffset);

            if (signature != MercurySaveLayout.SectionSignature)
            {
                complete = false;
                reason ??= $"sector {sector} has signature {signature:X8}";
                continue;
            }
            if (id >= MercurySaveLayout.SectionCount)
            {
                complete = false;
                reason ??= $"sector {sector} has section id {id}";
                continue;
            }
            if (sectorOfId[id] != -1)
            {
                complete = false;
                reason ??= $"section {id} appears more than once";
                continue;
            }

            sectorOfId[id] = sector;
            if (!haveCounter)
            {
                counter = sectionCounter;
                haveCounter = true;
            }
            else if (counter != sectionCounter)
            {
                uniform = false;
            }
        }

        for (int id = 0; id < MercurySaveLayout.SectionCount; id++)
        {
            if (sectorOfId[id] == -1)
            {
                complete = false;
                reason ??= $"section {id} is missing";
                break;
            }
        }

        if (complete)
        {
            for (int id = 0; id < MercurySaveLayout.SectionCount; id++)
            {
                int off = sectorOfId[id] * MercurySaveLayout.SectorSize;
                int size = MercurySaveLayout.SectionSizes[id];
                ushort stored = ReadU16(data, off + MercurySaveLayout.SectionChecksumOffset);
                ushort actual = CalculateChecksum(data, off, size);
                if (stored != actual)
                {
                    reason = $"section {id} checksum {stored:X4} != {actual:X4}";
                    return new SlotInfo { Valid = false, Base = sectorBase, Counter = counter, SectorOfId = sectorOfId, Reason = reason };
                }
            }
        }

        if (!uniform)
        {
            complete = false;
            reason ??= "section counters are not uniform";
        }

        return new SlotInfo
        {
            Valid = complete,
            Base = sectorBase,
            Counter = counter,
            SectorOfId = sectorOfId,
            Reason = reason ?? "incomplete section set",
        };
    }

        private int SectionOffset(int sectionId, int offset) => (_sectorOfId[sectionId] * MercurySaveLayout.SectorSize) + offset;

    // --- export -----------------------------------------------------------

    // ROM 09D3E49C copies the five pouch descriptors at 09DD7240.
    // RAM 0203BB20..0203C748 spans section 13's parasite tail and sector 30.
    // PC items: SaveBlock1+298, 30 four-byte records (0809A304/0809A33C).
    private const int BagDataLength = 3112;
    private const int InventoryDataLength = BagDataLength + (30 * 4);

    public byte[] GetInventoryData()
    {
        var result = new byte[InventoryDataLength];
        for (int i = 0; i < result.Length; i++)
            result[i] = _data[InventoryFileOffset(i)];
        return result;
    }

    public void SetInventoryData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNotEqual(data.Length, InventoryDataLength);
        for (int i = 0; i < data.Length; i++)
        {
            int offset = InventoryFileOffset(i);
            if (_data[offset] == data[i])
                continue;
            _data[offset] = data[i];
            _anyEdit = true;
            if (i >= BagDataLength)
                _dirty[1] = true;
        }
    }

    private int InventoryFileOffset(int index)
    {
        if (index >= BagDataLength)
            return SectionOffset(1, 0x298 + index - BagDataLength);
        const int tailLength = 0xFF0 - 0xAD8;
        return index < tailLength
            ? SectionOffset(13, 0xAD8 + index)
            : (MercurySaveLayout.SpecialSectorA * MercurySaveLayout.SectorSize) + index - tailLength;
    }

    /// <summary>
    /// Produce the save bytes. Unedited sections are copied verbatim (including parasite data,
    /// the inactive slot, sectors 28-31 and any RTC trailer); only modified sections get a new checksum.
    /// </summary>
    public byte[] Export()
    {
        for (int id = 0; id < MercurySaveLayout.SectionCount; id++)
        {
            if (!_dirty[id])
                continue;
            int off = _sectorOfId[id] * MercurySaveLayout.SectorSize;
            int size = MercurySaveLayout.SectionSizes[id];
            WriteU16(_data, off + MercurySaveLayout.SectionChecksumOffset, CalculateChecksum(_data, off, size));
        }
        return (byte[])_data.Clone();
    }

    // --- boxes ------------------------------------------------------------

    /// <summary>Get an independent copy of the Pokémon in the given box/slot (empty when the slot is empty).</summary>
    public MercuryPokemon GetBox(int box, int slot)
    {
        ValidateBox(box);
        ValidateSlot(slot);
        var (special, ids, offset) = BoxLocation(box);
        var bytes = ReadBlock(special, ids, offset + slot * MercurySaveLayout.BoxMonSize, MercurySaveLayout.BoxMonSize);
        return MercuryPokemon.FromBox(bytes);
    }

    /// <summary>Write a Pokémon into the given box/slot. An empty Pokémon clears the slot.</summary>
    public void SetBox(int box, int slot, MercuryPokemon mon)
    {
        ArgumentNullException.ThrowIfNull(mon);
        ValidateBox(box);
        ValidateSlot(slot);
        var (special, ids, offset) = BoxLocation(box);
        WriteBlock(special, ids, offset + slot * MercurySaveLayout.BoxMonSize, mon.ToBoxBytes());
    }

    /// <summary>Clear a box slot.</summary>
    public void ClearBox(int box, int slot)
    {
        ValidateBox(box);
        ValidateSlot(slot);
        var (special, ids, offset) = BoxLocation(box);
        WriteBlock(special, ids, offset + slot * MercurySaveLayout.BoxMonSize, new byte[MercurySaveLayout.BoxMonSize]);
    }

    private (bool Special, int[]? Ids, int Offset) BoxLocation(int box)
    {
        int stride = MercurySaveLayout.BoxRecordLength;
        if (box < 19)
            return (false, StorageIds, MercurySaveLayout.StorageBoxBase + (box * stride));
        if (box < 22)
            return (true, null, MercurySaveLayout.SpecialBoxBase + ((box - 19) * stride));
        if (box < 24)
            return (false, SaveBlock1Ids, MercurySaveLayout.SaveBlock1BoxBase + ((box - 22) * stride));
        return (false, SaveBlock2Ids, MercurySaveLayout.SaveBlock2BoxBase);
    }

    // --- party ------------------------------------------------------------

    /// <summary>Get an independent copy of a party Pokémon; slots at or beyond the party count are empty.</summary>
    public MercuryPokemon GetParty(int slot)
    {
        ValidateParty(slot);
        var bytes = ReadBlock(false, SaveBlock1Ids, MercurySaveLayout.PartyBaseOffset + (slot * MercurySaveLayout.PartyMonSize), MercurySaveLayout.PartyMonSize);
        return MercuryPokemon.FromParty(bytes);
    }

    /// <summary>Write a party Pokémon. The stored party count is only raised, never lowered.</summary>
    public void SetParty(int slot, MercuryPokemon mon)
    {
        ArgumentNullException.ThrowIfNull(mon);
        ValidateParty(slot);
        WriteBlock(false, SaveBlock1Ids, MercurySaveLayout.PartyBaseOffset + (slot * MercurySaveLayout.PartyMonSize), mon.ToPartyBytes());
        if (!mon.IsEmpty && slot >= PartyCount)
            WriteBlock(false, SaveBlock1Ids, MercurySaveLayout.PartyCountOffset, [(byte)(slot + 1)]);
    }

    /// <summary>Remove a party slot, shifting later members up and decrementing the party count.</summary>
    public void DeleteParty(int slot)
    {
        ValidateParty(slot);
        int count = PartyCount;
        if (slot >= count)
        {
            // Nothing stored there; make sure the slot is zeroed.
            WriteBlock(false, SaveBlock1Ids, MercurySaveLayout.PartyBaseOffset + (slot * MercurySaveLayout.PartyMonSize), new byte[MercurySaveLayout.PartyMonSize]);
            return;
        }

        for (int i = slot; i < count - 1; i++)
        {
            var next = ReadBlock(false, SaveBlock1Ids, MercurySaveLayout.PartyBaseOffset + ((i + 1) * MercurySaveLayout.PartyMonSize), MercurySaveLayout.PartyMonSize);
            WriteBlock(false, SaveBlock1Ids, MercurySaveLayout.PartyBaseOffset + (i * MercurySaveLayout.PartyMonSize), next);
        }
        WriteBlock(false, SaveBlock1Ids, MercurySaveLayout.PartyBaseOffset + ((count - 1) * MercurySaveLayout.PartyMonSize), new byte[MercurySaveLayout.PartyMonSize]);
        WriteBlock(false, SaveBlock1Ids, MercurySaveLayout.PartyCountOffset, [(byte)(count - 1)]);
    }

    // --- trainer ----------------------------------------------------------

    /// <summary>Get an independent copy of the trainer data.</summary>
    public MercuryTrainer GetTrainer()
    {
        var name = ReadBlock(false, SaveBlock2Ids, MercurySaveLayout.TrainerNameOffset, MercurySaveLayout.TrainerNameLength);
        uint key = ReadU32(_data, SectionOffset(0, MercurySaveLayout.EncryptionKeyOffset));
        uint money = ReadU32(ReadBlock(false, SaveBlock1Ids, MercurySaveLayout.MoneyOffset, 4), 0) ^ key;

        var trainer = new MercuryTrainer();
        trainer.NameBytes = name;
        trainer.Coins = ReadU32(_data, CoinsFileOffset);
        trainer.Gender = _data[SectionOffset(0, MercurySaveLayout.TrainerGenderOffset)];
        ushort tid = ReadU16(_data, SectionOffset(0, MercurySaveLayout.TrainerTidOffset));
        ushort sid = ReadU16(_data, SectionOffset(0, MercurySaveLayout.TrainerSidOffset));
        trainer.ID32 = ((uint)sid << 16) | tid;
        trainer.Money = money;
        trainer.PlayedHours = ReadU16(_data, SectionOffset(0, MercurySaveLayout.PlayTimeHoursOffset));
        trainer.PlayedMinutes = _data[SectionOffset(0, MercurySaveLayout.PlayTimeMinutesOffset)];
        trainer.PlayedSeconds = _data[SectionOffset(0, MercurySaveLayout.PlayTimeSecondsOffset)];
        return trainer;
    }

    /// <summary>Write the trainer data back into the active slot.</summary>
    public void SetTrainer(MercuryTrainer trainer)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        WriteBlock(false, SaveBlock2Ids, MercurySaveLayout.TrainerNameOffset, trainer.NameBytesRaw);
        WriteBlock(false, SaveBlock2Ids, MercurySaveLayout.TrainerGenderOffset, [trainer.Gender]);
        WriteBlock(false, SaveBlock2Ids, MercurySaveLayout.TrainerTidOffset, LittleEndian16((ushort)(trainer.ID32 & 0xFFFF)));
        WriteBlock(false, SaveBlock2Ids, MercurySaveLayout.TrainerSidOffset, LittleEndian16((ushort)(trainer.ID32 >> 16)));
        WriteBlock(false, SaveBlock2Ids, MercurySaveLayout.PlayTimeHoursOffset, LittleEndian16(trainer.PlayedHours));
        WriteBlock(false, SaveBlock2Ids, MercurySaveLayout.PlayTimeMinutesOffset, [trainer.PlayedMinutes]);
        WriteBlock(false, SaveBlock2Ids, MercurySaveLayout.PlayTimeSecondsOffset, [trainer.PlayedSeconds]);

        uint key = ReadU32(_data, SectionOffset(0, MercurySaveLayout.EncryptionKeyOffset));
        WriteBlock(false, SaveBlock1Ids, MercurySaveLayout.MoneyOffset, LittleEndian32(trainer.Money ^ key));
        // Coins live in section 13's parasite tail, outside the checksummed payload.
        WriteU32(_data, CoinsFileOffset, trainer.Coins);
        _anyEdit = true;
    }

    /// <summary>Coins (u32, no XOR) sit at section 13 + 0x7CC (parasite block + 0x37C).</summary>
    private int CoinsFileOffset => (_sectorOfId[13] * MercurySaveLayout.SectorSize) + MercurySaveLayout.CoinsFileOffset;

    /// <summary>
    /// Stat-scaling mode from the ROM flag/VAR pair: flag 0x930 at section 0 + 0xF2A bit 0,
    /// VAR 0x5018 (u16) at section 4 + 0xEFC. Returns 11, 12 or 13 when the flag is set, otherwise 0.
    /// </summary>
    public int StatMode
    {
        get
        {
            int flagByte = _data[(_sectorOfId[0] * MercurySaveLayout.SectorSize) + MercurySaveLayout.StatFlagFileOffset];
            if ((flagByte & 1) == 0)
                return 0;
            ushort value = ReadU16(_data, (_sectorOfId[4] * MercurySaveLayout.SectorSize) + MercurySaveLayout.StatVarFileOffset);
            return value is 11 or 12 or 13 ? value : 0;
        }
    }

    private static void WriteU32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static uint ReadU32(byte[] data, int offset) => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static void WriteU16(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)(value >> 8);
    }

    private static byte[] LittleEndian16(ushort value) => [(byte)(value & 0xFF), (byte)(value >> 8)];

    private static byte[] LittleEndian32(uint value) =>
        [(byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF), (byte)((value >> 24) & 0xFF)];

    // --- block IO ---------------------------------------------------------

    private byte[] ReadBlock(bool special, int[]? ids, int offset, int length)
    {
        var result = new byte[length];
        if (special)
        {
            ReadSpecial(offset, result);
            return result;
        }
        if (ids is null)
            throw new ArgumentNullException(nameof(ids));

        int position = offset;
        int written = 0;
        foreach (int id in ids)
        {
            int size = MercurySaveLayout.SectionSizes[id];
            if (position >= size)
            {
                position -= size;
                continue;
            }
            int take = Math.Min(size - position, length - written);
            int sector = _sectorOfId[id];
            Buffer.BlockCopy(_data, (sector * MercurySaveLayout.SectorSize) + position, result, written, take);
            written += take;
            position = 0;
            if (written == length)
                break;
        }
        if (written != length)
            throw new InvalidDataException($"Block read of {length} bytes at offset {offset} exceeds the block size.");
        return result;
    }

    private void WriteBlock(bool special, int[]? ids, int offset, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (special)
        {
            WriteSpecial(offset, bytes);
            _anyEdit = true;
            return;
        }
        if (ids is null)
            throw new ArgumentNullException(nameof(ids));

        int position = offset;
        int consumed = 0;
        foreach (int id in ids)
        {
            int size = MercurySaveLayout.SectionSizes[id];
            if (position >= size)
            {
                position -= size;
                continue;
            }
            int take = Math.Min(size - position, bytes.Length - consumed);
            int absolute = (_sectorOfId[id] * MercurySaveLayout.SectorSize) + position;
            Buffer.BlockCopy(bytes, consumed, _data, absolute, take);
            _dirty[id] = true;
            _anyEdit = true;
            consumed += take;
            position = 0;
            if (consumed == bytes.Length)
                break;
        }
        if (consumed != bytes.Length)
            throw new InvalidDataException($"Block write of {bytes.Length} bytes at offset {offset} exceeds the block size.");
    }

    /// <summary>Boxes 19-21 live in the concatenated data areas of sectors 30 and 31 (no section checksum).</summary>
    private void ReadSpecial(int offset, byte[] destination)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            int position = offset + i;
            if (position >= 2 * MercurySaveLayout.SectorDataSize)
                throw new InvalidDataException($"Special box area read out of range at {position}.");
            int sector = position < MercurySaveLayout.SectorDataSize ? MercurySaveLayout.SpecialSectorA : MercurySaveLayout.SpecialSectorB;
            int within = position % MercurySaveLayout.SectorDataSize;
            destination[i] = _data[(sector * MercurySaveLayout.SectorSize) + within];
        }
    }

    private void WriteSpecial(int offset, byte[] source)
    {
        for (int i = 0; i < source.Length; i++)
        {
            int position = offset + i;
            if (position >= 2 * MercurySaveLayout.SectorDataSize)
                throw new InvalidDataException($"Special box area write out of range at {position}.");
            int sector = position < MercurySaveLayout.SectorDataSize ? MercurySaveLayout.SpecialSectorA : MercurySaveLayout.SpecialSectorB;
            int within = position % MercurySaveLayout.SectorDataSize;
            _data[(sector * MercurySaveLayout.SectorSize) + within] = source[i];
        }
    }

    // --- validation -------------------------------------------------------

    private static void ValidateBox(int box)
    {
        if (box < 0 || box >= BoxCount)
            throw new ArgumentOutOfRangeException(nameof(box), box, $"Box must be 0-{BoxCount - 1}.");
    }

    private static void ValidateSlot(int slot)
    {
        if (slot < 0 || slot >= BoxCapacity)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Slot must be 0-{BoxCapacity - 1}.");
    }

    private static void ValidateParty(int slot)
    {
        if (slot < 0 || slot >= MaxPartySize)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Party slot must be 0-{MaxPartySize - 1}.");
    }

    private static ushort ReadU16(byte[] data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));

    private static ushort CalculateChecksum(byte[] data, int offset, int size)
    {
        uint sum = 0;
        for (int i = 0; i < size; i += 4)
            sum += (uint)(data[offset + i] | (data[offset + i + 1] << 8) | (data[offset + i + 2] << 16) | (data[offset + i + 3] << 24));
        return (ushort)(((sum >> 16) + (sum & 0xFFFF)) & 0xFFFF);
    }
}
