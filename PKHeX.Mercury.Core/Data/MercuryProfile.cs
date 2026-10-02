using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Locally generated Mercury data profile. A profile is a self-contained snapshot of the ROM-derived
/// tables (plus an optional imported charmap) and is written to a user-chosen directory only; it is
/// never bundled with the public build.
/// </summary>
internal sealed class MercuryProfile
{
    public const string FormatId = "PKHeX.Mercury.Profile";
    public const int CurrentVersion = 1;
    public const string FileName = "mercury-profile.json";

    /// <summary>
    /// Local ROM cache written next to the profile when a ROM is held. This is a per-user local cache
    /// (32 MiB); it is never committed or shipped in a release.
    /// </summary>
    public const string RomCacheFileName = "rom-cache.gba";

    public string Format { get; set; } = FormatId;

    public int Version { get; set; } = CurrentVersion;

    public string RomSha256 { get; set; } = string.Empty;

    /// <summary>"rom", "research", "profile" or "numeric".</summary>
    public string Source { get; set; } = "rom";

    /// <summary>
    /// Relative file name of the local ROM cache (<see cref="RomCacheFileName"/>) written next to the
    /// profile. Resolved against the profile directory on load and re-verified by SHA-256. Never an
    /// absolute developer path.
    /// </summary>
    public string? RomPath { get; set; }

    /// <summary>Optional imported charmap (hex code -&gt; Unicode text), stored locally.</summary>
    public Dictionary<string, string>? Charmap { get; set; }

    public string[] AbilityNames { get; set; } = [];

    public List<ProfileSpecies> Species { get; set; } = [];

    public List<ProfileMove> Moves { get; set; } = [];

    public List<ProfileItem> Items { get; set; } = [];

    /// <summary>Optional growth-rate experience rows (6 x 101). Empty when unavailable.</summary>
    public uint[][] Growth { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static MercuryProfile Load(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Mercury profile not found: {path}", path);

        string json = File.ReadAllText(path);
        MercuryProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<MercuryProfile>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Mercury profile is not valid JSON: {path}", ex);
        }

        if (profile is null)
            throw new InvalidDataException($"Mercury profile is empty: {path}");
        if (!string.Equals(profile.Format, FormatId, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported profile format '{profile.Format}' in {path}.");
        if (profile.Version > CurrentVersion)
            throw new InvalidDataException($"Profile version {profile.Version} is newer than supported ({CurrentVersion}).");
        return profile;
    }

    public void Save(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        string json = JsonSerializer.Serialize(this, Options);
        File.WriteAllText(path, json);
    }

    public sealed class ProfileSpecies
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int[] BaseStats { get; set; } = new int[6];
        public byte GenderRatio { get; set; }
        public byte GrowthRate { get; set; }
        public byte BaseFriendship { get; set; }
        public int[] Abilities { get; set; } = new int[3];
        public int[] AbilityNameIndices { get; set; } = new int[3];
        public List<int[]> LevelUp { get; set; } = [];
        public int[] Tmhm { get; set; } = [];
        public int[] Tutor { get; set; } = [];
        public bool HasData { get; set; }
    }

    public sealed class ProfileMove
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte Type { get; set; }
        public byte Power { get; set; }
        public byte PP { get; set; }
        public byte Accuracy { get; set; }
        public sbyte Priority { get; set; }
    }

    public sealed class ProfileItem
    {
        public int Id { get; set; }
        public int EmbeddedId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
