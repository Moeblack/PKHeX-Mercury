using System.Buffers.Binary;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Fixed ROM addresses and decoding keys for the supported Mercury 1.1 build.
/// Every value here is taken from ROM instruction/pointer evidence (see docs/mercury-rom-profile.md).
/// Addresses are GBA addresses; convert with <see cref="ToOffset"/> before indexing the ROM byte array.
/// </summary>
internal static class MercuryRomLayout
{
    /// <summary>SHA-256 of the one supported ROM build. Any other image is rejected.</summary>
    public const string ExpectedSha256 = "131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd";

    public const int RomSize = 0x2000000; // 32 MiB
    public const uint GbaBase = 0x08000000;
    public const uint GbaEnd = 0x0A000000;

    // --- pointer slots / code literals (see evidence) ---
    public const uint FrontPicSlot = 0x08000128;       // stored 0x070074DC; +0x027B3BC8 -> 0x097BB0A4 (code 0x08736DAC)
    public const uint FrontPicKeyA = 0xFA7B3BC8;       // first probe key (same family at 0x0736EB8)
    public const uint FrontPicKeyB = 0x027B3BC8;       // confirmed key
    public const uint BackPicSlot = 0x0800012C;        // -> 0x0976AF2C
    public const uint PaletteSlot = 0x08000130;        // -> 0x097D64CC
    public const uint ShinyPaletteSlot = 0x08000134;   // -> 0x097E49D4
    public const uint SpeciesNamesSlot = 0x08000144;   // -> 0x0941B350, stride 11
    public const uint MoveNamesSlot = 0x08000148;      // -> 0x09D8FC34, stride 13
    public const uint BaseStatsSlot = 0x080001BC;      // -> 0x0976DFBC, stride 28
    public const uint MovesLiteral = 0x08019544;       // ldr r2,[pc] -> 0x09DF68E3, stride 12
    public const uint AbilityNamesLiteral = 0x080D8624;// ldr r0,[pc] -> 0x09D87140, stride 13
    public const uint ItemPointerVar = 0x0809A8D8;     // stored 0x04E63D6A; +0x03964096 -> 0x087C7E00, stride 44
    public const uint ItemKey = 0x03964096;
    public const uint LevelUpPtrSlot = 0x08043E20;     // gLevelUpLearnsets pointer table, 1554 x u32
    public const uint TmhmLearnsetSlot = 0x08043C68;   // 1554 x u32[4] bitmap
    public const uint TutorLearnsetSlot = 0x08120C30;  // 1554 x u32[5] bitmap
    public const uint TmhmMovesSlot = 0x08125A8C;      // u16[128], 120 TM + 8 HM
    public const uint TutorMovesSlot = 0x08120BE4;     // u16[160]

    // Growth-rate experience tables, proven by the ROM consumer GetLevelFromBoxMonExp (0x0803E830):
    // 0x0803E850 ldr r6,[literal 0x0803E894]=0x09DFE8CC; growth index = base stats +0x13 (0x0803E85C);
    // row stride 0x400 (0x0803E85E..0x0803E862: movs r5,#0x20; lsls r5,#5); threshold read at [level*4],
    // loop level<=100. Six rows of 101 u32 (level 0..100). The legacy copy at 0x08253AE4 is NOT used.
    public const uint GrowthTables = 0x09DFE8CC;

    // --- table shapes ---
    public const int SpeciesCount = 1554;
    public const int SpeciesNameStride = 11;
    public const int MoveCount = 1015;
    public const int MoveNameStride = 13;
    public const int MoveStride = 12;
    public const int AbilityNameCount = 300;
    public const int AbilityNameStride = 13;
    public const int ItemCount = 750;
    public const int ItemStride = 44;
    public const int BaseStatsStride = 28;
    public const int GrowthStride = 0x400;
    public const int GrowthRateCount = 6;
    public const int MaxLevel = 100;

