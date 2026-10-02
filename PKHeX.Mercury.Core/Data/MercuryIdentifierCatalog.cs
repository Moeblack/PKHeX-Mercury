using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Mercury identifier evidence. Unknown and nameless slots remain selectable by their exact IDs;
/// none of the currently established evidence classifies a stored value as reserved or illegal.
/// </summary>
public static class MercuryIdentifierCatalog
{
    public static MercuryIdentifier[] CreateBalls(IEnumerable<MercuryItem> items, string language = "zh")
    {
        var entries = CreateUnknown(256);
        // 0x09D0C386..0x09D0C398 stores pocket-3 ItemId_GetType unchanged, not a retail ball ID.
        foreach (var group in items.Where(z => z.Pocket == 3 && z.Type is <= 26).GroupBy(z => z.Type!.Value))
        {
            var matches = group.ToArray();
            // Do not select a name arbitrarily, even if duplicate entries happen to share a label.
            bool unique = matches.Length == 1 && !string.IsNullOrWhiteSpace(matches[0].Name);
            entries[group.Key] = new MercuryIdentifier(group.Key,
                unique ? MercuryIdentifierState.ConfirmedMeaning : MercuryIdentifierState.Unknown,
                unique ? matches[0].Name : null)
            {
                MatchingItemIds = Array.AsReadOnly(matches.Select(z => z.Id).ToArray()),
            };
        }
        entries[27] = entries[27] with
        {
            EvidenceNote = language.StartsWith("zh", StringComparison.Ordinal)
                ? "已证运行时覆盖图像索引；存档捕获语义未查明。"
                : "Confirmed runtime image-index override; its meaning as a stored capture ball is unknown.",
        };
        return entries;
    }

    public static MercuryIdentifier[] CreateOrigins(string language)
    {
        var entries = CreateUnknown(16);
        // Creation code 0x0803DC48 reads byte 4 at 0x081E9F10. Other values are not retail versions.
        entries[4] = new MercuryIdentifier(4, MercuryIdentifierState.ConfirmedMeaning,
            language.StartsWith("zh", StringComparison.Ordinal) ? "宝可梦水银" : "Pokémon Mercury");
        // Field 0x25 getter 0x080400B4..BA reads bits 7..9, while setter 0x08040840..52
        // writes bits 7..10. Keep the four-bit stored ID, including the unexplained high bit.
        string note = language.StartsWith("zh", StringComparison.Ordinal)
            ? "编辑器保留4位原始值；已覆盖游戏getter（字段0x25）读取低3位，高位含义未查明。"
            : "The editor preserves the four-bit stored value. The observed field 0x25 getter reads the low three bits; the high bit's meaning is unknown.";
        for (int id = 0; id < entries.Length; id++)
            entries[id] = entries[id] with { ObservedGameReadValue = id & 7, EvidenceNote = note };
        return entries;
    }

    internal static MercuryIdentifier[] CreateLocations(byte[]? rom, MercuryTextCodec text, string language)
    {
        var entries = CreateUnknown(256);
        // Confirmed 0xFFFFFFFF slots in this ROM, not evidence that these stored bytes are illegal.
        for (int id = 0xDE; id <= 0xFC; id++)
            entries[id] = new MercuryIdentifier(id, MercuryIdentifierState.InvalidNamePointer);
        bool chinese = language.StartsWith("zh", StringComparison.Ordinal);
        entries[0x5E] = entries[0x5E] with
        {
            EvidenceNote = chinese
                ? "部分运行状态下游戏显示为彩虹百货大楼；此处保留普通地点名称。存档地点编号单独不足以确定该显示。"
                : "In some runtime states the game displays Celadon Department Store; the ordinary location name is retained here. The stored location ID alone does not determine that display.",
        };
        for (int id = 0xFD; id <= 0xFE; id++)
            entries[id] = entries[id] with
            {
                EvidenceNote = chinese
                    ? "摘要允许该编号，但已覆盖名称路径返回空白。"
                    : "The summary accepts this ID, but the observed name path returns blank text.",
            };
        if (rom is null)
            return entries;

        // GetMapName (0x080C4D78) subtracts 0x58 and accepts table indices 0..0xA4.
        // Historical summaries also reach 0x080C4D40, but its special name depends on current runtime state.
        for (int index = 0; index < 0xA5; index++)
        {
            if (!MercuryRomLayout.TryReadU32(rom, 0x08C2B000u + (uint)index * 4, out uint pointer))
                throw new InvalidDataException("Mercury location-name table is truncated.");
            int id = index + 0x58;
            entries[id] = MercuryRomLayout.IsRomAddress(pointer)
                ? entries[id] with { State = MercuryIdentifierState.ConfirmedMeaning,
                    Name = text.Decode(rom.AsSpan(checked((int)MercuryRomLayout.ToOffset(pointer)))) }
                : entries[id] with { State = MercuryIdentifierState.InvalidNamePointer };
        }
        return entries;
    }

    public static string[] GetNames(IEnumerable<MercuryIdentifier> entries, string language)
        => entries.Select(z => z.GetDisplayName(language)).ToArray();

    private static MercuryIdentifier[] CreateUnknown(int count)
        => Enumerable.Range(0, count).Select(id => new MercuryIdentifier(id, MercuryIdentifierState.Unknown)).ToArray();
}
