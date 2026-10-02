using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using PKHeX.Mercury.Core;
using Xunit;

namespace PKHeX.Core.Tests.Mercury;

public sealed class MercuryPidSpotsTests
{
    private const int Resource = 0x134;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Assembly Core = typeof(MercuryGameData).Assembly;
    private delegate void ApplyOperation(Span<byte> tiles, int resource, uint pid);
    private delegate ReadOnlySpan<byte> MaskAccessor();
    private delegate bool ResolveTable(byte[] rom, out uint table);
    private delegate bool Decompress(byte[] rom, uint address, out byte[] bytes);
    private delegate bool Render(byte[] rom, int resource, uint pid, uint trainer, MercurySpriteSelection selection,
        out byte[] rgba, out MercurySpriteMetadata metadata, int? paletteIndex);
    private static readonly Type Operation = Core.GetType("PKHeX.Mercury.Core.MercuryPidSpots")!;
    private static readonly ApplyOperation Apply = Operation.GetMethod("Apply", Static)!.CreateDelegate<ApplyOperation>();
    private static readonly MaskAccessor GetMask = Operation.GetProperty("MaskData", Static)!.GetMethod!.CreateDelegate<MaskAccessor>();
    private static readonly ResolveTable GetFrontTable = Core.GetType("PKHeX.Mercury.Core.MercuryRomLayout")!
        .GetMethod("TryGetFrontPicTable", Static)!.CreateDelegate<ResolveTable>();
    private static readonly Decompress Lz = Core.GetType("PKHeX.Mercury.Core.MercuryLz77")!
        .GetMethod("TryDecompress", Static)!.CreateDelegate<Decompress>();
    private static readonly Render RenderRom = Core.GetType("PKHeX.Mercury.Core.MercurySpriteLoader")!.GetMethods(Static)
        .Single(m => m.Name == "TryRender" && m.GetParameters().Length == 8).CreateDelegate<Render>();

    // Independent numeric transcription from the four ROM records, not the implementation's table.
    private static readonly (int X, int Y, ushort[] Rows)[] Spots =
    [
        (16, 7, [0x70, 0x1FC, 0x3FE, 0x7FE, 0x7FF, 0xFFF, 0xFFF, 0xFFF, 0x7FE, 0x7FE, 0x3FC, 0x1E0, 0, 0, 0, 0]),
        (40, 8, [0x1E0, 0x3F8, 0x7FC, 0xFFE, 0xFFE, 0x1FFF, 0x1FFF, 0x1FFF, 0xFFE, 0xFFE, 0x7FC, 0x7F8, 0xE0, 0, 0, 0]),
        (22, 25, [0x1C, 0x3E, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x3E, 0x1C, 0, 0, 0, 0, 0, 0, 0]),
        (34, 26, [0x3C, 0x7E, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7E, 0x3C, 0, 0, 0, 0, 0, 0, 0]),
    ];

    [Fact]
    public void EveryPidByteZeroOrFFBoundaryMatchesIndependentPixelProjection()
    {
        for (int combination = 0; combination < 16; combination++)
        {
            uint pid = 0;
            for (int i = 0; i < 4; i++)
                if ((combination & (1 << i)) != 0)
                    pid |= 0xFFu << (i * 8);
            var tiles = Enumerable.Range(0, 8192).Select(i => (byte)(i * 73)).ToArray();
            var expected = Project(tiles, pid);
            Apply(tiles, Resource, pid);
            Assert.Equal(expected, tiles);
        }
    }

    [Fact]
    public void BothNibblesOnlyTransformIndicesOneThroughThree()
    {
        var maskPixels = MarkedPixels(0x88888888);
        Assert.Contains(maskPixels.Keys, p => (p & 1) == 0);
        Assert.Contains(maskPixels.Keys, p => (p & 1) != 0);
        for (int low = 0; low < 16; low++)
        for (int high = 0; high < 16; high++)
        {
            byte packed = (byte)(low | (high << 4));
            var tiles = Enumerable.Repeat(packed, 8192).ToArray();
            Assert.Equal(Project(tiles, 0x88888888), ApplyCopy(tiles, 0x88888888));
            Apply(tiles, Resource, 0x88888888);
            foreach (int pixel in maskPixels.Keys)
            {
                int original = (pixel & 1) == 0 ? low : high;
                int expected = original is >= 1 and <= 3 ? original + 4 : original;
                Assert.Equal(expected, (tiles[pixel / 2] >> ((pixel & 1) * 4)) & 15);
            }
        }
    }

