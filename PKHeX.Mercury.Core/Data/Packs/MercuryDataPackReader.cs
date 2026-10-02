using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace PKHeX.Mercury.Core;

/// <summary>Query outcome; missing resources and invalid selections are not replaced or clamped.</summary>
public enum MercuryPackReadStatus
{
    Available,
    InvalidIndex,
    InvalidSelection,
    InvalidPointer,
    InvalidEntry,
    InvalidLz77,
    InsufficientResource,
    Unsupported,
    Missing,
}

/// <summary>
/// Read-only, offline view of a validated portable pack. The compressed archive is snapshotted into
/// this instance and decoded payloads are cached privately. No ZIP/file handles remain open between
/// calls. This does not attach the pack to GameData, change HasSprites, or certify ROM possession.
/// </summary>
public sealed class MercuryDataPackReader
{
    private readonly byte[] _archive;
    private readonly MercuryPackSpriteIndex _index;
    private readonly IReadOnlyList<MercuryPackLocation> _locations;
    private readonly Dictionary<string, byte[]> _payloads = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public string ProfileDirectory { get; }
    public string SourceRomSha { get; }
    public MercuryDataPackManifest Manifest { get; }

    public MercuryDataPackReader(string directory)
    {
        ProfileDirectory = Path.GetFullPath(directory);
        Manifest = MercuryDataPackValidator.Validate(ProfileDirectory);
        SourceRomSha = Manifest.SupportedRomSha256;
        // LoadProfile remains the numeric-data consumer; this reader does not modify its ROM gates.
        // Reject the field even if null, just as the validator does, before exposing the directory.
        using (var profile = JsonDocument.Parse(ReadVerified(MercuryDataPackManifest.ProfileFileName)))
        {
            if (profile.RootElement.EnumerateObject().Any(p => string.Equals(p.Name, "romPath", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("A data-pack profile must not contain romPath.");
        }
        var locations = JsonSerializer.Deserialize<MercuryPackLocation[]>(ReadVerified(MercuryDataPackManifest.LocationsFileName), MercuryDataPackJson.Options)
            ?? throw new InvalidDataException("Missing locations.");
        _locations = Array.AsReadOnly(locations);
        _archive = ReadVerified(MercuryDataPackManifest.SpritesFileName);
        using var memory = new MemoryStream(_archive, writable: false);
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read);
        using var stream = (zip.GetEntry("index.json") ?? throw new InvalidDataException("Missing sprite index.")).Open();
        _index = JsonSerializer.Deserialize<MercuryPackSpriteIndex>(stream, MercuryDataPackJson.Options)
            ?? throw new InvalidDataException("Missing sprite index.");
        // Validators permit any ordering while requiring the complete unique ID ranges.
        _index = _index with
        {
            Front = _index.Front.OrderBy(z => z.ResourceIndex).ToArray(),
            Items = _index.Items.OrderBy(z => z.ResourceIndex).ToArray(),
            Types = _index.Types.OrderBy(z => z.ResourceIndex).ToArray(),
            Balls = _index.Balls.OrderBy(z => z.ResourceIndex).ToArray(),
        };
    }

    public IReadOnlyList<MercuryPackLocation> GetLocations() => _locations;

    public bool TryGetFront(int resourceIndex, int paletteIndex, bool shiny, MercurySpriteSelection selection,
        out byte[] rgba, out MercurySpriteMetadata metadata)
        => TryGetFront(resourceIndex, paletteIndex, shiny, selection, out rgba, out metadata, out _);

    public bool TryGetFront(int resourceIndex, int paletteIndex, bool shiny, MercurySpriteSelection selection,
        out byte[] rgba, out MercurySpriteMetadata metadata, out MercuryPackReadStatus status)
    {
        rgba = [];
        metadata = default;
        if ((uint)resourceIndex >= (uint)_index.Front.Length || (uint)paletteIndex >= (uint)_index.Front.Length)
        {
            status = MercuryPackReadStatus.InvalidIndex;
            return false;
        }
        var tileInfo = _index.Front[resourceIndex].Tiles;
        var paletteInfo = shiny ? _index.Front[paletteIndex].ShinyPalette : _index.Front[paletteIndex].NormalPalette;
        status = GetStatus(tileInfo);
        if (status != MercuryPackReadStatus.Available)
            return false;
        status = GetStatus(paletteInfo);
        if (status != MercuryPackReadStatus.Available)
            return false;
        byte[] tiles = ReadPayload(tileInfo), palette = ReadPayload(paletteInfo);
        var actual = new MercurySpriteMetadata(tiles.Length / 2048, palette.Length / 32, tiles.Length % 2048, palette.Length % 32);
        if (actual.FrameCount != tileInfo.CompleteFrameCount || actual.PalettePageCount != paletteInfo.CompletePalettePages ||
            actual.TrailingTileBytes != tileInfo.TailLength || actual.TrailingPaletteBytes != paletteInfo.TailLength)
            throw new InvalidDataException("Sprite payload lengths disagree with frame/page metadata.");
        if (selection.FrameIndex < 0 || selection.FrameIndex >= actual.FrameCount ||
            selection.PalettePage < 0 || selection.PalettePage >= actual.PalettePageCount)
        {
            status = MercuryPackReadStatus.InvalidSelection;
            return false;
        }
        int frameOffset = selection.FrameIndex * 2048, paletteOffset = selection.PalettePage * 32;
        Span<int> colors = stackalloc int[16];
        for (int i = 0; i < colors.Length; i++)
        {
            int offset = paletteOffset + i * 2;
            int value = palette[offset] | (palette[offset + 1] << 8);
            int r = value & 31, g = (value >> 5) & 31, b = (value >> 10) & 31;
            colors[i] = ((r << 3) | (r >> 2)) | (((g << 3) | (g >> 2)) << 8) | (((b << 3) | (b >> 2)) << 16);
        }
        var output = new byte[64 * 64 * 4];
        for (int pixel = 0; pixel < 64 * 64; pixel++)
        {
            byte packed = tiles[frameOffset + (pixel >> 1)];
            int colorIndex = (pixel & 1) == 0 ? packed & 15 : packed >> 4;
            int x = (pixel % 8) + (((pixel / 64) % 8) * 8);
            int y = ((pixel / 8) % 8) + ((pixel / 512) * 8);
            int offset = (y * 64 + x) * 4;
            int color = colors[colorIndex];
            output[offset] = (byte)color;
            output[offset + 1] = (byte)(color >> 8);
            output[offset + 2] = (byte)(color >> 16);
            output[offset + 3] = colorIndex == 0 ? (byte)0 : (byte)255;
        }
        rgba = output;
        metadata = actual;
        status = MercuryPackReadStatus.Available;
        return true;
    }

    public bool TryGetItemRgba(int item, out byte[] rgba, out int width, out int height, out MercuryPackReadStatus status)
        => TryGetRendered(_index.Items, item, out rgba, out width, out height, out status);

    public bool TryGetTypeRgba(int type, out byte[] rgba, out int width, out int height, out MercuryPackReadStatus status)
        => TryGetRendered(_index.Types, type, out rgba, out width, out height, out status);

    /// <summary>Returns the exported first ball frame only; does not synthesize animation.</summary>
    public bool TryGetBallRgba(int ball, out byte[] rgba, out int width, out int height, out MercuryPackReadStatus status)
        => TryGetRendered(_index.Balls, ball, out rgba, out width, out height, out status);

    private bool TryGetRendered(MercuryPackRenderedSprite[] resources, int index, out byte[] rgba,
        out int width, out int height, out MercuryPackReadStatus status)
    {
        rgba = [];
        width = height = 0;
        if ((uint)index >= (uint)resources.Length)
        {
            status = MercuryPackReadStatus.InvalidIndex;
            return false;
        }
        var image = resources[index].Image;
        status = GetStatus(image);
        if (status != MercuryPackReadStatus.Available)
            return false;
        byte[] bytes = ReadPayload(image);
        if (image.Width is not > 0 || image.Height is not > 0 || bytes.Length != (long)image.Width * image.Height * 4)
            throw new InvalidDataException("RGBA payload does not match its dimensions.");
        rgba = (byte[])bytes.Clone(); // Callers cannot mutate the private cache.
        width = image.Width.Value;
        height = image.Height.Value;
        return true;
    }

    private byte[] ReadVerified(string name)
    {
        var expected = Manifest.Files.Single(z => z.Path == name);
        byte[] bytes = File.ReadAllBytes(Path.Combine(ProfileDirectory, name));
        if (bytes.LongLength != expected.Length || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), expected.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Pack file changed after validation: {name}");
        return bytes;
    }

    private byte[] ReadPayload(MercuryPackSpriteBlob blob)
    {
        string path = blob.Path ?? throw new InvalidDataException("Available resource has no indexed payload.");
        lock (_sync)
        {
            if (_payloads.TryGetValue(path, out var cached))
                return cached;
            using var memory = new MemoryStream(_archive, writable: false);
            using var zip = new ZipArchive(memory, ZipArchiveMode.Read);
            var entry = zip.GetEntry(path) ?? throw new InvalidDataException("Indexed sprite payload is missing.");
            if (blob.Length is not > 0 || blob.Length > 0x100000 || entry.Length != blob.Length)
                throw new InvalidDataException("Indexed sprite payload length is invalid.");
            using var stream = entry.Open();
            byte[] bytes = new byte[blob.Length.Value];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != blob.Sha256)
                throw new InvalidDataException("Indexed sprite payload integrity check failed.");
            _payloads.Add(path, bytes);
            return bytes;
        }
    }

    private static MercuryPackReadStatus GetStatus(MercuryPackSpriteBlob blob) => blob.Status switch
    {
        MercuryPackSpriteStatus.Available => MercuryPackReadStatus.Available,
        MercuryPackSpriteStatus.InvalidPointer => MercuryPackReadStatus.InvalidPointer,
        MercuryPackSpriteStatus.InvalidEntry => MercuryPackReadStatus.InvalidEntry,
        MercuryPackSpriteStatus.InvalidLz77 => MercuryPackReadStatus.InvalidLz77,
        MercuryPackSpriteStatus.InsufficientResource => MercuryPackReadStatus.InsufficientResource,
        MercuryPackSpriteStatus.Unsupported => MercuryPackReadStatus.Unsupported,
        _ => MercuryPackReadStatus.Missing,
    };
}
