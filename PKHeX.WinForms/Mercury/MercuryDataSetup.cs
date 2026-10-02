using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms;

/// <summary>User-initiated setup only; does not publish data or access a fixed profile directory.</summary>
internal static class MercuryDataSetup
{
    internal const string HomeCharmapUrl = "https://sum-light.github.io/azoth-wiki/home/app.html";
    internal const string CharmapFileName = "mercury-charmap.json";

    internal static HttpClient CreateClient() => new() { Timeout = TimeSpan.FromSeconds(30) };

    internal static MercuryTextCodec ParseCharmap(string text)
    {
        var codec = MercuryTextCodec.FromCharmapJson(text);
        // The parser always adds a space; an unrelated JSON object is not a usable charmap.
        if (codec.SingleByteCount <= 1 && codec.DoubleByteCount == 0)
            throw new InvalidDataException("The name charmap contains no usable glyph mappings.");
        return codec;
    }

    internal static async Task<(string Text, MercuryTextCodec Codec)> DownloadCharmapAsync(HttpClient client)
    {
        var text = await client.GetStringAsync(HomeCharmapUrl).ConfigureAwait(false);
        return (text, ParseCharmap(text)); // Text extraction only; never executes scripts.
    }

    internal static void CacheCharmap(string directory, string text)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, CharmapFileName), text);
    }

    /// <summary>A cancelled selection is a no-op, including no network or disk writes.</summary>
    internal static async Task<MercuryGameData?> ConfigureFromRomAsync(string? romPath,
        MercuryTextCodec? existingCodec, string directory, HttpClient? client = null)
    {
        if (romPath is null)
            return null;

        var rom = await File.ReadAllBytesAsync(romPath).ConfigureAwait(false);
        string? downloadedText = null;
        var codec = existingCodec;
        if (codec is not { IsImported: true })
        {
            if (client is null)
            {
                using var ownedClient = CreateClient();
                (downloadedText, codec) = await DownloadCharmapAsync(ownedClient).ConfigureAwait(false);
            }
            else
            {
                (downloadedText, codec) = await DownloadCharmapAsync(client).ConfigureAwait(false);
            }
        }

        // Keep all parsing and persistence ahead of the caller's single Publish operation.
        // SaveProfile is not transactional: disk failures can leave partial profile/cache files.
        return await Task.Run(() =>
        {
            var data = MercuryGameData.FromRom(rom, codec);
            data.SaveProfile(directory);
            if (downloadedText is not null)
                CacheCharmap(directory, downloadedText);
            return data;
        }).ConfigureAwait(false);
    }
}