    [Fact]
    public void OverlappingMasksAndRepeatedApplicationAreIdempotent()
    {
        const uint pid = 0x80FF8888; // Third origin (29,32), fourth origin (26,26); mask pixels overlap.
        Assert.Contains(MarkedPixels(pid).Values, count => count > 1);
        var tiles = Enumerable.Repeat((byte)0x21, 8192).ToArray();
        var expected = Project(tiles, pid);
        Apply(tiles, Resource, pid);
        Assert.Equal(expected, tiles);
        Apply(tiles, Resource, pid);
        Assert.Equal(expected, tiles);
    }

    [Fact]
    public void WrappedYTargetsFourthFrameWithoutRepeatingSpotsOnEveryFrame()
    {
        var tiles = Enumerable.Repeat((byte)0x11, 8192).ToArray();
        Apply(tiles, Resource, 0);
        Assert.NotEqual((byte)0x11, tiles[7998]); // spot0: x12,y255 -> tile-byte7998.
        Assert.All(tiles.AsSpan(2048, 4096).ToArray(), value => Assert.Equal((byte)0x11, value));
        var changedFourth = tiles.AsSpan(6144).ToArray().Select((value, index) => (value, index))
            .Where(z => z.value != 0x11).Select(z => z.index).ToArray();
        Assert.NotEmpty(changedFourth);
        Assert.All(changedFourth, index => Assert.InRange(index, 1792, 2047));
        Assert.Equal(Project(Enumerable.Repeat((byte)0x11, 8192).ToArray(), 0), tiles);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(4352)]
    public void ShortBuffersOnlyProjectExistingBytesAndNeverAddFrames(int length)
    {
        var tiles = Enumerable.Repeat((byte)0x33, length).ToArray();
        var expected = Project(tiles, 0);
        Apply(tiles, Resource, 0);
        Assert.Equal(length, tiles.Length);
        Assert.Equal(expected, tiles);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(307)]
    [InlineData(309)]
    [InlineData(0x308)]
    public void OtherMappedResourcesAreUnchanged(int resource)
    {
        var tiles = Enumerable.Repeat((byte)0x12, 8192).ToArray();
        var original = (byte[])tiles.Clone();
        Apply(tiles, resource, 0);
        Assert.Equal(original, tiles);
    }

    [Fact]
    public void RomLoaderAppliesOnceBeforeSelectionAndShinyOnlyChangesPalette()
    {
        var tiles = Enumerable.Repeat((byte)0x21, 8192).ToArray();
        byte[] normal = Palette(1), shiny = Palette(9);
        var rom = SyntheticRom(tiles, normal, shiny);
        var before = (byte[])rom.Clone();
        const uint pid = 0;
        for (int frame = 0; frame < 4; frame++)
        foreach (uint trainer in new uint[] { 8, 0 })
        {
            Assert.True(RenderRom(rom, Resource, pid, trainer, new(frame, 0), out var rgba, out var metadata, null));
            Assert.Equal(new MercurySpriteMetadata(4, 1, 0, 0), metadata);
            Assert.Equal(RenderExpected(Project(tiles, pid), trainer == 0 ? shiny : normal, frame), rgba);
            Assert.True(RenderRom(rom, 1, pid, trainer, new(frame, 0), out var other, out _, null));
            Assert.Equal(RenderExpected(tiles, trainer == 0 ? shiny : normal, frame), other);
        }
        Assert.Equal(before, rom);
        var shortRom = SyntheticRom(tiles.AsSpan(0, 2048).ToArray(), normal, shiny);
        Assert.True(RenderRom(shortRom, Resource, pid, 8, default, out _, out var shortMetadata, null));
        Assert.Equal(new MercurySpriteMetadata(1, 1, 0, 0), shortMetadata);
        Assert.False(RenderRom(shortRom, Resource, pid, 8, new(1, 0), out _, out _, null));
    }

    [MercurySpotFilesFact("MERCURY_TEST_ROM", "MERCURY_TEST_ROM_V10")]
    public void BothVerifiedRomsMatchAll144MaskBytesAndActualEntityProjection()
    {
        byte[] mask = GetMask().ToArray();
        Assert.Equal(144, mask.Length);
        Assert.Equal("1b05a7a24b442290e00d699561465627cb4857feb5a28aaf4cef0c608f3b647e", Convert.ToHexStringLower(SHA256.HashData(mask)));
        foreach (string variable in new[] { "MERCURY_TEST_ROM", "MERCURY_TEST_ROM_V10" })
        {
            var rom = File.ReadAllBytes(Environment.GetEnvironmentVariable(variable)!);
            var data = MercuryGameData.FromRom(rom); // Exact known ROM validation, not arbitrary offsets on unknown bytes.
            Assert.Equal(variable.EndsWith("V10", StringComparison.Ordinal) ? MercuryRomVersion.V1_0.Sha256 : MercuryRomVersion.V1_1.Sha256, data.RomSha256);
            Assert.Equal(mask, rom.AsSpan(0x25265C, 144).ToArray());
            byte[] tiles = RawTiles(rom, Resource);
            Assert.Equal(2048, tiles.Length);
            foreach (uint pid in new uint[] { 0, uint.MaxValue, 0x88888888, 0x12345678 })
            foreach (bool shiny in new[] { false, true })
            {
                uint trainer = pid ^ (shiny ? 0u : 8u);
                var rgba = data.GetSpriteRgba(Resource, pid, trainer, default, out int width, out int height, out var metadata);
                Assert.Equal(64, width); Assert.Equal(64, height);
                Assert.Equal(new MercurySpriteMetadata(1, 1, 0, 0), metadata);
                Assert.Equal(RenderExpected(Project(tiles, pid), RawPalette(rom, Resource, shiny), 0), rgba);
            }
        }
    }

