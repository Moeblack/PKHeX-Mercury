using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using PKHeX.Mercury.Core;

namespace PKHeX.Mercury.Tests;

internal static class Program
{
    private static readonly List<object> Results = [];
    private static readonly int[] SectionSizes = [0xF24, 0xFF0, 0xFF0, 0xFF0, 0xD98, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0, 0x450];
    private const uint Signature = 0x08012025;
    private static int failures;

    public static int Main(string[] args)
    {
        Run("unchanged 128KiB and RTC-tail saves are byte-identical", () =>
        {
            foreach (bool trailer in new[] { false, true })
            {
                var input = Fixture(trailer: trailer);
                var save = MercurySave.Load(input);
                Equal(9u, save.SaveCounter);
                Equal(1, save.ActiveSlot);
                Check(!save.IsDirty, "newly loaded save is not dirty");
                Bytes(input, save.Export());
            }
        });
        Run("all 750 slots and split-sector boundaries", () =>
        {
            var input = Fixture(trailer: true);
            var save = MercurySave.Load(input);
            for (int box = 0; box < 25; box++)
                for (int slot = 0; slot < 30; slot++)
                {
                    var mon = ExampleMon();
                    mon.PID = (uint)(0x10000000 + box * 30 + slot);
                    mon.Experience = (uint)(box * 30000 + slot * 100);
                    save.SetBox(box, slot, mon);
                }
            Check(save.IsDirty, "edits mark document dirty");
            var output = save.Export();
            var loaded = MercurySave.Load(output);
            for (int box = 0; box < 25; box++)
                for (int slot = 0; slot < 30; slot++)
                {
                    var mon = loaded.GetBox(box, slot);
                    Equal((uint)(0x10000000 + box * 30 + slot), mon.PID);
                    Equal((uint)(box * 30000 + slot * 100), mon.Experience);
                    Equal((ushort)1253, mon.Species);
                    Equal((ushort)1014, mon.Moves[2]);
                }
            Bytes(input.AsSpan(0, 14 * 0x1000), output.AsSpan(0, 14 * 0x1000));
            Bytes(input.AsSpan(28 * 0x1000, 2 * 0x1000), output.AsSpan(28 * 0x1000, 2 * 0x1000));
            Bytes(input.AsSpan(30 * 0x1000 + 0xFF0, 16), output.AsSpan(30 * 0x1000 + 0xFF0, 16));
            Bytes(input.AsSpan(31 * 0x1000 + 0xFF0, 16), output.AsSpan(31 * 0x1000 + 0xFF0, 16));
            Bytes(input.AsSpan(0x20000), output.AsSpan(0x20000));
            foreach (int id in new[] { 0, 4, 13 })
            {
                int offset = SectorFor(1, id) * 0x1000 + SectionSizes[id];
                Bytes(input.AsSpan(offset, 0xFF0 - SectionSizes[id]), output.AsSpan(offset, 0xFF0 - SectionSizes[id]));
            }
            VerifyChecksums(output);
        });
        Run("single-slot change touches only entity and its section checksum", () =>
        {
            var input = Fixture();
            var save = MercurySave.Load(input);
            save.SetBox(0, 0, ExampleMon());
            var output = save.Export();
            int start = SectorFor(1, 5) * 0x1000;
            for (int i = 0; i < input.Length; i++)
                if (input[i] != output[i])
                    Check((i >= start + 4 && i < start + 4 + 58) || i == start + 0xFF6 || i == start + 0xFF7, $"unexpected change at {i:X}");
            VerifyChecksums(output);
        });
        Run("move packing, IV/EV boundaries, egg/ability bits and cloning", () =>
        {
            var mon = ExampleMon();
            mon.Moves = [1023, 0, 1, 1014];
            mon.IVs = [31, 0, 1, 30, 15, 16];
            mon.EVs = [252, 252, 6, 0, 0, 0];
            mon.PPUps = [3, 0, 2, 1];
            mon.IsEgg = true;
            mon.HiddenAbility = true;
            var loaded = MercuryPokemon.FromBox(mon.ToBoxBytes());
            Check(loaded.Moves.SequenceEqual(mon.Moves), "all four 10-bit slots preserved");
            Check(loaded.IVs.SequenceEqual(mon.IVs), "IV fields preserved");
            Check(loaded.EVs.SequenceEqual(mon.EVs), "EV fields preserved");
            Check(loaded.PPUps.SequenceEqual(mon.PPUps), "PP-up fields preserved");
            Check(loaded.IsEgg && loaded.HiddenAbility, "high IV flags preserved");
            loaded.IVs = [1, 2, 3, 4, 5, 6];
            Check(loaded.IsEgg && loaded.HiddenAbility, "IV edit preserves high flags");
            var clone = loaded.Clone();
            clone.Species = 25;
            Equal((ushort)1253, loaded.Species);
            Equal(58, loaded.ToBoxBytes().Length);
            Throws(() => MercuryPokemon.FromBox(new byte[57]));
            Throws(() => MercuryPokemon.FromBox(new byte[59]));
            Throws(() => loaded.Moves = [1024, 0, 0, 0]);
            Throws(() => loaded.IVs = [32, 0, 0, 0, 0, 0]);
        });
        Run("PID generation preserves nature, gender, shiny and ability constraints", () =>
        {
            foreach (bool shiny in new[] { false, true })
                foreach (int gender in new[] { 0, 1 })
                    foreach (int ability in new[] { 0, 1, 2 })
                    {
                        var mon = ExampleMon();
                        mon.SetPersonality(13, gender, 127, shiny, ability);
                        Equal(13, mon.Nature);
                        Equal(gender, (mon.PID & 255) < 127 ? 1 : 0);
                        Equal(shiny, mon.IsShiny);
                        Equal(ability, mon.AbilitySlot);
                    }
            var impossible = ExampleMon();
            Throws(() => impossible.SetPersonality(0, 1, 0, false, 0));
        });
        Run("invalid, torn and corrupted save slots are not silently accepted", () =>
        {
            Throws(() => MercurySave.Load(new byte[10]));
            Throws(() => MercurySave.Load(new byte[0x20000]));
            var corrupted = Fixture();
            corrupted[SectorFor(1, 5) * 0x1000 + 100] ^= 1;
            var fallback = MercurySave.Load(corrupted);
            Equal(8u, fallback.SaveCounter);
            Check(fallback.Warnings.Count != 0, "fallback is disclosed");
            corrupted[SectorFor(0, 5) * 0x1000 + 100] ^= 1;
            Throws(() => MercurySave.Load(corrupted));
            var torn = Fixture();
            BinaryPrimitives.WriteUInt32LittleEndian(torn.AsSpan(SectorFor(1, 6) * 0x1000 + 0xFFC), 10);
            Equal(8u, MercurySave.Load(torn).SaveCounter);
            var duplicate = Fixture();
            BinaryPrimitives.WriteUInt16LittleEndian(duplicate.AsSpan(SectorFor(1, 6) * 0x1000 + 0xFF4), 5);
            Equal(8u, MercurySave.Load(duplicate).SaveCounter);
        });
        Run("save-counter wrap selects newest complete slot", () =>
        {
            var save = MercurySave.Load(Fixture(uint.MaxValue, 0));
            Equal(0u, save.SaveCounter);
            Equal(1, save.ActiveSlot);
        });
        Run("box/trainer edits are isolated until explicit setters", () =>
        {
            var save = MercurySave.Load(Fixture());
            var mon = save.GetBox(0, 0);
            mon.Species = 25;
            Check(save.GetBox(0, 0).IsEmpty, "getter must not mutate document");
            var trainer = save.GetTrainer();
            uint oldId = trainer.ID32;
            trainer.ID32 = 0x10203040;
            Equal(oldId, save.GetTrainer().ID32);
            save.SetTrainer(trainer);
            Equal(0x10203040u, MercurySave.Load(save.Export()).GetTrainer().ID32);
            Throws(() => save.GetBox(25, 0));
            Throws(() => save.GetBox(0, 30));
            Throws(() => save.SetBox(-1, 0, mon));
        });
        Run("party add/edit/delete is serialized independently of boxes", () =>
        {
            var save = MercurySave.Load(Fixture());
            Equal(0, save.PartyCount);
            var mon = ExampleMon();
            mon.RecalculatePartyStats([80, 90, 100, 70, 60, 60], 50);
            save.SetParty(0, mon);
            Equal(1, save.PartyCount);
            var second = mon.Clone();
            second.Species = 25;
            save.SetParty(1, second);
            var loaded = MercurySave.Load(save.Export());
            Equal(2, loaded.PartyCount);
            Equal((ushort)1253, loaded.GetParty(0).Species);
            Equal((byte)50, loaded.GetParty(0).PartyLevel);
            Equal((ushort)25, loaded.GetParty(1).Species);
            Check(loaded.GetBox(0, 0).IsEmpty, "party does not alias PC storage");
            loaded.DeleteParty(0);
            Equal(1, loaded.PartyCount);
            Equal((ushort)25, MercurySave.Load(loaded.Export()).GetParty(0).Species);
        });
        Run("text codec enforces byte length and unknown-byte roundtrip", () =>
        {
            var codec = MercuryTextCodec.FromCharmapJson("{\"BB\":\"A\",\"BC\":\"B\",\"0101\":\"妙\",\"0102\":\"蛙\",\"FE\":\"n\"}");
            Equal("妙蛙", codec.Decode(codec.Encode("妙蛙", 4)));
            Check(!codec.CanEncode("妙蛙A", 4), "CJK name limit uses bytes");
            Throws(() => codec.Encode("妙蛙A", 4));
            var unknown = new byte[] { 0xEF, 0xFF };
            Bytes(unknown, codec.Encode(codec.Decode(unknown), 2));
            Check(codec.Decode([0xFE, 0xFF]) != "n", "control code is not external glyph");
        });

        Run("party-only opaque bytes survive a friendship-only edit", () =>
        {
            var raw = ExampleMon().ToPartyBytes();
            raw[0x1C] = 0x76;
            raw[0x1D] = 0x54;
            raw[0x2B] = 0xCC;
            raw[0x13] |= 0xA0;
            raw[0x1E] = 0xEE;
            raw[0x1F] = 0xDD;
            for (int i = 0x4C; i < 0x64; i++) raw[i] = (byte)(i ^ 0x6A);
            var mon = MercuryPokemon.FromParty(raw);
            Bytes(raw, mon.ToPartyBytes());
            mon.Friendship ^= 1;
            var edited = mon.ToPartyBytes();
            for (int i = 0; i < 100; i++)
                if (i != 0x29) Equal(raw[i], edited[i]);
            Equal((byte)(raw[0x29] ^ 1), edited[0x29]);
            Bytes(edited, mon.Clone().ToPartyBytes());
        });
        Run("expanded ball byte, egg flags and type override are independent", () =>
        {
            var raw = ExampleMon().ToBoxBytes();
            raw[0x35] = 0x6D;
            raw[0x13] |= 0x18;
            var mon = MercuryPokemon.FromBox(raw);
            mon.Ball = 200;
            var changed = mon.ToBoxBytes();
            Equal((byte)200, changed[0x26]);
            Equal(raw[0x35], changed[0x35]);
            mon.HiddenAbility = true;
            mon.IsEgg = true;
            var ivword = BinaryPrimitives.ReadUInt32LittleEndian(mon.ToBoxBytes().AsSpan(0x36));
            Equal(0xC0000000u, ivword & 0xC0000000u);
            Check((mon.ToBoxBytes()[0x13] & 4) != 0, "header egg flag synchronized");
            mon.Species = 25;
            Equal(0, mon.TypeOverride);
            Check(mon.IsEgg && mon.HiddenAbility, "species change preserves egg and hidden flags");
        });
        Run("all single-gender ratios and large-ID shiny constraints", () =>
        {
            foreach (var (ratio, gender) in new[] { ((byte)0, 0), ((byte)254, 1), ((byte)255, 2) })
            {
                var mon = ExampleMon();
                mon.ID32 = 0xFEDC0123;
                mon.SetPersonality(24, gender, ratio, true, 2);
                Equal(gender, mon.GetGender(ratio));
                Equal(24, mon.Nature);
                Check(mon.IsShiny, "shiny across full PID space");
            }
        });
        Run("expanded coins use unchecksummed section13 tail, not legacy field", () =>
        {
            var input = Fixture();
            var save = MercurySave.Load(input);
            var trainer = save.GetTrainer();
            trainer.Coins = 123456789;
            save.SetTrainer(trainer);
            var output = save.Export();
            int coins = SectorFor(1, 13) * 0x1000 + 0x7CC;
            Equal(123456789u, BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(coins)));
            Equal(123456789u, MercurySave.Load(output).GetTrainer().Coins);
            for (int i = 0; i < input.Length; i++)
                if (i < coins || i >= coins + 4) Equal(input[i], output[i]);
            VerifyChecksums(output);
        });
        Run("stat modes are read from mapped parasite fields and applied", () =>
        {
            int[] baseStats = [60, 70, 50, 60, 40, 40]; // BST 320, HP byte doubling does not overflow.
            foreach (int mode in new[] { 0, 11, 12, 13 })
            {
                var bytes = Fixture();
                bytes[SectorFor(1, 0) * 0x1000 + 0xF2A] = mode == 0 ? (byte)0 : (byte)1;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(SectorFor(1, 4) * 0x1000 + 0xEFC), (ushort)mode);
                var save = MercurySave.Load(bytes);
                Equal(mode, save.StatMode);
                var mon = MercuryPokemon.Create(25, 0);
                mon.RecalculatePartyStats(baseStats, 50, save.StatMode);
                int hpBase = mode == 13 ? 100 : mode == 12 ? 120 : 60;
                int atkBase = mode == 13 ? 100 : mode == 12 ? 140 : mode == 11 ? 70 * 540 / 260 : 70;
                Equal((ushort)(hpBase + 60), mon.PartyStats[0]);
                Equal((ushort)(atkBase + 5), mon.PartyStats[1]);
                var shedinja = MercuryPokemon.Create(0x12F, 0);
                shedinja.RecalculatePartyStats(baseStats, 50, mode);
                Equal((ushort)1, shedinja.PartyStats[0]);
            }
        });

        string? rom = Option(args, "--rom");
        string? research = Option(args, "--research");
        MercuryGameData? game = null;
        if (rom is not null)
            Run("user ROM profile and native resource selection", () =>
            {
                game = MercuryGameData.FromRom(File.ReadAllBytes(rom));
                Equal(1554, game.Species.Count);
                Equal(1015, game.Moves.Count);
                Equal(750, game.Items.Count);
                Equal("131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd", game.RomSha256.ToLowerInvariant());
                for (int species = 0; species < 1554; species++)
                {
                    var data = game.GetSpecies(species);
                    if (!data.HasData) continue;
                    foreach (byte level in new byte[] { 1, 5, 50, 100 })
                        Equal(level, game.GetLevel(species, game.GetExperience(species, level)));
                }
                var pixels = game.GetSpriteRgba(25, 0, 0, out int width, out int height);
                Check(pixels is not null && width == 64 && height == 64 && pixels.Length == width * height * 4, "native 64x64 RGBA sprite");
                var altered = File.ReadAllBytes(rom);
                altered[0x100] ^= 1;
                Throws(() => MercuryGameData.FromRom(altered));
            });
        if (research is not null)
            Run("existing ROM-native research agrees with runtime importer", () =>
            {
                var fromResearch = MercuryGameData.FromResearch(research);
                Equal(1554, fromResearch.Species.Count);
                Equal(1015, fromResearch.Moves.Count);
                Equal(750, fromResearch.Items.Count);
                if (game is not null)
                    for (int id = 0; id < 1554; id++)
                    {
                        var a = game.GetSpecies(id);
                        var b = fromResearch.GetSpecies(id);
                        Check(a.BaseStats.SequenceEqual(b.BaseStats), $"species {id} base stats");
                        Check(a.Abilities.SequenceEqual(b.Abilities), $"species {id} stored abilities");
                        Equal(a.GrowthRate, b.GrowthRate);
                        Equal(a.GenderRatio, b.GenderRatio);
                        Check(a.LevelUpMoves.SequenceEqual(b.LevelUpMoves), $"species {id} level-up table");
                    }
                Equal(192, fromResearch.Items[192].Id);
                Equal(255, fromResearch.Items[192].EmbeddedId);
                Equal("月月熊", fromResearch.SpeciesName(1253));
                var profilePath = Option(args, "--profile-output");
                if (profilePath is not null)
                {
                    fromResearch.SaveProfile(profilePath);
                    var restored = MercuryGameData.LoadProfile(profilePath);
                    Equal(fromResearch.SpeciesName(1253), restored.SpeciesName(1253));
                    Equal(fromResearch.AbilityName(1253, 0), restored.AbilityName(1253, 0));
                    var rgba = restored.GetSpriteRgba(25, 0, 0, out int w, out int h);
                    Check(rgba is not null && rgba.Length == w * h * 4, "profile retains sprite source");
                }
            });
        int privateIndex = 0;
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--save")
            {
                string privatePath = args[++i];
                int index = ++privateIndex;
                Run($"private save {index}: unchanged roundtrip and copy-only edit", () =>
                {
                    var input = File.ReadAllBytes(privatePath);
                    var hash = SHA256.HashData(input);
                    var save = MercurySave.Load(input);
                    Bytes(input, save.Export());
                    bool edited = false;
                    for (int box = 0; box < 25 && !edited; box++)
                        for (int slot = 0; slot < 30 && !edited; slot++)
                        {
                            var mon = save.GetBox(box, slot);
                            if (mon.IsEmpty) continue;
                            var original = mon.ToBoxBytes();
                            mon.Friendship = (byte)(mon.Friendship ^ 1);
                            save.SetBox(box, slot, mon);
                            var exported = save.Export();
                            var loaded = MercurySave.Load(exported);
                            Equal(mon.Friendship, loaded.GetBox(box, slot).Friendship);
                            Equal(mon.Species, loaded.GetBox(box, slot).Species);
                            save.SetBox(box, slot, MercuryPokemon.FromBox(original));
                            Bytes(input, save.Export());
                            VerifyChecksums(exported, false);
                            edited = true;
                        }
                    Check(edited, "save contains an editable PC entity");
                    Bytes(hash, SHA256.HashData(File.ReadAllBytes(privatePath)));
                });
            }
        var report = new { status = failures == 0 ? "passed" : "failed", passed = Results.Count - failures, failed = failures, checks = Results, scope = "Binary/semantic checks only. No emulator or hardware gameplay claim. Private saves read-only; changes kept in memory." };
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        string? reportPath = Option(args, "--report");
        if (reportPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, json);
        }
        return failures == 0 ? 0 : 1;
    }

    private static MercuryPokemon ExampleMon()
    {
        var mon = MercuryPokemon.Create(1253, 0x12345678);
        mon.PID = 0x10203040;
        mon.HeldItem = 749;
        mon.Experience = 125000;
        mon.Moves = [1, 0, 1014, 1000];
        mon.IVs = [31, 30, 29, 28, 27, 26];
        mon.EVs = [252, 0, 0, 6, 252, 0];
        mon.Friendship = 200;
        return mon;
    }

    private static byte[] Fixture(uint first = 8, uint second = 9, bool trailer = false)
    {
        var result = new byte[0x20000 + (trailer ? 16 : 0)];
        for (int slot = 0; slot < 2; slot++)
            for (int id = 0; id < 14; id++)
            {
                int start = SectorFor(slot, id) * 0x1000;
                result.AsSpan(start + SectionSizes[id], 0xFF0 - SectionSizes[id]).Fill(0xA1);
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start + 0xFF0), 0x44332211);
                BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(start + 0xFF4), (ushort)id);
                BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(start + 0xFF6), Checksum(result.AsSpan(start, SectionSizes[id])));
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start + 0xFF8), Signature);
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(start + 0xFFC), slot == 0 ? first : second);
            }
        result.AsSpan(28 * 0x1000, 2 * 0x1000).Fill(0xA7);
        result.AsSpan(30 * 0x1000 + 0xFF0, 16).Fill(0x7A);
        result.AsSpan(31 * 0x1000 + 0xFF0, 16).Fill(0x7B);
        if (trailer) for (int i = 0; i < 16; i++) result[0x20000 + i] = (byte)(0xC0 + i);
        return result;
    }

    private static int SectorFor(int slot, int section) => slot * 14 + (section + (slot == 0 ? 3 : 7)) % 14;
    private static ushort Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length; i += 4)
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32LittleEndian(data[i..]));
        return (ushort)((sum & 0xFFFF) + (sum >> 16));
    }
    private static void VerifyChecksums(byte[] data, bool bothSlots = true)
    {
        // Parse actual footer IDs rather than assuming the synthetic rotation.
        int validSlots = 0;
        for (int slot = 0; slot < 2; slot++)
        {
            bool valid = true;
            for (int sector = slot * 14; sector < slot * 14 + 14; sector++)
            {
                int start = sector * 0x1000;
                int id = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start + 0xFF4));
                if (id >= 14 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(start + 0xFF8)) != Signature ||
                    BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start + 0xFF6)) != Checksum(data.AsSpan(start, SectionSizes[id])))
                    valid = false;
            }
            if (valid) validSlots++;
        }
        Check(validSlots >= (bothSlots ? 2 : 1), "written sections have valid checksums");
    }
    private static string? Option(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
    private static void Run(string name, Action action)
    {
        try { action(); Results.Add(new { name, status = "passed" }); }
        catch (Exception ex) { failures++; Results.Add(new { name, status = "failed", error = ex.GetType().Name + ": " + ex.Message }); }
    }
    private static void Equal<T>(T expected, T actual) where T : IEquatable<T> => Check(expected.Equals(actual), $"expected {expected}, got {actual}");
    private static void Bytes(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual) => Check(expected.SequenceEqual(actual), "byte sequence mismatch");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Throws(Action action)
    {
        try { action(); }
        catch (Exception) { return; }
        throw new InvalidOperationException("expected rejection but call succeeded");
    }
}
