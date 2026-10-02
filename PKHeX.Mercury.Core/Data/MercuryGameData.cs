using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Mercury species/move/item/ability name access plus sprite previews.
/// <para>
/// Three data sources are supported:
/// <list type="bullet">
/// <item><see cref="FromRom"/> reads the tables straight out of the user's own Mercury 1.1 ROM
/// (fixed SHA-256) and works offline. Chinese labels need an imported charmap; without one the built-in
/// GBA symbol table is used.</item>
/// <item><see cref="FromResearch"/> consumes an existing ROM-native research directory (verified via
/// its manifest hash) without re-extracting anything.</item>
/// <item><see cref="LoadProfile"/> re-loads a locally saved profile.</item>
/// </list>
/// No ROM, name table or charmap is embedded in this assembly; <see cref="NumericOnly"/> exposes
/// internal ids only.
/// </para>
/// </summary>
public sealed class MercuryGameData
{
    private readonly List<MercurySpecies> _species;
    private readonly List<MercuryMove> _moves;
    private readonly List<MercuryItem> _items;
    private readonly MercuryTextCodec _text;
    private readonly string[] _abilityNames;
    private readonly uint[][] _growth;
    private readonly byte[]? _rom;
    private readonly string _romSha256;
    private readonly string _source;

    private MercuryGameData(
        string romSha256,
        string source,
        List<MercurySpecies> species,
        List<MercuryMove> moves,
        List<MercuryItem> items,
        MercuryTextCodec text,
        string[] abilityNames,
        uint[][] growth,
        byte[]? rom)
    {
        _romSha256 = romSha256;
        _source = source;
        _species = species;
        _moves = moves;
        _items = items;
        _text = text;
        _abilityNames = abilityNames;
        _growth = growth;
        _rom = rom;
    }

    /// <summary>SHA-256 of the ROM this data was read from; empty for <see cref="NumericOnly"/>.</summary>
    public string RomSha256 => _romSha256;

    /// <summary>Where the data came from: "rom", "research", "profile" or "numeric".</summary>
    public string Source => _source;

    public IReadOnlyList<MercurySpecies> Species => _species;

    public IReadOnlyList<MercuryMove> Moves => _moves;

    public IReadOnlyList<MercuryItem> Items => _items;

    public MercuryTextCodec Text => _text;

    /// <summary>Ability name pool (300 entries when loaded, empty for numeric-only).</summary>
    public IReadOnlyList<string> AbilityNames => _abilityNames;

    /// <summary>True when growth experience tables are available (GetLevel/GetExperience work).</summary>
    public bool HasGrowthTables => _growth.Length == MercuryRomLayout.GrowthRateCount;

    /// <summary>True when a ROM is held for native sprite rendering.</summary>
    public bool HasSprites => _rom is not null;

