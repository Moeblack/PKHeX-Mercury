using System;
using System.Buffers.Text;
using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

/// <summary>Four published reference records, not a generator, identity certificate or general PMH1 importer.</summary>
public static class MercuryDistributionCatalog
{
    public const string SourceCommit = "274fcd4dbd5ad7c06af9705c5f76796d853a275c";
    public const string SourceUrl = "https://github.com/Sum-Light/azoth-wiki/blob/" + SourceCommit + "/docs/distribution/index.md";

    public static IReadOnlyList<MercuryDistributionReference> Entries { get; } = Array.AsReadOnly<MercuryDistributionReference>(
    [
        new("梦想成为假面骑士的芭瓢虫", "PMH1.MAAAADHUAAABJAprAmj_AAAAAAAJKw_t_wAAAKUAAABkAAAAADIZIcAAAAAAAAAAAAAAAAAA____vw"),
        new("竹兰的圆陆鲨", "PMH1.gAAAAAEAAAAPvgjuHeD_AAAAAAAQwggx_wAAAPABAACGAAAAADIBXbGgQAAAAAAAAAAAAAAA____vw"),
        new("壮壮妈", "PMH1.HAIAADHUAAAC1AV9HfH_AAAAAAAJKw_t_wAAAIMFAACGAAAAADIaIaxAAwAAAAAAAAAAAAAA____Pw"),
        new("Lv.5 新叶喵", "PMH1.TgAAADHUAAAOTQ8GFDX_AAAAAAAJKw_t_wAAAIAFAACGAAAAADIXCpwwJQAAAAAAAAAAAAAA____vw"),
    ]);

    /// <summary>Compares the complete canonical boxed record; changed fields are not guessed or ignored.</summary>
    public static MercuryDistributionReference? Match(MercuryPokemon pk)
    {
        ArgumentNullException.ThrowIfNull(pk);
        var bytes = pk.ToBoxBytes();
        foreach (var entry in Entries)
        {
            if (entry.Matches(bytes))
                return entry;
        }
        return null;
    }
}

/// <summary>Immutable metadata with privately retained reference bytes. Matching proves content equality only.</summary>
public sealed class MercuryDistributionReference
{
    private readonly byte[] _bytes;

    public string Label { get; }
    public string SourceUrl => MercuryDistributionCatalog.SourceUrl;
    public string SourceCommit => MercuryDistributionCatalog.SourceCommit;
    public string Pmh1Payload { get; }

    internal MercuryDistributionReference(string label, string pmh1Payload)
    {
        if (!pmh1Payload.StartsWith("PMH1.", StringComparison.Ordinal))
            throw new ArgumentException("Published distribution reference requires a PMH1 prefix.", nameof(pmh1Payload));
        _bytes = Base64Url.DecodeFromChars(pmh1Payload.AsSpan(5));
        if (_bytes.Length != MercuryPokemon.BoxSize)
            throw new ArgumentException("Published distribution reference must contain one 58-byte boxed record.", nameof(pmh1Payload));
        Label = label;
        Pmh1Payload = pmh1Payload;
    }

    internal bool Matches(ReadOnlySpan<byte> bytes) => bytes.SequenceEqual(_bytes);
}
