using System;
using System.Diagnostics.CodeAnalysis;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Identifies an exact known ROM image. Recognition alone does not enable game-data loading or save editing.
/// </summary>
public sealed class MercuryRomVersion
{
    internal const string V10Sha256 = "628607dcbeac3ab471310d5472c8fbd0df250745230207c488f66adbf1a43821";
    internal const string V11Sha256 = "131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd";

    /// <summary>Known input only; its complete data and save contracts are not yet enabled.</summary>
    public static MercuryRomVersion V1_0 { get; } = new("Mercury 1.0", V10Sha256, 0x2000000, false, false);

    /// <summary>The currently supported game-data and save-editing build.</summary>
    public static MercuryRomVersion V1_1 { get; } = new("Mercury 1.1", V11Sha256, 0x2000000, true, true);

    public string Label { get; }
    public string Sha256 { get; }
    public int RomSize { get; }
    public bool CanReadGameData { get; }
    public bool CanEditSave { get; }

    private MercuryRomVersion(string label, string sha256, int romSize, bool canReadGameData, bool canEditSave)
    {
        Label = label;
        Sha256 = sha256;
        RomSize = romSize;
        CanReadGameData = canReadGameData;
        CanEditSave = canEditSave;
    }

    /// <summary>
    /// Looks up a known SHA-256 without granting any capability. Callers must separately check the
    /// image size and the capability required by their operation; labels and ROM headers are not identifiers.
    /// </summary>
    public static bool TryGetBySha256(string? sha256, [NotNullWhen(true)] out MercuryRomVersion? version)
    {
        if (string.Equals(sha256, V10Sha256, StringComparison.OrdinalIgnoreCase))
            version = V1_0;
        else if (string.Equals(sha256, V11Sha256, StringComparison.OrdinalIgnoreCase))
            version = V1_1;
        else
            version = null;
        return version is not null;
    }
}
