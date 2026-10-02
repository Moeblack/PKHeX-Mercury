using System;
using System.IO;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

/// <summary>Session-only evidence bound to the exact loaded data instance; never persisted into profiles.</summary>
internal static class MercuryEncounterContext
{
    // The current encounter research and loader support only this Mercury 1.1 ROM declaration.
    private const string SupportedRomSha256 = "131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd";
    private sealed record Context(MercuryGameData Data, MercuryEncounterEvidence Evidence);
    private static Context? _current;

    public static bool CanImport(MercuryGameData? data)
        => data is { HasSprites: true } && data.Source != "numeric"
            && string.Equals(data.RomSha256, SupportedRomSha256, StringComparison.OrdinalIgnoreCase);

    public static MercuryEncounterEvidence? TryGet(MercuryGameData data)
    {
        var context = _current;
        return context is not null && ReferenceEquals(context.Data, data)
            && string.Equals(context.Evidence.RomSha256, data.RomSha256, StringComparison.OrdinalIgnoreCase)
                ? context.Evidence : null;
    }

    public static void SetContext(MercuryGameData data, MercuryEncounterEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(evidence);
        if (!CanImport(data) || !string.Equals(evidence.RomSha256, data.RomSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Encounter evidence supports only the verified Mercury 1.1 ROM. Mercury 1.0 range and learning checks remain available without encounter evidence.");
        _current = new Context(data, evidence);
    }

    /// <summary>Cancellation is a no-op. Loading/validation failure leaves the previous context intact.</summary>
    public static MercuryEncounterEvidence? Import(MercuryGameData data, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (!CanImport(data))
            throw new InvalidDataException("Encounter evidence requires verified Mercury 1.1 ROM data. Mercury 1.0 range and learning checks remain available without encounter evidence.");
        var evidence = MercuryEncounterEvidenceLoader.Load(path, data.RomSha256);
        SetContext(data, evidence);
        return evidence;
    }

    public static void Clear() => _current = null;
}
