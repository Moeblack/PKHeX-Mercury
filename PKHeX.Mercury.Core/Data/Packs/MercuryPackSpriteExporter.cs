using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PKHeX.Mercury.Core;

/// <summary>Indexed resource totals, including unavailable entries. Front availability requires all three blobs.</summary>
public sealed record MercuryPackSpriteCounts
{
    public int FrontResources { get; init; }
    public int ItemResources { get; init; }
    public int TypeResources { get; init; }
    public int BallResources { get; init; }
    public int FrontAvailable { get; init; }
    public int ItemAvailable { get; init; }
    public int TypeAvailable { get; init; }
    public int BallAvailable { get; init; }
    public int FrontMissing => FrontResources - FrontAvailable;
    public int ItemMissing => ItemResources - ItemAvailable;
    public int TypeMissing => TypeResources - TypeAvailable;
    public int BallMissing => BallResources - BallAvailable;
}

/// <summary>Status describes usability, not merely presence of a ZIP entry.</summary>
public enum MercuryPackSpriteStatus
{
    Available,
    InvalidPointer,
    InvalidEntry,
    InvalidLz77,
    InsufficientResource,
    Unsupported,
    Missing,
}

/// <summary>
/// Payload metadata. Null path/length/hash means no payload. Insufficient resources may still
/// carry their complete decompressed bytes. SHA256 is lowercase hexadecimal over uncompressed payload bytes.
/// </summary>
public sealed record MercuryPackSpriteBlob
{
    public string? Path { get; init; }
    public int? Length { get; init; }
    public string? Sha256 { get; init; }
    public MercuryPackSpriteStatus Status { get; init; }
    public string? MissingReason { get; init; }
    public string Encoding { get; init; } = "";
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? BytesPerFrame { get; init; }
    public int? CompleteFrameCount { get; init; }
    public int? BytesPerPalettePage { get; init; }
    public int? CompletePalettePages { get; init; }
    public int? TailLength { get; init; }
    public ushort? DeclaredSize { get; init; }
}

/// <summary>Raw table index, not species identity. Tiles and both palette pools can be selected independently.</summary>
public sealed record MercuryPackFrontSprite
{
    public int ResourceIndex { get; init; }
    public MercuryPackSpriteBlob Tiles { get; init; } = new();
    public MercuryPackSpriteBlob NormalPalette { get; init; } = new();
    public MercuryPackSpriteBlob ShinyPalette { get; init; } = new();
}

public sealed record MercuryPackRenderedSprite
{
    public int ResourceIndex { get; init; }
    public MercuryPackSpriteBlob Image { get; init; } = new();
    public bool FirstFrameOnly { get; init; }
}

/// <summary>
/// Version 1 archive index. Raw front tiles use sequential GBA 8x8 tiles, 4bpp low nibble first,
/// 64x64 per 2048-byte frame. Palette pages contain 16 little-endian BGR555 colours; index zero
/// is transparent. RGBA8888 images are row-major, top-left origin. No animation is specified.
/// </summary>
public sealed record MercuryPackSpriteIndex
{
    public int FormatVersion { get; init; } = 1;
    public MercuryPackSpriteCounts Counts { get; init; } = new();
    public MercuryPackFrontSprite[] Front { get; init; } = [];
    public MercuryPackRenderedSprite[] Items { get; init; } = [];
    public MercuryPackRenderedSprite[] Types { get; init; } = [];
    public MercuryPackRenderedSprite[] Balls { get; init; } = [];
}

