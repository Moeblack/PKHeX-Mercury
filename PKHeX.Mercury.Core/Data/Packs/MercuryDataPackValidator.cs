using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PKHeX.Mercury.Core;

/// <summary>Checks local pack structure and integrity, not permission, authenticity, or gameplay legality.</summary>
public static class MercuryDataPackValidator
{
    public static MercuryDataPackManifest Validate(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string root = Path.GetFullPath(directory);
        RejectLink(root);
        string manifestPath = Path.Combine(root, MercuryDataPackManifest.FileName);
        RejectLink(manifestPath);
        var manifest = JsonSerializer.Deserialize<MercuryDataPackManifest>(File.ReadAllText(manifestPath), MercuryDataPackJson.Options)
            ?? throw new InvalidDataException("The data pack manifest is empty.");
        if (manifest.Format != MercuryDataPackManifest.FormatId || manifest.Version != MercuryDataPackManifest.CurrentVersion)
            throw new InvalidDataException("Unsupported data pack format/version.");
        if (!string.Equals(manifest.SupportedRomSha256, MercuryRomLayout.ExpectedSha256, StringComparison.OrdinalIgnoreCase) ||
            manifest.SourceRomLength != MercuryRomLayout.RomSize)
            throw new InvalidDataException("The pack's declared ROM is unsupported.");
        if (manifest.Source is null || !manifest.Source.ImportedCharmap ||
            manifest.Source.Method != "local-verified-rom-extraction" || string.IsNullOrWhiteSpace(manifest.Source.Notice))
            throw new InvalidDataException("The pack lacks its local extraction/source declaration.");
        string[] payloads = [MercuryDataPackManifest.ProfileFileName, MercuryDataPackManifest.LocationsFileName, MercuryDataPackManifest.SpritesFileName];
        if (manifest.Files is null || manifest.Files.Count != payloads.Length ||
            !manifest.Files.Select(f => f.Path).Order().SequenceEqual(payloads.Order()))
            throw new InvalidDataException("The pack must declare exactly the profile, locations and sprite archive.");
        if (Directory.GetDirectories(root).Length != 0 ||
            !Directory.GetFiles(root).Select(Path.GetFileName).Order().SequenceEqual(payloads.Append(MercuryDataPackManifest.FileName).Order()))
            throw new InvalidDataException("The pack contains missing or unrecognized files (ROM files are not allowed).");
        foreach (var file in manifest.Files)
        {
            string path = Path.Combine(root, file.Path); // Names above match the fixed allowlist exactly.
            RejectLink(path);
            if (file.Length < 0 || new FileInfo(path).Length != file.Length ||
                !string.Equals(MercuryDataPackExporter.HashFile(path), file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Pack file integrity check failed: {file.Path}");
        }

        string profilePath = Path.Combine(root, MercuryDataPackManifest.ProfileFileName);
        using (var document = JsonDocument.Parse(File.ReadAllText(profilePath)))
        {
            if (document.RootElement.EnumerateObject().Any(p => string.Equals(p.Name, "romPath", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("A portable profile must omit romPath entirely.");
        }
        var profile = MercuryProfile.Load(root);
        if (profile.Version != MercuryProfile.CurrentVersion || profile.RomPath is not null ||
            !string.Equals(profile.RomSha256, manifest.SupportedRomSha256, StringComparison.OrdinalIgnoreCase) ||
            profile.Charmap is not { Count: > 0 })
            throw new InvalidDataException("The portable profile is not a current ROM-free profile with an imported charmap.");
        if (profile.Species.Count != MercuryRomLayout.SpeciesCount || profile.Moves.Count != MercuryRomLayout.MoveCount ||
            profile.Items.Count != MercuryRomLayout.ItemCount || profile.AbilityNames.Length != MercuryRomLayout.AbilityNameCount ||
            profile.Growth.Length != MercuryRomLayout.GrowthRateCount ||
            profile.Growth.Any(row => row is null || row.Length != MercuryRomLayout.MaxLevel + 1))
            throw new InvalidDataException("The portable profile has incomplete table dimensions.");
        RequireIds(profile.Species.Select(z => z.Id).ToArray(), MercuryRomLayout.SpeciesCount);
        RequireIds(profile.Moves.Select(z => z.Id).ToArray(), MercuryRomLayout.MoveCount);
        RequireIds(profile.Items.Select(z => z.Id).ToArray(), MercuryRomLayout.ItemCount);
        var locations = JsonSerializer.Deserialize<MercuryPackLocation[]>(File.ReadAllText(Path.Combine(root, MercuryDataPackManifest.LocationsFileName)), MercuryDataPackJson.Options)
            ?? throw new InvalidDataException("The locations table is empty.");
        RequireIds(locations.Select(z => z.Id).ToArray(), 256);
        if (locations.Any(z => !Enum.IsDefined(z.State)))
            throw new InvalidDataException("An unknown location evidence state was supplied.");
        var sprites = MercuryPackSpriteExporter.Validate(Path.Combine(root, MercuryDataPackManifest.SpritesFileName));
        var expected = new MercuryDataPackCounts(profile.Species.Count, profile.Moves.Count, profile.Items.Count,
            profile.AbilityNames.Length, profile.Growth.Length, MercuryRomLayout.MaxLevel + 1, locations.Length, sprites);
        if (JsonSerializer.Serialize(manifest.Counts, MercuryDataPackJson.Options) != JsonSerializer.Serialize(expected, MercuryDataPackJson.Options))
            throw new InvalidDataException("The manifest counts do not match its payloads.");
        return manifest;
    }

    private static void RequireIds(int[] ids, int count)
    {
        if (!ids.SequenceEqual(Enumerable.Range(0, count)))
            throw new InvalidDataException($"Expected {count} ordered, unique identifiers starting at zero.");
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Data pack files/directories may not be links.");
    }
}
