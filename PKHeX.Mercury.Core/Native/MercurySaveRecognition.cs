using System;
using System.Collections.Generic;
using System.IO;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>
/// How a byte input relates to the Mercury and retail Gen3 save layouts.
/// <para>
/// These states are an explicit naming of what the input bytes <i>can</i> be parsed as; they are not a new
/// uniqueness proof. Mercury 1.1 and retail Gen3 share the 0x08012025 section signature and a 128 KiB size,
/// so the same bytes can legitimately satisfy both layouts (<see cref="Ambiguous"/>).
/// </para>
/// </summary>
public enum MercurySaveRecognitionState
{
    /// <summary>Neither layout parsed the input.</summary>
    Neither,

    /// <summary>Only the Mercury structural validation accepted the input.</summary>
    MercuryCandidate,

    /// <summary>Only the built-in parser accepted the input as a checksum-valid SAV3.</summary>
    RetailCandidate,

    /// <summary>Both layouts accepted the input; the actual format requires an explicit host/user choice.</summary>
    Ambiguous,
}

/// <summary>
/// Byte-level recognition result for a save input.
/// <para>
/// The classification is derived only from the input bytes: <see cref="MercurySave.Load"/> structural
/// validation (sections, checksums, counters) for the Mercury side, and the built-in Gen3 parser's
/// checksum-valid <see cref="SAV3"/> interpretation for the retail side.
/// <see cref="MercuryGameData"/> availability is deliberately not part of the classification: it only decides
/// whether an editable Mercury object can be built for a candidate (see <see cref="MercuryCandidate"/>).
/// A <see cref="MercuryGameData"/> whose <see cref="MercuryGameData.RomVersion"/> does not authorize save editing
/// (including <see cref="MercuryGameData.NumericOnly"/>) can still yield a Mercury candidate state, but
/// cannot be turned into an editable Mercury object here.
/// </para>
/// </summary>
public sealed class MercurySaveRecognition
{
    private static readonly IReadOnlyList<string> EmptyEvidence = [];

    internal MercurySaveRecognition(
        MercurySaveRecognitionState state,
        MercurySaveFile? mercuryCandidate,
        SaveFile? retailCandidate,
        IReadOnlyList<string>? evidence = null)
    {
        State = state;
        MercuryCandidate = mercuryCandidate;
        RetailCandidate = retailCandidate;
        Evidence = evidence ?? EmptyEvidence;
    }

    /// <summary>The named relationship between the input bytes and the two layouts.</summary>
    public MercurySaveRecognitionState State { get; }

    /// <summary>
    /// An editable Mercury object when the input is a Mercury candidate and matching game data was supplied;
    /// otherwise null. Null does not contradict a <see cref="MercurySaveRecognitionState.MercuryCandidate"/>
    /// or <see cref="MercurySaveRecognitionState.Ambiguous"/> state, because data availability is separate
    /// from byte classification.
    /// </summary>
    public MercurySaveFile? MercuryCandidate { get; }

    /// <summary>The built-in checksum-valid SAV3 interpretation, when the retail parser accepted the input; otherwise null.</summary>
    public SaveFile? RetailCandidate { get; }

    /// <summary>Human-readable notes describing how the classification was reached (never a uniqueness claim).</summary>
    public IReadOnlyList<string> Evidence { get; }

    /// <summary>True when the input was accepted by the Mercury structural validation.</summary>
    public bool IsMercuryCandidate => State is MercurySaveRecognitionState.MercuryCandidate or MercurySaveRecognitionState.Ambiguous;

    /// <summary>True when the built-in parser produced a checksum-valid SAV3 interpretation.</summary>
    public bool IsRetailCandidate => State is MercurySaveRecognitionState.RetailCandidate or MercurySaveRecognitionState.Ambiguous;

    /// <summary>
    /// Classifies the input bytes without claiming ownership. Optionally builds an editable Mercury object when
    /// <paramref name="gameData"/> identifies a ROM version that authorizes save editing; classification never depends on that data.
    /// </summary>
    public static MercurySaveRecognition Analyze(ReadOnlySpan<byte> data, MercuryGameData? gameData = null, string? path = null)
    {
        var evidence = new List<string>();

        byte[] bytes = data.ToArray();

        bool mercuryAccepted = false;
        try
        {
            _ = MercurySave.Load(bytes);
            mercuryAccepted = true;
            evidence.Add("MercurySave.Load accepted the section/checksum/counter structure.");
        }
        catch (InvalidDataException ex)
        {
            evidence.Add($"MercurySave.Load rejected the input: {ex.Message}");
        }

        SaveFile? retail = null;
        if (SaveUtil.TryGetSaveFileBuiltIn(bytes, out var builtIn, path) && builtIn is SAV3 sav3 && sav3.ChecksumsValid)
        {
            retail = sav3;
            evidence.Add($"Built-in parser produced a checksum-valid {sav3.GetType().Name} interpretation.");
        }
        else
        {
            evidence.Add("Built-in parser produced no checksum-valid SAV3 interpretation.");
        }

        // Data availability is separate from classification. Only build an editable object when the caller
        // supplied game data whose ROM descriptor permits save editing; NumericOnly / unknown data cannot do this.
        MercurySaveFile? mercury = null;
        if (mercuryAccepted)
        {
            if (gameData is null)
                evidence.Add("Mercury candidate detected; no game data supplied, so no editable Mercury object was built.");
            else if (gameData.RomVersion?.CanEditSave != true)
                evidence.Add("Mercury candidate detected; supplied game data does not identify a ROM version supporting save editing, so no editable Mercury object was built.");
            else
            {
                try
                {
                    mercury = new MercurySaveFile(bytes, gameData);
                }
                catch (InvalidDataException ex)
                {
                    evidence.Add($"Mercury candidate detected; editable object build failed: {ex.Message}");
                }
            }
        }

        var state = (mercuryAccepted, retail is not null) switch
        {
            (true, true) => MercurySaveRecognitionState.Ambiguous,
            (true, false) => MercurySaveRecognitionState.MercuryCandidate,
            (false, true) => MercurySaveRecognitionState.RetailCandidate,
            _ => MercurySaveRecognitionState.Neither,
        };

        return new MercurySaveRecognition(state, mercury, retail, evidence);
    }
}
