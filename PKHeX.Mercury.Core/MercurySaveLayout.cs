namespace PKHeX.Mercury.Core;

/// <summary>
/// Raw layout constants for the Mercury 1.1 (BPRE-based) save container and Pokémon records.
/// All values are derived from instructions in the exact ROM build
/// 131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd; see docs/mercury-save-format.md.
/// </summary>
internal static class MercurySaveLayout
{
    // --- save container ---------------------------------------------------
    public const int SectorSize = 0x1000;
    public const int SectorDataSize = 0xFF0;
    public const int SectionCount = 14;
    public const int SlotCount = 2;
    public const int SectorCount = 32;
    public const int SaveBlockSize = SectorCount * SectorSize; // 0x20000

    public const uint SectionSignature = 0x08012025;
    public const int SectionIdOffset = 0xFF4;
    public const int SectionChecksumOffset = 0xFF6;
    public const int SectionSignatureOffset = 0xFF8;
    public const int SectionCounterOffset = 0xFFC;

    /// <summary>Logical payload size of each section id (ROM @0x0804C16C/0x174/0x180 block sizes).</summary>
    public static readonly int[] SectionSizes =
    [
        0xF24, // 0 SaveBlock2
        0xFF0, 0xFF0, 0xFF0, 0xD98, // 1-4 SaveBlock1 (0x3D68 total)
        0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0x450, // 5-13 PokemonStorage (0x83D0 total)
    ];

    /// <summary>Sector 30/31 hold boxes 19-21 (ROM @0x09D56D08 reads sector 0x1E/0x1F).</summary>
    public const int SpecialSectorA = 30;
    public const int SpecialSectorB = 31;

    // --- Pokémon ----------------------------------------------------------
    public const int BoxCount = 25;
    public const int BoxCapacity = 30;
    public const int BoxMonSize = 58;
    public const int PartyCount = 6;
    public const int PartyMonSize = 100;

    // Box 0-18 live in PokemonStorage at offset 4 (ROM: box0 ptr 0x02029318 = gPokemonStorage(0x02029314)+4).
    public const int StorageBoxBase = 4;
    // Box 19-21 live in the concatenation of sectors 30/31 at offset 0xB0C (ROM: box19 ptr 0x0203CB44 - 0x0203C038).
    public const int SpecialBoxBase = 0xB0C;
    // Box 22-23 live in SaveBlock1 at 0x1F08 (ROM: box22 ptr 0x02027434 = gSaveBlock1(0x0202552C)+0x1F08).
    public const int SaveBlock1BoxBase = 0x1F08;
    // Box 24 lives in SaveBlock2 at 0xB0 (ROM: box24 ptr 0x02024638 = gSaveBlock2(0x02024588)+0xB0).
    public const int SaveBlock2BoxBase = 0xB0;

    public static int BoxRecordLength => BoxCapacity * BoxMonSize; // 1740

    // Party: count @ SaveBlock1+0x34, records @ SaveBlock1+0x38 (standard FRLG struct, verified against real saves).
    public const int PartyCountOffset = 0x34;
    public const int PartyBaseOffset = 0x38;

    // Trainer (SaveBlock2), standard FRLG struct, verified against real saves.
    public const int TrainerNameOffset = 0x00;
    public const int TrainerNameLength = 8;
    public const int TrainerGenderOffset = 0x08;
    public const int TrainerTidOffset = 0x0A; // u16
    public const int TrainerSidOffset = 0x0C; // u16
    public const int PlayTimeHoursOffset = 0x0E; // u16
    public const int PlayTimeMinutesOffset = 0x10;
    public const int PlayTimeSecondsOffset = 0x11;
    public const int EncryptionKeyOffset = 0xF20; // u32

    // Money: SaveBlock1+0x290 (u32) XOR encryption key (ROM @0x09D3D004/0x014/0x028 + GetMoney 0x0809FD58).
    public const int MoneyOffset = 0x290;
    // Coins: u32 inside section 13's parasite tail (RAM 0x0203B814 = parasite block 0x0203B498 + 0x37C).
    public const int CoinsFileOffset = 0x7CC; // 0x450 payload + 0x37C