/// <summary>
/// Export only; input must already have passed exact ROM hash verification.
/// Exact ZIP names: index.json; front/{index}/tiles.bin, normal-palette.bin, shiny-palette.bin;
/// items/{index}.rgba; types/{index}.rgba; balls/{index}.rgba. Indices are unpadded decimal.
/// No source ROM path or compressed ROM blocks are stored. Only the supplied ZIP path is written.
/// </summary>
internal static class MercuryPackSpriteExporter
{
    internal static MercuryPackSpriteCounts Export(byte[] verifiedRom, string zipPath)
    {
        ArgumentNullException.ThrowIfNull(verifiedRom);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        // CreateNew is intentional: even an empty pre-existing destination must not be overwritten.
        using var output = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        bool frontValid = MercuryRomLayout.TryGetFrontPicTable(verifiedRom, out uint frontTable);
        bool normalValid = TryPaletteTable(verifiedRom, MercuryRomLayout.PaletteSlot, out uint normalTable);
        bool shinyValid = TryPaletteTable(verifiedRom, MercuryRomLayout.ShinyPaletteSlot, out uint shinyTable);
        var front = new MercuryPackFrontSprite[MercuryRomLayout.SpeciesCount];
        for (int i = 0; i < front.Length; i++)
        {
            string prefix = "front/" + i.ToString(CultureInfo.InvariantCulture) + "/";
            front[i] = new MercuryPackFrontSprite
            {
                ResourceIndex = i,
                Tiles = Extract(archive, verifiedRom, frontValid, frontTable, i, prefix + "tiles.bin", false),
                NormalPalette = Extract(archive, verifiedRom, normalValid, normalTable, i, prefix + "normal-palette.bin", true),
                ShinyPalette = Extract(archive, verifiedRom, shinyValid, shinyTable, i, prefix + "shiny-palette.bin", true),
            };
        }

        var items = new MercuryPackRenderedSprite[MercuryRomLayout.ItemCount];
        for (int i = 0; i < items.Length; i++)
        {
            bool success = MercurySpriteLoader.TryRenderItem(verifiedRom, i, out byte[] rgba);
            items[i] = Rendered(archive, "items", i, success, rgba,
                MercurySpriteLoader.ItemIconWidth, MercurySpriteLoader.ItemIconHeight, false, false);
        }
        var types = new MercuryPackRenderedSprite[256];
        for (int i = 0; i < types.Length; i++)
        {
            // The existing renderer reads the supported IDs from the verified ROM, not a wiki list.
            bool success = MercurySpriteLoader.TryRenderType(verifiedRom, (byte)i, out byte[] rgba, out int width, out int height);
            types[i] = Rendered(archive, "types", i, success, rgba, width, height, false, !success);
        }
        var balls = new MercuryPackRenderedSprite[28];
        for (int i = 0; i < balls.Length; i++)
        {
            bool success = MercurySpriteLoader.TryRenderBall(verifiedRom, (byte)i, out byte[] rgba, out int width, out int height);
            balls[i] = Rendered(archive, "balls", i, success, rgba, width, height, true, false);
        }
        var counts = new MercuryPackSpriteCounts
        {
            FrontResources = front.Length,
            ItemResources = items.Length,
            TypeResources = types.Length,
            BallResources = balls.Length,
            FrontAvailable = front.Count(z => IsAvailable(z.Tiles) && IsAvailable(z.NormalPalette) && IsAvailable(z.ShinyPalette)),
            ItemAvailable = items.Count(z => IsAvailable(z.Image)),
            TypeAvailable = types.Count(z => IsAvailable(z.Image)),
            BallAvailable = balls.Count(z => IsAvailable(z.Image)),
        };
        var index = new MercuryPackSpriteIndex { Counts = counts, Front = front, Items = items, Types = types, Balls = balls };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        using (var stream = archive.CreateEntry("index.json", CompressionLevel.Optimal).Open())
            JsonSerializer.Serialize(stream, index, options);
        return counts;
    }

    private static bool IsAvailable(MercuryPackSpriteBlob blob) => blob.Status == MercuryPackSpriteStatus.Available;

    private static bool TryPaletteTable(byte[] rom, uint slot, out uint table)
        => MercuryRomLayout.TryReadU32(rom, slot, out table) && MercuryRomLayout.IsRomAddress(table);

