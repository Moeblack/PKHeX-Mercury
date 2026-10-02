using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Integrity inventory for a locally extracted data pack, not a license or an independent attestation
/// of its origin. The pack contains no full ROM and is not installed or published by this API.
/// </summary>
public sealed class MercuryDataPackManifest
{
    public const string FormatId = "PKHeX.Mercury.DataPack";
    public const int CurrentVersion = 1;
    public const string FileName = "mercury-data-pack.json";
    public const string ProfileFileName = "mercury-profile.json";
    public const string LocationsFileName = "locations.json";
    public const string SpritesFileName = "sprites.zip";

    public required string Format { get; init; }
    public required int Version { get; init; }
    public required string SupportedRomSha256 { get; init; }
    public required long SourceRomLength { get; init; }
    public required MercuryDataPackSource Source { get; init; }
    public required MercuryDataPackCounts Counts { get; init; }
    public required List<MercuryDataPackFile> Files { get; init; }
}

public sealed record MercuryDataPackFile(string Path, long Length, string Sha256);

public sealed record MercuryDataPackSource(string Method, string DataSource, bool ImportedCharmap, string Notice);

public sealed record MercuryDataPackCounts(int Species, int Moves, int Items, int AbilityNames,
    int GrowthRows, int GrowthLevels, int Locations, MercuryPackSpriteCounts Sprites);

/// <summary>Preserves the location's exact identifier and evidence state, not a localized fallback label.</summary>
public sealed record MercuryPackLocation(int Id, MercuryIdentifierState State, string? Name);

internal static class MercuryDataPackJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };
}
