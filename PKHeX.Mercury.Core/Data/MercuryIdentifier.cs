using System;
using System.Collections.Generic;

namespace PKHeX.Mercury.Core;

/// <summary>Evidence about an identifier, not a legality or writability classification.</summary>
public enum MercuryIdentifierState
{
    Unknown,
    ConfirmedMeaning,
    InvalidNamePointer,
    ConfirmedReserved,
}

/// <summary>Keeps the exact stored identifier separate from its known meaning and display name.</summary>
public sealed record MercuryIdentifier(int Id, MercuryIdentifierState State, string? Name = null)
{
    /// <summary>Matching pocket-3 item indices. Multiple matches do not establish a unique ball name.</summary>
    public IReadOnlyList<int> MatchingItemIds { get; init; } = Array.Empty<int>();

    public string GetDisplayName(string language)
    {
        bool chinese = language.StartsWith("zh", StringComparison.Ordinal);
        return State switch
        {
            MercuryIdentifierState.ConfirmedMeaning when !string.IsNullOrWhiteSpace(Name) => Name,
            MercuryIdentifierState.InvalidNamePointer => chinese ? $"名称指针无效[{Id}]" : $"Invalid name pointer [{Id}]",
            MercuryIdentifierState.ConfirmedReserved => chinese ? $"保留已证[{Id}]" : $"Confirmed reserved [{Id}]",
            _ => chinese ? $"未查明[{Id}]" : $"Unknown [{Id}]",
        };
    }
}
