using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PKHeX.Mercury.Core;

/// <summary>The fixed Mercury 1.1 dataset shipped with this application; never reads user configuration.</summary>
internal static class MercuryBuiltInData
{
    internal const string ResourcePrefix = "PKHeX.Mercury.Core.BuiltIn.1.1.";
    internal const string ManifestSha256 = "d651b1ec87d3b6d6adabcc1f47d9dce1929a72d55602507a9d2a6826ab9d38af";
    private static readonly Lazy<MercuryDataPackReader> CachedReader = new(() => CreateReader(OpenResource));
    internal static MercuryDataPackReader Reader => CachedReader.Value;
    internal const string EncounterSha256 = "37eb8635e2952e74a2e39cfd62a20d3edb4def344f131b7ab04cada388371458";
    private static readonly Lazy<MercuryEncounterEvidence> CachedEncounters = new(LoadEncounters);
    internal static MercuryEncounterEvidence Encounters => CachedEncounters.Value;

    private static MercuryEncounterEvidence LoadEncounters()
    {
        using var input = OpenResource("encounters.json");
        using var output = new MemoryStream();
        input.CopyTo(output);
        byte[] bytes = output.ToArray();
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != EncounterSha256)
            throw new InvalidDataException("The built-in Mercury 1.1 encounter resource failed its application-pinned SHA-256 check.");
        return MercuryEncounterEvidenceLoader.Parse(Encoding.UTF8.GetString(bytes), MercuryRomVersion.V1_1.Sha256, "内置水银1.1遭遇资料");
    }

    internal static Stream OpenResource(string name)
        => typeof(MercuryBuiltInData).Assembly.GetManifestResourceStream(ResourcePrefix + name)
           ?? throw new InvalidDataException($"Missing built-in Mercury 1.1 resource: {name}");

    // Test injection returns an isolated reader only; it cannot mark arbitrary GameData trusted or replace the cache.
    internal static MercuryDataPackReader CreateReader(Func<string, Stream> openResource)
    {
        string[] names = [MercuryDataPackManifest.FileName, MercuryDataPackManifest.ProfileFileName,
            MercuryDataPackManifest.LocationsFileName, MercuryDataPackManifest.SpritesFileName];
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            using var input = openResource(name);
            using var output = new MemoryStream();
            input.CopyTo(output);
            files.Add(name, output.ToArray());
        }
        if (Convert.ToHexStringLower(SHA256.HashData(files[MercuryDataPackManifest.FileName])) != ManifestSha256)
            throw new InvalidDataException("The built-in Mercury 1.1 manifest failed its application-pinned SHA-256 check.");
        return new MercuryDataPackReader(files);
    }
}