    /// <summary>Checks archive integrity and schema consistency, not provenance or authorization.</summary>
    internal static MercuryPackSpriteCounts Validate(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
            Require(entries.TryAdd(entry.FullName, entry), "Duplicate ZIP entry.");
        Require(entries.TryGetValue("index.json", out var indexEntry), "Missing index.json.");
        Require(indexEntry!.Length <= 16 * 1024 * 1024, "Index is too large.");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        MercuryPackSpriteIndex index;
        using (var stream = indexEntry.Open())
            index = JsonSerializer.Deserialize<MercuryPackSpriteIndex>(stream, options) ?? throw new InvalidDataException("Null index.");
        Require(index.FormatVersion == 1 && index.Counts is not null, "Unsupported index version or missing counts.");
        Require(index.Front is not null && index.Front.Length == MercuryRomLayout.SpeciesCount, "Wrong front count.");
        var declared = new HashSet<string>(StringComparer.Ordinal) { "index.json" };
        var frontIds = new HashSet<int>();
        int frontAvailable = 0;
        foreach (var resource in index.Front!)
        {
            Require(resource is not null, "Null front resource.");
            Require(resource!.ResourceIndex >= 0 && resource.ResourceIndex < MercuryRomLayout.SpeciesCount && frontIds.Add(resource.ResourceIndex), "Invalid or duplicate front index.");
            string prefix = "front/" + resource.ResourceIndex.ToString(CultureInfo.InvariantCulture) + "/";
            CheckBlob(resource.Tiles, prefix + "tiles.bin", 1);
            CheckBlob(resource.NormalPalette, prefix + "normal-palette.bin", 2);
            CheckBlob(resource.ShinyPalette, prefix + "shiny-palette.bin", 2);
            if (IsAvailable(resource.Tiles) && IsAvailable(resource.NormalPalette) && IsAvailable(resource.ShinyPalette))
                frontAvailable++;
        }
        var counts = new MercuryPackSpriteCounts
        {
            FrontResources = MercuryRomLayout.SpeciesCount,
            ItemResources = MercuryRomLayout.ItemCount,
            TypeResources = 256,
            BallResources = 28,
            FrontAvailable = frontAvailable,
            ItemAvailable = CheckRendered(index.Items, "items", MercuryRomLayout.ItemCount),
            TypeAvailable = CheckRendered(index.Types, "types", 256),
            BallAvailable = CheckRendered(index.Balls, "balls", 28),
        };
        Require(counts == index.Counts, "Declared counts disagree with resources.");
        Require(entries.Count == declared.Count, "Undeclared ZIP entries.");
        return counts;

        int CheckRendered(MercuryPackRenderedSprite[] resources, string group, int count)
        {
            Require(resources is not null && resources.Length == count, "Wrong rendered resource count.");
            var ids = new HashSet<int>();
            int available = 0;
            foreach (var resource in resources!)
            {
                Require(resource is not null, "Null rendered resource.");
                Require(resource!.ResourceIndex >= 0 && resource.ResourceIndex < count && ids.Add(resource.ResourceIndex), "Invalid or duplicate rendered index.");
                Require(resource.FirstFrameOnly == (group == "balls"), "Incorrect first-frame metadata.");
                CheckBlob(resource.Image, group + "/" + resource.ResourceIndex.ToString(CultureInfo.InvariantCulture) + ".rgba", 0);
                Require(resource.Image.Status != MercuryPackSpriteStatus.Unsupported || group == "types", "Unsupported status is only valid for type IDs.");
                if (IsAvailable(resource.Image))
                {
                    if (group == "items")
                        Require(resource.Image.Width == MercurySpriteLoader.ItemIconWidth && resource.Image.Height == MercurySpriteLoader.ItemIconHeight, "Wrong item dimensions.");
                    available++;
                }
            }
            return available;
        }

        void CheckBlob(MercuryPackSpriteBlob blob, string expectedPath, int kind)
        {
            Require(blob is not null, "Missing blob metadata.");
            Require(Enum.IsDefined(blob!.Status), "Unknown blob status.");
            bool available = IsAvailable(blob);
            Require(available ? blob.MissingReason is null : !string.IsNullOrWhiteSpace(blob.MissingReason), "Inconsistent missing reason.");
            Require(blob.Encoding == (kind == 0 ? "rgba8888" : kind == 1 ? "gba-tiled-4bpp" : "bgr555-le"), "Incorrect encoding.");
            if (kind == 0)
            {
                Require(blob.Status is MercuryPackSpriteStatus.Available or MercuryPackSpriteStatus.Missing or MercuryPackSpriteStatus.Unsupported, "Invalid rendered status.");
                Require(blob.DeclaredSize is null && blob.BytesPerPalettePage is null && blob.CompletePalettePages is null, "Unexpected rendered palette metadata.");
                Require(blob.CompleteFrameCount == (available ? 1 : 0), "Wrong rendered frame count.");
                Require(available ? blob.Width > 0 && blob.Height > 0 && blob.Length == (long)blob.Width * blob.Height * 4 && blob.BytesPerFrame == blob.Length && blob.TailLength == 0 : blob.BytesPerFrame is null && blob.TailLength is null, "Invalid rendered dimensions or length.");
            }
            else
            {
                Require(blob.Status is MercuryPackSpriteStatus.Available or MercuryPackSpriteStatus.InvalidPointer or MercuryPackSpriteStatus.InvalidEntry or MercuryPackSpriteStatus.InvalidLz77 or MercuryPackSpriteStatus.InsufficientResource, "Invalid raw status.");
                Require(kind == 1 ? blob.Width == 64 && blob.Height == 64 && blob.BytesPerFrame == 2048 && blob.BytesPerPalettePage is null && blob.CompletePalettePages is null : blob.Width is null && blob.Height is null && blob.BytesPerPalettePage == 32 && blob.BytesPerFrame is null && blob.CompleteFrameCount is null, "Invalid raw shape metadata.");
            }
            bool hasPayload = available || blob.Status == MercuryPackSpriteStatus.InsufficientResource;
            if (!hasPayload)
            {
                Require(blob.Path is null && blob.Length is null && blob.Sha256 is null, "Unavailable blob declares a payload.");
                if (kind != 0)
                    Require(blob.CompleteFrameCount is null && blob.CompletePalettePages is null && blob.TailLength is null, "Unavailable raw resource declares decoded metadata.");
                return;
            }
            Require(blob.Path == expectedPath && declared.Add(expectedPath), "Incorrect or duplicate payload path.");
            Require(blob.Length > 0 && blob.Length <= 0x100000 && blob.Sha256 is not null && blob.Sha256.Length == 64, "Invalid payload length or hash.");
            if (kind != 0)
            {
                int unit = kind == 1 ? 2048 : 32;
                int? complete = kind == 1 ? blob.CompleteFrameCount : blob.CompletePalettePages;
                Require(blob.DeclaredSize is not null && complete == blob.Length / unit && blob.TailLength == blob.Length % unit && available == (complete > 0), "Inconsistent raw frame/page metadata.");
            }
            Require(entries.TryGetValue(expectedPath, out var entry) && entry.Length == blob.Length, "Missing payload or wrong ZIP length.");
            using var payload = entry!.Open();
            // Bound reads by declared length rather than trusting a potentially forged ZIP directory.
            byte[] bytes = new byte[blob.Length!.Value];
            payload.ReadExactly(bytes);
            Require(payload.ReadByte() == -1, "Payload exceeds declared length.");
            Require(Convert.ToHexStringLower(SHA256.HashData(bytes)) == blob.Sha256, "Payload SHA256 mismatch.");
        }
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private static MercuryPackSpriteBlob Extract(ZipArchive archive, byte[] rom, bool tableValid, uint table,
        int index, string path, bool palette)
    {
        int unit = palette ? MercuryRomLayout.PaletteBytes : MercuryRomLayout.SpriteFrameBytes;
        var metadata = new MercuryPackSpriteBlob
        {
            Encoding = palette ? "bgr555-le" : "gba-tiled-4bpp",
            Width = palette ? null : MercuryRomLayout.SpriteWidth,
            Height = palette ? null : MercuryRomLayout.SpriteHeight,
            BytesPerFrame = palette ? null : unit,
            BytesPerPalettePage = palette ? unit : null,
        };
        if (!tableValid)
            return metadata with { Status = MercuryPackSpriteStatus.InvalidPointer, MissingReason = "Table pointer could not be resolved by the loader's decoder." };
        // Mirrors MercurySpriteLoader.TryReadEntry: 8-byte stride, u32 pointer and u16 declared size.
        long offset = MercuryRomLayout.ToOffset(table) + 8L * index;
        if (!MercuryRomLayout.TryReadU32Raw(rom, offset, out uint pointer)
            || !MercuryRomLayout.TryReadU16Raw(rom, offset + 4, out ushort declaredSize))
            return metadata with { Status = MercuryPackSpriteStatus.InvalidEntry, MissingReason = "Table entry is outside the ROM." };
        metadata = metadata with { DeclaredSize = declaredSize };
        if (!MercuryRomLayout.IsRomAddress(pointer))
            return metadata with { Status = MercuryPackSpriteStatus.InvalidPointer, MissingReason = "Resource pointer is outside the ROM address range." };
        if (!MercuryLz77.TryDecompress(rom, pointer, out byte[] data))
            return metadata with { Status = MercuryPackSpriteStatus.InvalidLz77, MissingReason = "The existing LZ77 decoder rejected the resource." };
        int complete = data.Length / unit;
        metadata = metadata with
        {
            CompleteFrameCount = palette ? null : complete,
            CompletePalettePages = palette ? complete : null,
            TailLength = data.Length % unit,
            Status = complete > 0 ? MercuryPackSpriteStatus.Available : MercuryPackSpriteStatus.InsufficientResource,
            MissingReason = complete > 0 ? null : palette ? "No complete 32-byte palette page." : "No complete 2048-byte tile frame.",
        };
        // Preserve every decompressed byte, including tails and resources too short to render.
        return Store(archive, path, data, metadata);
    }

    private static MercuryPackRenderedSprite Rendered(ZipArchive archive, string group, int index, bool success,
        byte[] rgba, int width, int height, bool firstFrameOnly, bool unsupported)
    {
        bool usable = success && width > 0 && height > 0 && rgba.Length == width * height * 4;
        var image = new MercuryPackSpriteBlob
        {
            Encoding = "rgba8888",
            Width = width > 0 ? width : null,
            Height = height > 0 ? height : null,
            Status = usable ? MercuryPackSpriteStatus.Available : unsupported ? MercuryPackSpriteStatus.Unsupported : MercuryPackSpriteStatus.Missing,
            MissingReason = usable ? null : unsupported ? "Type ID is not supported by the existing ROM renderer." : "The existing renderer did not produce a complete image; no fallback was substituted.",
            CompleteFrameCount = usable ? 1 : 0,
            BytesPerFrame = usable ? rgba.Length : null,
            TailLength = usable ? 0 : null,
        };
        if (usable)
            image = Store(archive, group + "/" + index.ToString(CultureInfo.InvariantCulture) + ".rgba", rgba, image);
        return new MercuryPackRenderedSprite { ResourceIndex = index, Image = image, FirstFrameOnly = firstFrameOnly };
    }

    private static MercuryPackSpriteBlob Store(ZipArchive archive, string path, byte[] data, MercuryPackSpriteBlob metadata)
    {
        using (var stream = archive.CreateEntry(path, CompressionLevel.Optimal).Open())
            stream.Write(data);
        return metadata with { Path = path, Length = data.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(data)) };
    }
}