    [MercurySpotFilesFact("MERCURY_TEST_ROM", "MERCURY_TEST_PACK")]
    public void FrozenPackPreservesRawPayloadAcrossPidsAndMatchesRomEntityRendering()
    {
        string directory = Environment.GetEnvironmentVariable("MERCURY_TEST_PACK")!;
        var hashes = Directory.GetFiles(directory).ToDictionary(p => p, p => SHA256.HashData(File.ReadAllBytes(p)));
        var reader = new MercuryDataPackReader(directory);
        var pack = MercuryGameData.LoadPack(directory);
        var romBytes = File.ReadAllBytes(Environment.GetEnvironmentVariable("MERCURY_TEST_ROM")!);
        var rom = MercuryGameData.FromRom(romBytes);
        Assert.Equal(rom.RomSha256, reader.SourceRomSha);
        Assert.True(reader.TryGetFront(Resource, Resource, false, default, out var rawBefore, out var metadataBefore));
        Assert.Equal(RenderExpected(RawTiles(romBytes, Resource), RawPalette(romBytes, Resource, false), 0), rawBefore);
        var results = new Dictionary<uint, byte[]>();
        foreach (uint pid in new uint[] { 0, uint.MaxValue, 0x88888888, 0, uint.MaxValue })
        foreach (bool shiny in new[] { false, true })
        {
            uint trainer = pid ^ (shiny ? 0u : 8u);
            Assert.True(reader.TryGetFront(Resource, Resource, shiny, pid, default, out var entity, out var metadata, out var status));
            Assert.Equal(MercuryPackReadStatus.Available, status);
            Assert.Equal(metadataBefore, metadata);
            Assert.Equal(rom.GetSpriteRgba(Resource, pid, trainer, out _, out _), entity);
            Assert.Equal(pack.GetSpriteRgba(Resource, pid, trainer, out _, out _), entity);
            if (!shiny)
            {
                if (results.TryGetValue(pid, out var prior)) Assert.Equal(prior, entity);
                results[pid] = entity;
            }
            Assert.True(reader.TryGetFront(Resource, Resource, false, default, out var rawAfter, out _));
            Assert.Equal(rawBefore, rawAfter); // Detects mutation of the shared decoded cache, not only archive bytes.
        }
        Assert.False(results[0].SequenceEqual(rawBefore));
        Assert.False(results[0].SequenceEqual(results[uint.MaxValue]));
        Assert.True(reader.TryGetFront(1, 1, false, default, out var unrelatedRaw, out _));
        Assert.True(reader.TryGetFront(1, 1, false, 0u, default, out var unrelatedEntity, out _));
        Assert.Equal(unrelatedRaw, unrelatedEntity);
        Assert.Equal(rom.GetSpriteRgba(1, 0, 8, out _, out _), pack.GetSpriteRgba(1, 0, 8, out _, out _));
        Assert.False(reader.TryGetFront(Resource, Resource, false, 0u, new(1, 0), out _, out _, out var invalid));
        Assert.Equal(MercuryPackReadStatus.InvalidSelection, invalid);
        foreach (var (path, hash) in hashes)
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static byte[] ApplyCopy(byte[] source, uint pid)
    {
        var copy = (byte[])source.Clone(); Apply(copy, Resource, pid); return copy;
    }

    private static Dictionary<int, int> MarkedPixels(uint pid)
    {
        var pixels = new Dictionary<int, int>();
        for (int i = 0; i < Spots.Length; i++)
        {
            var spot = Spots[i];
            int x = (spot.X + (int)((pid >> (i * 8)) & 15) - 8) & 255;
            int originY = (spot.Y + (int)((pid >> (i * 8 + 4)) & 15) - 8) & 255;
            for (int row = 0; row < 16; row++)
            for (int bit = 0; bit < 16; bit++)
            {
                if ((spot.Rows[row] & (1 << bit)) == 0) continue;
                int px = x + bit, py = (originY + row) & 255;
                int tile = (py / 8) * 8 + px / 8;
                int pixel = tile * 64 + (py % 8) * 8 + px % 8;
                pixels[pixel] = pixels.GetValueOrDefault(pixel) + 1;
            }
        }
        return pixels;
    }

    private static byte[] Project(byte[] original, uint pid)
    {
        var copy = (byte[])original.Clone();
        // A set of target pixel identities avoids implementing the production nibble/mask traversal twice.
        foreach (int pixel in MarkedPixels(pid).Keys)
        {
            if (pixel / 2 >= copy.Length) continue;
            int shift = (pixel & 1) * 4;
            int index = (original[pixel / 2] >> shift) & 15;
            if (index is >= 1 and <= 3)
                copy[pixel / 2] = (byte)((copy[pixel / 2] & ~(15 << shift)) | ((index + 4) << shift));
        }
        return copy;
    }

    private static byte[] RenderExpected(byte[] tiles, byte[] palette, int frame)
    {
        byte[] rgba = new byte[16384];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            int tile = y / 8 * 8 + x / 8;
            int offset = frame * 2048 + tile * 32 + y % 8 * 4 + x % 8 / 2;
            int index = (tiles[offset] >> ((x & 1) * 4)) & 15;
            ushort color = BinaryPrimitives.ReadUInt16LittleEndian(palette.AsSpan(index * 2));
            int pixel = (y * 64 + x) * 4;
            for (int c = 0; c < 3; c++)
            {
                int value = (color >> (c * 5)) & 31;
                rgba[pixel + c] = (byte)((value << 3) | (value >> 2));
            }
            rgba[pixel + 3] = index == 0 ? (byte)0 : (byte)255;
        }
        return rgba;
    }

