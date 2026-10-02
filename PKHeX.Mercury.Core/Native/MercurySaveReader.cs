using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Native <see cref="ISaveReader"/> for Mercury 1.1 saves.
/// <para>
/// Recognition is a byte-level candidate classification produced by <see cref="MercurySaveRecognition.Analyze"/>;
/// it does not prove that the input <i>belongs</i> to Mercury. Mercury and retail Gen3 layouts share the
/// 0x08012025 section signature and a 128 KiB size, so the same bytes can satisfy both; that case is reported as
/// <see cref="MercurySaveRecognitionState.Ambiguous"/> and requires an explicit host choice in the UI.
/// </para>
/// <para>
/// The supplied <see cref="MercuryGameData"/> only indicates whether matching game data is available to build an
/// editable Mercury object. Its <see cref="MercuryGameData.RomSha256"/> is a data-source constraint, not
/// ownership evidence, and is applied only while converting a candidate into an editable object. When the input
/// is not a Mercury candidate, <see cref="TryRead"/> returns false and leaves the input to the built-in handlers.
/// Optional 16-byte RTC trailers are recognized and preserved.
/// </para>
/// </summary>
public sealed class MercurySaveReader : ISaveReader
{
    private const int SaveSize = 0x20000;
    private const int RtcTrailerSize = 16;

    private readonly MercuryGameData _gameData;

    public MercurySaveReader(MercuryGameData gameData)
    {
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
    }

    public MercuryGameData GameData => _gameData;

    public bool IsRecognized(long dataLength) => dataLength is SaveSize or (SaveSize + RtcTrailerSize);

    public bool TryRead(Memory<byte> data, [NotNullWhen(true)] out SaveFile? result, string? path = null)
    {
        result = null;
        if (!IsRecognized(data.Length))
            return false;

        MercurySaveRecognition recognition = MercurySaveRecognition.Analyze(data.Span, _gameData, path);

        switch (recognition.State)
        {
            case MercurySaveRecognitionState.MercuryCandidate:
                result = recognition.MercuryCandidate;
                return result is not null;

            case MercurySaveRecognitionState.Ambiguous:
                if (recognition.MercuryCandidate is not { } mercury)
                    return false;
                // Both layouts parsed: keep the retail candidate attached so the UI can require an explicit choice.
                mercury.RetailAlternative = recognition.RetailCandidate;
                result = mercury;
                return true;

            case MercurySaveRecognitionState.RetailCandidate:
            case MercurySaveRecognitionState.Neither:
            default:
                return false;
        }
    }
}
