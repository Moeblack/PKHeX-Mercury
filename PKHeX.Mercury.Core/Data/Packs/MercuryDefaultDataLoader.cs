using System;
using System.IO;

namespace PKHeX.Mercury.Core;

public sealed record MercuryDefaultDataLoadResult(MercuryGameData? Data, Exception? PackError, Exception? ProfileError);

/// <summary>One offline attempt at the explicit pack location, then one legacy profile fallback.</summary>
public static class MercuryDefaultDataLoader
{
    public static MercuryDefaultDataLoadResult Load(string packDirectory, string profileDirectory)
    {
        Exception? packError = null;
        if (File.Exists(Path.Combine(packDirectory, MercuryDataPackManifest.FileName)))
        {
            try
            {
                return new MercuryDefaultDataLoadResult(MercuryGameData.LoadPack(packDirectory), null, null);
            }
            catch (Exception error)
            {
                packError = error;
            }
        }
        try
        {
            var data = Directory.Exists(profileDirectory) ? MercuryGameData.LoadProfile(profileDirectory) : null;
            return new MercuryDefaultDataLoadResult(data is { Species.Count: > 0 } ? data : null, packError, null);
        }
        catch (Exception error)
        {
            return new MercuryDefaultDataLoadResult(null, packError, error);
        }
    }
}