    /// <summary>Ids only, no names. Useful before a ROM/profile is loaded.</summary>
    public static MercuryGameData NumericOnly()
    {
        var species = new List<MercurySpecies>(MercuryRomLayout.SpeciesCount);
        for (int i = 0; i < MercuryRomLayout.SpeciesCount; i++)
        {
            species.Add(new MercurySpecies
            {
                Id = i,
                Name = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                HasData = false,
            });
        }

        var moves = new List<MercuryMove>(MercuryRomLayout.MoveCount);
        for (int i = 0; i < MercuryRomLayout.MoveCount; i++)
        {
            moves.Add(new MercuryMove
            {
                Id = i,
                Name = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        var items = new List<MercuryItem>(MercuryRomLayout.ItemCount);
        for (int i = 0; i < MercuryRomLayout.ItemCount; i++)
        {
            items.Add(new MercuryItem
            {
                Id = i,
                EmbeddedId = i, // placeholder: no ROM data loaded
                Name = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        return new MercuryGameData(string.Empty, "numeric", species, moves, items, MercuryTextCodec.Default(), [], [], null);
    }

    /// <summary>
    /// Reads all data tables from the user's Mercury 1.1 ROM bytes. Rejects any other image with
    /// <see cref="InvalidDataException"/>. Supply an imported <paramref name="text"/> codec for Chinese labels.
    /// </summary>
    public static MercuryGameData FromRom(byte[] rom, MercuryTextCodec? text = null)
    {
        ArgumentNullException.ThrowIfNull(rom);
        if (rom.Length < MercuryRomLayout.RomSize)
            throw new InvalidDataException($"ROM is {rom.Length} bytes; the supported Mercury 1.1 image is {MercuryRomLayout.RomSize} bytes.");

        string sha = ComputeSha256(rom);
        if (!string.Equals(sha, MercuryRomLayout.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Unsupported ROM. Expected SHA-256 {MercuryRomLayout.ExpectedSha256} but got {sha}.");

        MercuryTextCodec codec = text ?? MercuryTextCodec.Default();
        string[] abilityNames = ReadAbilityNames(rom, codec);
        List<MercurySpecies> species = ReadSpecies(rom, codec);
        List<MercuryMove> moves = ReadMoves(rom, codec);
        List<MercuryItem> items = ReadItems(rom, codec);
        uint[][] growth = ReadGrowthTables(rom);

        return new MercuryGameData(sha, "rom", species, moves, items, codec, abilityNames, growth, rom);
    }

    /// <summary>
    /// Loads data from a ROM-native research directory (the folder containing <c>out/MANIFEST.json</c>,
    /// or that <c>out</c> folder itself). The manifest SHA-256 is verified before anything is used.
    /// </summary>
    public static MercuryGameData FromResearch(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        string outDir = ResolveResearchOut(root);

        string manifestPath = Path.Combine(outDir, "MANIFEST.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Research manifest not found: {manifestPath}", manifestPath);

        string? romPath;
        using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath)))
        {
            string? sha = GetString(manifest.RootElement, "input", "sha256");
            VerifyManifestSha(sha, manifestPath);
            romPath = GetString(manifest.RootElement, "input", "path");
        }

        string spriteManifestPath = Path.Combine(outDir, "sprites", "sprite_manifest.json");
        if (File.Exists(spriteManifestPath))
        {
            using JsonDocument spriteManifest = JsonDocument.Parse(File.ReadAllText(spriteManifestPath));
            string? sha = GetString(spriteManifest.RootElement, "input", "sha256");
            VerifyManifestSha(sha, spriteManifestPath);
        }

        MercuryTextCodec codec = ReadResearchCodec(outDir);
        string[] abilityNames = ReadResearchAbilityNames(outDir);
        Dictionary<(int Species, int Ability), int> aliases = ReadResearchAbilityAliases(outDir);
        int[] tmhmMoves = ReadResearchMoveList(outDir, "tmhm_moves.json");
        int[] tutorMoves = ReadResearchMoveList(outDir, "tutor_moves.json");
        byte[]? rom = TryLoadVerifiedRom(romPath);

        List<MercurySpecies> species = ReadResearchSpecies(outDir, aliases, tmhmMoves, tutorMoves);
        List<MercuryMove> moves = ReadResearchMoves(outDir);
        List<MercuryItem> items = ReadResearchItems(outDir);
        // Growth tables come only from the ROM's consumer-proven location; the HOME/legacy
        // experienceTables are never used as current-ROM values.
        uint[][] growth = rom is null ? [] : ReadGrowthTables(rom);

        string sha256 = MercuryRomLayout.ExpectedSha256;
        return new MercuryGameData(sha256, "research", species, moves, items, codec, abilityNames, growth, rom);
    }

    /// <summary>Loads a locally saved profile from the given directory.</summary>
    public static MercuryGameData LoadProfile(string directory)
    {
        MercuryProfile profile = MercuryProfile.Load(directory);

        MercuryTextCodec codec = profile.Charmap is { Count: > 0 }
            ? MercuryTextCodec.FromCharmapJson(JsonSerializer.Serialize(profile.Charmap))
            : MercuryTextCodec.Default();

        var species = new List<MercurySpecies>(profile.Species.Count);
        foreach (MercuryProfile.ProfileSpecies entry in profile.Species)
        {
            species.Add(new MercurySpecies
            {
                Id = entry.Id,
                Name = entry.Name,
                BaseStats = entry.BaseStats,
                GenderRatio = entry.GenderRatio,
                GrowthRate = entry.GrowthRate,
                BaseFriendship = entry.BaseFriendship,
                Abilities = entry.Abilities,
                AbilityNameIndices = entry.AbilityNameIndices,
                LevelUpMoves = entry.LevelUp.Select(pair => new MercuryLearnMove(pair[1], pair[0])).ToList(),
                MachineMoves = entry.Tmhm,
                TutorMoves = entry.Tutor,
                HasData = entry.HasData,
            });
        }

        var moves = new List<MercuryMove>(profile.Moves.Count);
        foreach (MercuryProfile.ProfileMove entry in profile.Moves)
        {
            moves.Add(new MercuryMove
            {
                Id = entry.Id,
                Name = entry.Name,
                Type = entry.Type,
                Power = entry.Power,
                PP = entry.PP,
                Accuracy = entry.Accuracy,
                Priority = entry.Priority,
            });
        }

        var items = new List<MercuryItem>(profile.Items.Count);
        foreach (MercuryProfile.ProfileItem entry in profile.Items)
        {
            items.Add(new MercuryItem
            {
                Id = entry.Id,
                EmbeddedId = entry.EmbeddedId,
                Name = entry.Name,
            });
        }

        byte[]? rom = null;
        if (!string.IsNullOrWhiteSpace(profile.RomPath))
        {
            string candidate = Path.IsPathRooted(profile.RomPath)
                ? profile.RomPath
                : Path.Combine(directory, profile.RomPath);
            rom = TryLoadVerifiedRom(candidate);
        }
        uint[][] growth = profile.Growth is { Length: MercuryRomLayout.GrowthRateCount }
            ? profile.Growth
            : (rom is null ? [] : ReadGrowthTables(rom));

        return new MercuryGameData(profile.RomSha256, "profile", species, moves, items, codec, profile.AbilityNames, growth, rom);
    }

    /// <summary>Writes this data (including the imported charmap, if any) to <paramref name="directory"/>.</summary>
    public void SaveProfile(string directory)
    {
        // Persist the ROM locally next to the profile when held, so sprites/growth still work after a
        // restart without depending on any absolute developer path. This cache is user-local only.
        string? romPath = null;
        if (_rom is not null)
        {
            Directory.CreateDirectory(directory);
            romPath = MercuryProfile.RomCacheFileName;
            File.WriteAllBytes(Path.Combine(directory, romPath), _rom);
        }

        var profile = new MercuryProfile
        {
            RomSha256 = _romSha256,
            Source = _source,
            RomPath = romPath,
            Charmap = _text.TryExportCharmap(),
            AbilityNames = _abilityNames,
            Growth = _growth,
            Species = _species.Select(s => new MercuryProfile.ProfileSpecies
            {
                Id = s.Id,
                Name = s.Name,
                BaseStats = s.BaseStats,
                GenderRatio = s.GenderRatio,
                GrowthRate = s.GrowthRate,
                BaseFriendship = s.BaseFriendship,
                Abilities = s.Abilities,
                AbilityNameIndices = s.AbilityNameIndices,
                LevelUp = s.LevelUpMoves.Select(m => new[] { m.Level, m.Move }).ToList(),
                Tmhm = [.. s.MachineMoves],
                Tutor = [.. s.TutorMoves],
                HasData = s.HasData,
            }).ToList(),
            Moves = _moves.Select(m => new MercuryProfile.ProfileMove
            {
                Id = m.Id,
                Name = m.Name,
                Type = m.Type,
                Power = m.Power,
                PP = m.PP,
                Accuracy = m.Accuracy,
                Priority = m.Priority,
            }).ToList(),
            Items = _items.Select(i => new MercuryProfile.ProfileItem
            {
                Id = i.Id,
                EmbeddedId = i.EmbeddedId,
                Name = i.Name,
            }).ToList(),
        };
        profile.Save(directory);
    }

    /// <summary>
    /// Returns an instance whose names are re-decoded with <paramref name="codec"/>. With a ROM held,
    /// the tables are re-read through <see cref="FromRom"/> so numeric rules stay identical. A
    /// numeric-only instance stays numeric-only (names need a ROM first); a research/profile instance
    /// without a ROM cannot re-decode and throws <see cref="NotSupportedException"/>.
    /// </summary>
    public MercuryGameData WithTextCodec(MercuryTextCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (_rom is not null)
            return FromRom(_rom, codec);
        if (_source == "numeric")
            return NumericOnly();
        throw new NotSupportedException("Cannot re-decode names without the ROM; load the Mercury ROM first.");
    }

    public MercurySpecies GetSpecies(int id)
    {
        if ((uint)id >= (uint)_species.Count)
            throw new ArgumentOutOfRangeException(nameof(id), id, $"Species id must be 0..{_species.Count - 1}.");
        return _species[id];
    }

    public string SpeciesName(int id)
        => (uint)id < (uint)_species.Count ? _species[id].Name : "?";

    public string MoveName(int id)
        => (uint)id < (uint)_moves.Count ? _moves[id].Name : "?";

    public string ItemName(int id)
        => (uint)id < (uint)_items.Count ? _items[id].Name : "?";

    /// <summary>Resolved display name for a species' ability slot (0 = first, 1 = second, 2 = hidden).</summary>
    public string AbilityName(int species, int abilitySlot)
    {
        if (_abilityNames.Length == 0)
            return string.Empty;
        if ((uint)species >= (uint)_species.Count || (uint)abilitySlot > 2)
            return "?";
        MercurySpecies entry = _species[species];
        if (entry.Abilities[abilitySlot] == 0)
            return string.Empty;
        int index = entry.AbilityNameIndices[abilitySlot];
        return (uint)index < (uint)_abilityNames.Length ? _abilityNames[index] : "?";
    }

    /// <summary>Highest level reached at <paramref name="exp"/> using the actual ROM growth table.</summary>
    public byte GetLevel(int species, uint exp)
    {
        uint[] row = GetGrowthRow(species);
        byte level = 1;
        for (int l = 1; l <= MercuryRomLayout.MaxLevel && l < row.Length; l++)
        {
            if (exp >= row[l])
                level = (byte)l;
            else
                break;
        }
        return level;
    }

    /// <summary>Total experience required to reach <paramref name="level"/> (clamped to 1..100).</summary>
    public uint GetExperience(int species, byte level)
    {
        uint[] row = GetGrowthRow(species);
        int l = Math.Clamp(level, 1, Math.Min(MercuryRomLayout.MaxLevel, row.Length - 1));
        return row[l];
    }

    /// <summary>
    /// Resolves the front-sprite resource index for a species/pid, applying the ROM's female-form
    /// overrides and the Xerneas form switch. <paramref name="runtimeState"/> maps the ROM runtime flag
    /// bit for species 0x338; null keeps the base species.
    /// </summary>
    public int GetSpriteIndex(int species, uint pid, bool? runtimeState = null)
    {
        const int Xerneas = 0x338;
        const int XerneasAlt = 0x44D;

        int s = species & 0xFFFF;
        int gender = GenderHelper(s, pid);
        if (gender == 0xFE)
        {
            return s switch
            {
                0x1F6 => 0x2E8,
                0x1F7 => 0x2E9,
                0x23E => 0x2BF,
                0x285 => 0x2C0,
                0x286 => 0x2C1,
                0x308 => 0x33F,
                _ => s,
            };
        }

        if (s == Xerneas && runtimeState == false)
            return XerneasAlt;
        return s;
    }

    /// <summary>
    /// Renders the native front sprite (first frame, first palette page) as RGBA8888 64x64.
    /// Returns null when the ROM is unavailable or the resource cannot be resolved.
    /// </summary>
    public byte[]? GetSpriteRgba(int species, uint pid, uint trainerId, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (_rom is null)
            return null;

        int index = GetSpriteIndex(species, pid);
        if (!MercurySpriteLoader.TryRender(_rom, index, pid, trainerId, out byte[] rgba))
            return null;

        width = MercurySpriteLoader.Width;
        height = MercurySpriteLoader.Height;
        return rgba;
    }

    private uint[] GetGrowthRow(int species)
    {
        if (!HasGrowthTables)
            throw new NotSupportedException("Growth experience tables are not loaded. Load the ROM or a profile containing them.");
        if ((uint)species >= (uint)_species.Count)
            throw new ArgumentOutOfRangeException(nameof(species), species, $"Species id must be 0..{_species.Count - 1}.");
        int rate = _species[species].GrowthRate;
        if ((uint)rate >= (uint)_growth.Length)
            throw new InvalidDataException($"Species {species} has growth rate {rate}, which is outside the loaded table.");
        return _growth[rate];
    }

    private int GenderHelper(int species, uint pid)
    {
        if ((uint)species >= (uint)_species.Count)
            return 0;
        byte ratio = _species[species].GenderRatio;
        if (ratio == 0)
            return 0;
        if (ratio >= 0xFE)
            return ratio;
        byte roll = (byte)(pid & 0xFF);
        return ratio > roll ? 0 : 0xFE;
    }

    // ---------------------------------------------------------------- ROM readers

    private static string[] ReadAbilityNames(byte[] rom, MercuryTextCodec codec)
    {
        if (!MercuryRomLayout.TryReadU32(rom, MercuryRomLayout.AbilityNamesLiteral, out uint baseAddress))
            return [];
        var names = new string[MercuryRomLayout.AbilityNameCount];
        for (int i = 0; i < names.Length; i++)
            names[i] = ReadName(rom, baseAddress, MercuryRomLayout.AbilityNameStride, i, codec) ?? string.Empty;
        return names;
    }

    private static List<MercurySpecies> ReadSpecies(byte[] rom, MercuryTextCodec codec)
    {
        if (!MercuryRomLayout.TryReadU32(rom, MercuryRomLayout.BaseStatsSlot, out uint statsBase) ||
            !MercuryRomLayout.TryReadU32(rom, MercuryRomLayout.SpeciesNamesSlot, out uint namesBase) ||
            !MercuryRomLayout.TryReadU32(rom, MercuryRomLayout.LevelUpPtrSlot, out uint levelUpPtrTable))
        {
            throw new InvalidDataException("Required species tables are missing from the ROM.");
        }

        uint[] tmhmMoves = ReadU16Table(rom, MercuryRomLayout.TmhmMovesSlot, 128);
        uint[] tutorMoves = ReadU16Table(rom, MercuryRomLayout.TutorMovesSlot, 160);
        uint[] tmhmSets = ReadPointerTable(rom, MercuryRomLayout.TmhmLearnsetSlot);
        uint[] tutorSets = ReadPointerTable(rom, MercuryRomLayout.TutorLearnsetSlot);

        long statsOffset = MercuryRomLayout.ToOffset(statsBase);
        long levelUpOffset = MercuryRomLayout.ToOffset(levelUpPtrTable);

        var species = new List<MercurySpecies>(MercuryRomLayout.SpeciesCount);
        for (int i = 0; i < MercuryRomLayout.SpeciesCount; i++)
        {
            long o = statsOffset + (MercuryRomLayout.BaseStatsStride * i);
            if (o < 0 || o + MercuryRomLayout.BaseStatsStride > rom.Length)
                throw new InvalidDataException("Base stats table is truncated.");

            int[] stats =
            [
                rom[o + 0], rom[o + 1], rom[o + 2], rom[o + 3], rom[o + 4], rom[o + 5],
            ];
            int[] abilities = [rom[o + 0x16], rom[o + 0x17], rom[o + 0x1A]];
            int[] abilityIndices =
            [
                MercuryAbilityAliases.Resolve(i, abilities[0]),
                MercuryAbilityAliases.Resolve(i, abilities[1]),
                MercuryAbilityAliases.Resolve(i, abilities[2]),
            ];

            string name = ReadName(rom, namesBase, MercuryRomLayout.SpeciesNameStride, i, codec) ?? string.Empty;
            IReadOnlyList<MercuryLearnMove> levelUp = ReadLevelUpMoves(rom, levelUpOffset, i);
            IReadOnlyList<int> machine = ReadBitmapMoves(rom, tmhmSets, tileCount: 4, bitCount: 128, i, tmhmMoves);
            IReadOnlyList<int> tutor = ReadBitmapMoves(rom, tutorSets, tileCount: 5, bitCount: 160, i, tutorMoves);

            species.Add(new MercurySpecies
            {
                Id = i,
                Name = name,
                BaseStats = stats,
                GenderRatio = rom[o + 0x10],
                GrowthRate = rom[o + 0x13],
                BaseFriendship = rom[o + 0x12],
                Abilities = abilities,
                AbilityNameIndices = abilityIndices,
                LevelUpMoves = levelUp,
                MachineMoves = machine,
                TutorMoves = tutor,
                HasData = stats.Any(v => v != 0),
            });
        }
        return species;
    }

    private static List<MercuryMove> ReadMoves(byte[] rom, MercuryTextCodec codec)
    {
        if (!MercuryRomLayout.TryReadU32(rom, MercuryRomLayout.MovesLiteral, out uint movesBase) ||
            !MercuryRomLayout.TryReadU32(rom, MercuryRomLayout.MoveNamesSlot, out uint namesBase))
        {
            throw new InvalidDataException("Required move tables are missing from the ROM.");
        }

        long movesOffset = MercuryRomLayout.ToOffset(movesBase);
        var moves = new List<MercuryMove>(MercuryRomLayout.MoveCount);
        for (int i = 0; i < MercuryRomLayout.MoveCount; i++)
        {
            long o = movesOffset + (MercuryRomLayout.MoveStride * i);
            if (o < 0 || o + MercuryRomLayout.MoveStride > rom.Length)
                throw new InvalidDataException("Move table is truncated.");

            moves.Add(new MercuryMove
            {
                Id = i,
                Name = ReadName(rom, namesBase, MercuryRomLayout.MoveNameStride, i, codec) ?? string.Empty,
                Power = rom[o + 1],
                Type = rom[o + 2],
                Accuracy = rom[o + 3],
                PP = rom[o + 4],
                Priority = (sbyte)rom[o + 7],
            });
        }
        return moves;
    }

    private static List<MercuryItem> ReadItems(byte[] rom, MercuryTextCodec codec)
    {
        if (!MercuryRomLayout.TryGetItemTable(rom, out uint tableBase))
            throw new InvalidDataException("The item table pointer could not be decoded from the ROM.");

        long tableOffset = MercuryRomLayout.ToOffset(tableBase);
        var items = new List<MercuryItem>(MercuryRomLayout.ItemCount);
        for (int i = 0; i < MercuryRomLayout.ItemCount; i++)
        {
            long o = tableOffset + (MercuryRomLayout.ItemStride * i);
            if (o < 0 || o + MercuryRomLayout.ItemStride > rom.Length)
                throw new InvalidDataException("Item table is truncated.");

            ushort embedded = (ushort)(rom[o + 0x0E] | (rom[o + 0x0F] << 8));
            string name = codec.Decode(rom.AsSpan((int)o, 14));
            items.Add(new MercuryItem
            {
                Id = i,
                EmbeddedId = embedded,
                Name = name,
            });
        }
        return items;
    }

    private static uint[][] ReadGrowthTables(byte[] rom)
    {
        // Real tables per the ROM consumer GetLevelFromBoxMonExp (0x0803E830): base 0x09DFE8CC,
        // 6 growth rows, 0x400-byte physical stride, threshold[level] for level 0..100. Values are read
        // verbatim and validated against the known level-100 totals and monotonicity.
        long baseOffset = MercuryRomLayout.ToOffset(MercuryRomLayout.GrowthTables);
        uint[][] rows = new uint[MercuryRomLayout.GrowthRateCount][];
        for (int r = 0; r < rows.Length; r++)
        {
            rows[r] = new uint[MercuryRomLayout.MaxLevel + 1];
            for (int l = 0; l <= MercuryRomLayout.MaxLevel; l++)
            {
                long o = baseOffset + (r * MercuryRomLayout.GrowthStride) + (4L * l);
                if (!MercuryRomLayout.TryReadU32Raw(rom, o, out uint value))
                    return [];
                rows[r][l] = value;
            }
        }

        // Validate against the known level-100 totals and monotonicity; otherwise treat as absent.
        uint[] totals = [1_000_000, 600_000, 1_640_000, 1_059_860, 800_000, 1_250_000];
        for (int r = 0; r < rows.Length; r++)
        {
            if (rows[r][MercuryRomLayout.MaxLevel] != totals[r])
                return [];
            for (int l = 1; l <= MercuryRomLayout.MaxLevel; l++)
            {
                if (rows[r][l] < rows[r][l - 1])
                    return [];
            }
        }
        return rows;
    }

    private static IReadOnlyList<MercuryLearnMove> ReadLevelUpMoves(byte[] rom, long levelUpPtrOffset, int species)
    {
        if (!MercuryRomLayout.TryReadU32Raw(rom, levelUpPtrOffset + (4L * species), out uint pointer))
            return [];
        if (!MercuryRomLayout.IsRomAddress(pointer))
            return [];

        long o = MercuryRomLayout.ToOffset(pointer);
        var result = new List<MercuryLearnMove>();
        for (int guard = 0; guard < 4096; guard++)
        {
            if (o + 3 > rom.Length)
                break;
            ushort move = (ushort)(rom[o] | (rom[o + 1] << 8));
            byte level = rom[o + 2];
            if (move == 0 && level == 0xFF)
                break;
            result.Add(new MercuryLearnMove(move, level));
            o += 3;
        }
        return result;
    }

    private static IReadOnlyList<int> ReadBitmapMoves(byte[] rom, uint[] setTable, int tileCount, int bitCount, int species, uint[] moves)
    {
        if (setTable.Length == 0)
            return [];
        long o = MercuryRomLayout.ToOffset(setTable[0]) + ((long)tileCount * 4 * species);
        if (o < 0 || o + (tileCount * 4) > rom.Length)
            return [];

        var result = new List<int>();
        for (int bit = 0; bit < bitCount; bit++)
        {
            int word = bit / 32;
            int shift = bit % 32;
            uint value = (uint)(rom[(int)o + (word * 4)] | (rom[(int)o + (word * 4) + 1] << 8) |
                                (rom[(int)o + (word * 4) + 2] << 16) | (rom[(int)o + (word * 4) + 3] << 24));
            if ((value & (1u << shift)) == 0)
                continue;
            if ((uint)bit < (uint)moves.Length)
                result.Add((int)moves[bit]);
        }
        return result;
    }

    private static uint[] ReadU16Table(byte[] rom, uint slot, int count)
    {
        if (!MercuryRomLayout.TryReadU32(rom, slot, out uint baseAddress))
            return [];
        long o = MercuryRomLayout.ToOffset(baseAddress);
        if (o < 0 || o + (count * 2) > rom.Length)
            return [];
        var table = new uint[count];
        for (int i = 0; i < count; i++)
            table[i] = (uint)(rom[(int)o + (2 * i)] | (rom[(int)o + (2 * i) + 1] << 8));
        return table;
    }

    /// <summary>Reads a pointer slot into a single-element array (index 0 is the table base).</summary>
    private static uint[] ReadPointerTable(byte[] rom, uint slot)
    {
        if (!MercuryRomLayout.TryReadU32(rom, slot, out uint baseAddress))
            return [];
        return [baseAddress];
    }

    private static string? ReadName(byte[] rom, uint baseAddress, int stride, int index, MercuryTextCodec codec)
    {
        long o = MercuryRomLayout.ToOffset(baseAddress) + ((long)stride * index);
        if (o < 0 || o + stride > rom.Length)
            return null;
        return codec.Decode(rom.AsSpan((int)o, stride));
    }

    // ---------------------------------------------------------------- research readers

    private static string ResolveResearchOut(string root)
    {
        string nested = Path.Combine(root, "out");
        if (File.Exists(Path.Combine(nested, "MANIFEST.json")))
            return nested;
        if (File.Exists(Path.Combine(root, "MANIFEST.json")))
            return root;
        return nested;
    }

    private static void VerifyManifestSha(string? sha, string path)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new InvalidDataException($"Manifest {path} does not declare a ROM sha256.");
        if (!string.Equals(sha, MercuryRomLayout.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Manifest {path} was produced from a different ROM (sha256 {sha}); expected {MercuryRomLayout.ExpectedSha256}.");
    }

    private static byte[]? TryLoadVerifiedRom(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        try
        {
            byte[] rom = File.ReadAllBytes(path);
            return string.Equals(ComputeSha256(rom), MercuryRomLayout.ExpectedSha256, StringComparison.OrdinalIgnoreCase)
                ? rom
                : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static MercuryTextCodec ReadResearchCodec(string outDir)
    {
        string charmap = Path.Combine(outDir, "charmap.json");
        return File.Exists(charmap)
            ? MercuryTextCodec.FromCharmapJson(File.ReadAllText(charmap))
            : MercuryTextCodec.Default();
    }

    private static string[] ReadResearchAbilityNames(string outDir)
    {
        string path = Path.Combine(outDir, "ability_names.json");
        if (!File.Exists(path))
            return [];
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
    }

    private static Dictionary<(int Species, int Ability), int> ReadResearchAbilityAliases(string outDir)
    {
        string path = Path.Combine(outDir, "ability_display_names.json");
        var map = new Dictionary<(int Species, int Ability), int>();
        if (!File.Exists(path))
            return map;

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement record in document.RootElement.EnumerateArray())
        {
            if (!record.TryGetProperty("species", out JsonElement speciesElement) ||
                !record.TryGetProperty("ability_id", out JsonElement abilityElement) ||
                !record.TryGetProperty("name_index", out JsonElement indexElement))
            {
                continue;
            }
            if (indexElement.ValueKind == JsonValueKind.Null)
                continue;
            map[(speciesElement.GetInt32(), abilityElement.GetInt32())] = indexElement.GetInt32();
        }
        return map;
    }

    private static int[] ReadResearchMoveList(string outDir, string fileName)
    {
        string path = Path.Combine(outDir, fileName);
        if (!File.Exists(path))
            return [];
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateArray().Select(e => e.GetInt32()).ToArray();
    }

    private static void AddMappedMoves(JsonElement slots, int[] table, List<int> target)
    {
        foreach (JsonElement slotElement in slots.EnumerateArray())
        {
            int slot = slotElement.GetInt32();
            target.Add((uint)slot < (uint)table.Length ? table[slot] : slot);
        }
    }

    private static List<MercurySpecies> ReadResearchSpecies(
        string outDir,
        Dictionary<(int Species, int Ability), int> aliases,
        int[] tmhmMoves,
        int[] tutorMoves)
    {
        string path = Path.Combine(outDir, "species.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        var species = new List<MercurySpecies>(MercuryRomLayout.SpeciesCount);

        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            int id = entry.TryGetProperty("index", out JsonElement indexElement) ? indexElement.GetInt32() : species.Count;
            // The research JSON stores names already decoded to Unicode; use them directly.
            string name = entry.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            JsonElement stats = entry.GetProperty("baseStats");
            int[] baseStats =
            [
                stats.GetProperty("hp").GetInt32(),
                stats.GetProperty("atk").GetInt32(),
                stats.GetProperty("def").GetInt32(),
                stats.GetProperty("spe").GetInt32(),
                stats.GetProperty("spa").GetInt32(),
                stats.GetProperty("spd").GetInt32(),
            ];

            JsonElement abilityElement = entry.GetProperty("abilities");
            int[] abilities =
            [
                abilityElement.GetProperty("ability1").GetInt32(),
                abilityElement.GetProperty("ability2").GetInt32(),
                abilityElement.GetProperty("hiddenAbility").GetInt32(),
            ];
            int[] indices = new int[3];
            for (int slot = 0; slot < 3; slot++)
            {
                int ability = abilities[slot];
                indices[slot] = ability == 0
                    ? 0
                    : aliases.TryGetValue((id, ability), out int resolved) ? resolved : ability;
            }

            var levelUp = new List<MercuryLearnMove>();
            if (entry.TryGetProperty("levelUpMoves", out JsonElement levelUpElement))
            {
                foreach (JsonElement pair in levelUpElement.EnumerateArray())
                    levelUp.Add(new MercuryLearnMove(pair[1].GetInt32(), pair[0].GetInt32()));
            }

            // The research product stores TM/tutor slot indices; resolve them to move ids.
            var machine = new List<int>();
            if (entry.TryGetProperty("tmhm", out JsonElement tmhmElement))
                AddMappedMoves(tmhmElement, tmhmMoves, machine);

            var tutor = new List<int>();
            if (entry.TryGetProperty("tutor", out JsonElement tutorElement))
                AddMappedMoves(tutorElement, tutorMoves, tutor);

            species.Add(new MercurySpecies
            {
                Id = id,
                Name = name,
                BaseStats = baseStats,
                GenderRatio = (byte)stats.GetProperty("genderRatio").GetInt32(),
                GrowthRate = (byte)stats.GetProperty("growthRate").GetInt32(),
                BaseFriendship = (byte)stats.GetProperty("friendship").GetInt32(),
                Abilities = abilities,
                AbilityNameIndices = indices,
                LevelUpMoves = levelUp,
                MachineMoves = machine,
                TutorMoves = tutor,
                HasData = baseStats.Any(v => v != 0),
            });
        }

        return species;
    }

    private static List<MercuryMove> ReadResearchMoves(string outDir)
    {
        string movesPath = Path.Combine(outDir, "moves.json");
        string namesPath = Path.Combine(outDir, "move_names.json");
        using JsonDocument movesDocument = JsonDocument.Parse(File.ReadAllText(movesPath));
        string[] names;
        if (File.Exists(namesPath))
        {
            using JsonDocument namesDocument = JsonDocument.Parse(File.ReadAllText(namesPath));
            names = namesDocument.RootElement.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
        }
        else
        {
            names = [];
        }

        var moves = new List<MercuryMove>(MercuryRomLayout.MoveCount);
        int index = 0;
        foreach (JsonElement entry in movesDocument.RootElement.EnumerateArray())
        {
            moves.Add(new MercuryMove
            {
                Id = index,
                Name = index < names.Length ? names[index] : index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Type = (byte)entry.GetProperty("type").GetInt32(),
                Power = (byte)entry.GetProperty("power").GetInt32(),
                PP = (byte)entry.GetProperty("pp").GetInt32(),
                Accuracy = (byte)entry.GetProperty("accuracy").GetInt32(),
                Priority = (sbyte)entry.GetProperty("priority").GetInt32(),
            });
            index++;
        }
        return moves;
    }

    private static List<MercuryItem> ReadResearchItems(string outDir)
    {
        string path = Path.Combine(outDir, "items.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        var items = new List<MercuryItem>(MercuryRomLayout.ItemCount);
        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            items.Add(new MercuryItem
            {
                Id = entry.GetProperty("index").GetInt32(),
                EmbeddedId = entry.GetProperty("itemId").GetInt32(),
                Name = entry.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
            });
        }
        return items;
    }

    // ---------------------------------------------------------------- helpers

    private static string? GetString(JsonElement root, string parent, string child)
    {
        if (!root.TryGetProperty(parent, out JsonElement p) || !p.TryGetProperty(child, out JsonElement c))
            return null;
        return c.ValueKind == JsonValueKind.String ? c.GetString() : null;
    }

    private static string ComputeSha256(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}