    // Stat-scaling mode flag/VAR inside section 0 / section 4 parasite tails.
    public const int StatFlagFileOffset = 0xF2A; // 0xF24 payload + 6 (flag 0x930)
    public const int StatVarFileOffset = 0xEFC;  // 0xD98 payload + 0x164 (VAR 0x5018)

    // --- box/party sub-layout (offsets inside the 58-byte box record) -----
    public const int BoxPid = 0x00;
    public const int BoxOtId = 0x04;
    public const int BoxNickname = 0x08;
    public const int BoxNicknameLength = 10;
    public const int BoxLanguage = 0x12;
    public const int BoxFlags = 0x13;
    public const int BoxOtName = 0x14;
    public const int BoxOtNameLength = 7;
    public const int BoxMarkings = 0x1B;
    public const int BoxSpecies = 0x1C;
    public const int BoxHeldItem = 0x1E;
    public const int BoxExperience = 0x20;
    public const int BoxPpUps = 0x24;
    public const int BoxFriendship = 0x25;
    // Ball is a full byte at box 0x26 / expanded 0x2A (ROM field 0x26 -> 0x09CCE574 -> 0x09D069A0 ldrb [G+0xA]).
    public const int BoxBall = 0x26;
    public const int BoxMovesPacked = 0x27; // 5 bytes, 4x10-bit
    public const int BoxEvs = 0x2C; // 6 bytes HP,Atk,Def,Spe,SpA,SpD
    public const int BoxPokerus = 0x32;
    public const int BoxMetLocation = 0x33;
    public const int BoxOrigins = 0x34; // u16
    public const int BoxIvs = 0x36; // u32; bits 0-29 IVs, bit 30 unused, bit 31 hidden ability

    // --- expanded (in-RAM / party) 80-byte sub-layout --------------------
    public const int ExTypeOverride = 0x1E;
    public const int ExSpecies = 0x20;
    public const int ExHeldItem = 0x22;
    public const int ExExperience = 0x24;
    public const int ExPpBonuses = 0x28;
    public const int ExFriendship = 0x29;
    public const int ExBall = 0x2A;
    public const int ExMoves = 0x2C;
    public const int ExCurrentPp = 0x34;
    public const int ExEvs = 0x38;
    public const int ExContest = 0x3E;
    public const int ExPokerus = 0x44;
    public const int ExMetLocation = 0x45;
    public const int ExOrigins = 0x46;
    public const int ExIvs = 0x48;
    public const int ExRibbons = 0x4C;

    // --- party tail (100-byte record, offsets 0x50-0x63) -----------------
    public const int PartyStatus = 0x50;
    public const int PartyLevel = 0x54;
    public const int PartyMail = 0x55;
    public const int PartyHp = 0x56;
    public const int PartyMaxHp = 0x58;
    public const int PartyAtk = 0x5A;
    public const int PartyDef = 0x5C;
    public const int PartySpe = 0x5E;
    public const int PartySpA = 0x60;
    public const int PartySpD = 0x62;
    public const int PartyTail = 0x50;
    public const int PartyTailLength = 20;

    /// <summary>IV word bit 30 is the egg flag (ROM MON_DATA_IS_EGG field 0x2D -> 0x08040154).</summary>
    public const uint IvEggFlag = 0x4000_0000u;

    /// <summary>IV word bit 31 is the hidden-ability marker (ROM field 0x2E -> 0x080400FA).</summary>
    public const uint IvHiddenAbilityFlag = 0x8000_0000u;

    /// <summary>Flags byte bit 0-2 (standard Gen3).</summary>
    public const int FlagBadEgg = 1 << 0;
    public const int FlagHasSpecies = 1 << 1;
    public const int FlagIsEgg = 1 << 2;
    public const int FlagBits = 0b111;

    /// <summary>Valid type values accepted by the Mercury type-override field (ROM table @0x09DDA014).</summary>
    public static bool IsValidType(int t) => t switch
    {
        >= 0 and <= 8 => true,
        >= 10 and <= 17 => true,
        23 or 24 => true,
        _ => false,
    };
}
