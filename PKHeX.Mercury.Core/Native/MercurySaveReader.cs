using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Native <see cref="ISaveReader"/> for Mercury 1.1 saves.
/// <para>
/// Recognition conditions (these are not a proof that the format is unique): the host must supply a
/// <see cref="MercuryGameData"/> whose <see cref="MercuryGameData.RomSha256"/> equals the supported build,
/// the input length must be 0x20000 or 0x20010 bytes, and at least one slot must show the full unique
/// 14-section signature set. <see cref="MercurySave.Load"/> then additionally requires section checksums
/// and uniform counters.
/// </para>
/// <para>
/// Limitation: a retail Gen3 save uses the same 0x08012025 section signature and can also be 128 KiB, so the
/// section shape is not by itself a Mercury discriminator. The loaded exact-ROM profile is what keeps this
/// reader from claiming ordinary retail files while Mercury is active; a 128 KiB retail save opened with a
/// Mercury profile loaded is not distinguished beyond the structural checks here. When any condition fails,
/// <see cref="TryRead"/> returns false and the input is left to the built-in handlers; the reader does not
/// accept and then silently corrupt a file it did not fully validate. Optional 16-byte RTC trailers are
/// recognized and preserved.
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

        // Require a loaded profile from the exact supported ROM before claiming a raw Gen3-sized file.
        if (!string.Equals(_gameData.RomSha256, MercuryRomLayout.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!HasMercurySectionShape(data.Span))
            return false;

        MercurySaveFile mercury;
        try
        {
            mercury = new MercurySaveFile(data.ToArray(), _gameData);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        if (SaveUtil.TryGetSaveFileBuiltIn(data, out var retail, path) && retail is SAV3 && retail.ChecksumsValid)
            mercury.RetailAlternative = retail;
        result = mercury;
        return true;
    }

    /// <summary>
    /// Cheap structural gate before the full <see cref="MercurySave.Load"/> validation: at least one slot
    /// must expose the Mercury section signature with a complete set of distinct valid section ids.
    /// </summary>
    private static bool HasMercurySectionShape(ReadOnlySpan<byte> data)
    {
        if (data.Length < SaveSize)
            return false;

        for (int slotBase = 0; slotBase <= 14; slotBase += 14)
        {
            if (SlotHasShape(data, slotBase))
                return true;
        }
        return false;
    }

    private static bool SlotHasShape(ReadOnlySpan<byte> data, int slotBase)
    {
        var seen = new bool[14];
        for (int i = 0; i < 14; i++)
        {
            int sector = slotBase + i;
            int sigOff = (sector * 0x1000) + MercurySaveLayout.SectionSignatureOffset;
            uint signature = (uint)(data[sigOff] | (data[sigOff + 1] << 8) | (data[sigOff + 2] << 16) | (data[sigOff + 3] << 24));
            if (signature != MercurySaveLayout.SectionSignature)
                return false;

            int idOff = (sector * 0x1000) + MercurySaveLayout.SectionIdOffset;
            int id = data[idOff] | (data[idOff + 1] << 8);
            if ((uint)id >= 14u || seen[id])
                return false;
            seen[id] = true;
        }
        return true;
    }
}
