using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace PKHeX.Mercury.Core;

/// <summary>Exports user-supplied verified ROM data to a new local directory, without installing it.</summary>
public static class MercuryDataPackExporter
{
    public static MercuryDataPackManifest ExportFromRomFile(MercuryGameData data, string sourceRomPath, string outputDirectory)
        => Export(data, File.ReadAllBytes(sourceRomPath), outputDirectory);

    /// <summary>
    /// The destination must not exist. Only an exclusively created sibling staging directory is written
    /// or cleaned up. SaveProfile's temporary ROM copy is removed there, never from the source/profile.
    /// </summary>
    public static MercuryDataPackManifest Export(MercuryGameData data, byte[] sourceRom, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(sourceRom);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        string sha = Convert.ToHexStringLower(SHA256.HashData(sourceRom));
        if (sourceRom.Length != MercuryRomLayout.RomSize ||
            !string.Equals(sha, MercuryRomLayout.ExpectedSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sha, data.RomSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The source ROM does not match the supported ROM and loaded data.");
        if (!data.HasSprites || !data.HasGrowthTables || !data.Text.IsImported || data.Source == "numeric")
            throw new InvalidDataException("Pack export requires loaded ROM data, growth tables and an imported charmap.");

        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        if (File.Exists(target) || Directory.Exists(target))
            throw new IOException("Pack export never overwrites an existing destination.");
        string parent = Path.GetDirectoryName(target) ?? throw new ArgumentException("A pack requires a parent directory.", nameof(outputDirectory));
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, ".mercury-pack-" + Guid.NewGuid().ToString("N"));
        // Only this newly generated sibling directory is eligible for cleanup.
        if (Directory.Exists(stage) || File.Exists(stage))
            throw new IOException("The unique pack staging path already exists.");
        Directory.CreateDirectory(stage);
        try
        {
            data.SaveProfile(stage); // Reuse the sole numeric-table serializer; no duplicated table mapping.
            var profile = MercuryProfile.Load(stage);
            string copiedRom = Path.Combine(stage, MercuryProfile.RomCacheFileName);
            if (!File.Exists(copiedRom) || !string.Equals(HashFile(copiedRom), sha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The loaded data's ROM copy differs from the supplied source ROM.");
            profile.RomPath = null;
            profile.Save(stage);
            File.Delete(copiedRom); // Only the exclusively generated copy, never the input ROM.

            var locations = data.GetLocationIdentifiers().Select(z => new MercuryPackLocation(z.Id, z.State, z.Name)).ToArray();
            File.WriteAllText(Path.Combine(stage, MercuryDataPackManifest.LocationsFileName),
                JsonSerializer.Serialize(locations, MercuryDataPackJson.Options));
            var sprites = MercuryPackSpriteExporter.Export(sourceRom, Path.Combine(stage, MercuryDataPackManifest.SpritesFileName));
            var manifest = new MercuryDataPackManifest
            {
                Format = MercuryDataPackManifest.FormatId,
                Version = MercuryDataPackManifest.CurrentVersion,
                SupportedRomSha256 = sha,
                SourceRomLength = sourceRom.Length,
                Source = new MercuryDataPackSource("local-verified-rom-extraction", data.Source, true,
                    "Locally extracted from the user's verified ROM and imported charmap. No full ROM is included. File hashes verify integrity only, not redistribution permission or independent ROM-origin certification."),
                Counts = new MercuryDataPackCounts(profile.Species.Count, profile.Moves.Count, profile.Items.Count,
                    profile.AbilityNames.Length, profile.Growth.Length, MercuryRomLayout.MaxLevel + 1, locations.Length, sprites),
                Files = [Describe(stage, MercuryDataPackManifest.ProfileFileName),
                    Describe(stage, MercuryDataPackManifest.LocationsFileName), Describe(stage, MercuryDataPackManifest.SpritesFileName)],
            };
            File.WriteAllText(Path.Combine(stage, MercuryDataPackManifest.FileName), JsonSerializer.Serialize(manifest, MercuryDataPackJson.Options));
            MercuryDataPackValidator.Validate(stage);
            Directory.Move(stage, target);
            return manifest;
        }
        finally
        {
            if (Directory.Exists(stage))
                Directory.Delete(stage, recursive: true);
        }
    }

    private static MercuryDataPackFile Describe(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        return new MercuryDataPackFile(name, new FileInfo(path).Length, HashFile(path));
    }

    internal static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