    // --- sprites ---
    public const int SpriteFrameBytes = 2048; // 64x64 4bpp
    public const int SpriteWidth = 64;
    public const int SpriteHeight = 64;
    public const int PaletteBytes = 32;       // 16 colours per page

    // --- ability name pool indices returned by the ROM alias function ---
    public const int AbilityAliasFirst = 256;
    public const int AbilityAliasLast = 299;

    public static bool IsRomAddress(uint value) => value >= GbaBase && value < GbaEnd;

    public static long ToOffset(uint gbaAddress) => (long)gbaAddress - GbaBase;

    public static bool TryReadU8(byte[] rom, uint gbaAddress, out byte value)
    {
        value = 0;
        long off = ToOffset(gbaAddress);
        if (off < 0 || off >= rom.Length)
            return false;
        value = rom[off];
        return true;
    }

    public static bool TryReadU16(byte[] rom, uint gbaAddress, out ushort value)
    {
        value = 0;
        long off = ToOffset(gbaAddress);
        if (off < 0 || off + 2 > rom.Length)
            return false;
        value = BinaryPrimitives.ReadUInt16LittleEndian(rom.AsSpan((int)off));
        return true;
    }

    public static bool TryReadU32(byte[] rom, uint gbaAddress, out uint value)
    {
        value = 0;
        long off = ToOffset(gbaAddress);
        if (off < 0 || off + 4 > rom.Length)
            return false;
        value = BinaryPrimitives.ReadUInt32LittleEndian(rom.AsSpan((int)off));
        return true;
    }

    public static bool TryReadU8Raw(byte[] rom, long offset, out byte value)
    {
        value = 0;
        if (offset < 0 || offset >= rom.Length)
            return false;
        value = rom[offset];
        return true;
    }

    public static bool TryReadU16Raw(byte[] rom, long offset, out ushort value)
    {
        value = 0;
        if (offset < 0 || offset + 2 > rom.Length)
            return false;
        value = BinaryPrimitives.ReadUInt16LittleEndian(rom.AsSpan((int)offset));
        return true;
    }

    public static bool TryReadU32Raw(byte[] rom, long offset, out uint value)
    {
        value = 0;
        if (offset < 0 || offset + 4 > rom.Length)
            return false;
        value = BinaryPrimitives.ReadUInt32LittleEndian(rom.AsSpan((int)offset));
        return true;
    }

    /// <summary>
    /// Reads a normalised pointer slot, mirroring the ROM's own branch: use the stored value
    /// directly when it already points into ROM, otherwise try the two known add keys in order.
    /// </summary>
    public static bool TryResolveSlot(byte[] rom, uint slotAddress, uint keyA, uint keyB, out uint target)
    {
        target = 0;
        if (!TryReadU32(rom, slotAddress, out uint stored))
            return false;
        if (IsRomAddress(stored))
        {
            target = stored;
            return true;
        }
        uint a = stored + keyA;
        if (IsRomAddress(a))
        {
            target = a;
            return true;
        }
        uint b = stored + keyB;
        if (IsRomAddress(b))
        {
            target = b;
            return true;
        }
        return false;
    }

    public static bool TryResolveSlot(byte[] rom, uint slotAddress, out uint target)
        => TryResolveSlot(rom, slotAddress, 0, 0, out target);

    /// <summary>Front picture table base, decoded through its slot (stored + 0x027B3BC8).</summary>
    public static bool TryGetFrontPicTable(byte[] rom, out uint table)
        => TryResolveSlot(rom, FrontPicSlot, FrontPicKeyA, FrontPicKeyB, out table);

    /// <summary>Item table base, decoded with the normalised-pointer rule.</summary>
    public static bool TryGetItemTable(byte[] rom, out uint table)
    {
        table = 0;
        if (!TryReadU32(rom, ItemPointerVar, out uint stored))
            return false;
        uint v = stored + ItemKey;
        if (!IsRomAddress(v))
            return false;
        table = v;
        return true;
    }
}