    private static byte[] RawTiles(byte[] rom, int resource)
    {
        Assert.True(GetFrontTable(rom, out uint table));
        return DecodeEntry(rom, table, resource);
    }
    private static byte[] RawPalette(byte[] rom, int resource, bool shiny)
        => DecodeEntry(rom, BinaryPrimitives.ReadUInt32LittleEndian(rom.AsSpan(shiny ? 0x134 : 0x130)), resource);
    private static byte[] DecodeEntry(byte[] rom, uint table, int resource)
    {
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(rom.AsSpan((int)(table - 0x08000000) + resource * 8));
        Assert.True(Lz(rom, pointer, out var bytes)); return bytes;
    }

    private static byte[] Palette(int bias)
    {
        var result = new byte[32];
        for (int i = 0; i < 16; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(i * 2), (ushort)(((i + bias) & 31) | (((i * 2 + bias) & 31) << 5) | (((i * 3 + bias) & 31) << 10)));
        return result;
    }
    private static byte[] SyntheticRom(byte[] tiles, byte[] normal, byte[] shiny)
    {
        var rom = new byte[0x10000];
        WritePointer(rom, 0x128, 0x08001000); WritePointer(rom, 0x130, 0x08002000); WritePointer(rom, 0x134, 0x08003000);
        foreach (int resource in new[] { Resource, 1 })
        {
            WritePointer(rom, 0x1000 + resource * 8, 0x08004000);
            WritePointer(rom, 0x2000 + resource * 8, 0x08008000);
            WritePointer(rom, 0x3000 + resource * 8, 0x08009000);
        }
        WriteLiteralLz(rom, 0x4000, tiles); WriteLiteralLz(rom, 0x8000, normal); WriteLiteralLz(rom, 0x9000, shiny);
        return rom;
    }
    private static void WritePointer(byte[] rom, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(offset), value);
    private static void WriteLiteralLz(byte[] rom, int offset, byte[] bytes)
    {
        rom[offset++] = 0x10; rom[offset++] = (byte)bytes.Length; rom[offset++] = (byte)(bytes.Length >> 8); rom[offset++] = (byte)(bytes.Length >> 16);
        for (int i = 0; i < bytes.Length;)
        {
            rom[offset++] = 0;
            for (int j = 0; j < 8 && i < bytes.Length; j++) rom[offset++] = bytes[i++];
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class MercurySpotFilesFactAttribute : FactAttribute
{
    public MercurySpotFilesFactAttribute(params string[] variables)
    {
        var missing = variables.Where(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v))).ToArray();
        if (missing.Length != 0)
            Skip = $"Set {string.Join(", ", missing)} to explicit user-owned ROM/frozen-pack inputs; no proprietary data is bundled.";
    }
}
