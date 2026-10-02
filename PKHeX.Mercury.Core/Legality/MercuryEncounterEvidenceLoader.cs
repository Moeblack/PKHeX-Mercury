using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PKHeX.Mercury.Core;

/// <summary>Reads the existing encounter research schema without extracting ROM data or guessing missing fields.</summary>
public static class MercuryEncounterEvidenceLoader
{
    public static MercuryEncounterEvidence Load(string path, string expectedRomSha256)
        => Parse(File.ReadAllText(path), expectedRomSha256, path);

    public static MercuryEncounterEvidence Parse(string json, string expectedRomSha256, string sourcePath = "external JSON")
    {
        if (!string.Equals(expectedRomSha256, MercuryRomLayout.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Encounter evidence requires the supported Mercury ROM SHA declaration.");
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            string sha = root.GetProperty("sha256").GetString() ?? string.Empty;
            if (!string.Equals(sha, expectedRomSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Encounter JSON ROM SHA declaration does not match the requested ROM; evidence was not imported.");

            var records = new List<MercuryEncounterRecord>();
            ReadOrdinary(root.GetProperty("tables"), records);
            ReadSwarm(root.GetProperty("swarm"), records);
            var broadcast = root.GetProperty("broadcast");
            ReadBroadcast(broadcast, records);
            return new MercuryEncounterEvidence(sha, sourcePath, root.GetProperty("evidence").GetRawText(),
                broadcast.GetProperty("evidence").GetRawText(), broadcast.GetProperty("gating").GetRawText(),
                root.GetProperty("unresolved").GetRawText(), records);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Encounter evidence does not have the supported research JSON field shape.", ex);
        }
    }

    private static void ReadOrdinary(JsonElement tables, List<MercuryEncounterRecord> records)
    {
        foreach (var time in tables.EnumerateObject())
        {
            var table = time.Value;
            foreach (var map in table.GetProperty("entries").EnumerateArray())
            {
                foreach (string category in new[] { "land", "water", "rock_smash_headbutt", "fishing" })
                {
                    if (!map.TryGetProperty(category, out var area) || area.ValueKind == JsonValueKind.Null)
                        continue;
                    foreach (var slot in area.GetProperty("slots").EnumerateArray())
                    {
                        records.Add(new MercuryEncounterRecord(MercuryEncounterSource.OrdinaryTable, category,
                            slot.GetProperty("species_id").GetInt32(), Number(map, "map_group"), Number(map, "map_num"),
                            Number(map, "mapsec_id"), Number(slot, "min_level"), Number(slot, "max_level"),
                            time.Name, Text(table, "hour_band"), null, Number(slot, "slot"),
                            Text(table, "addr"), Text(map, "entry_addr"), Text(area, "info_addr"), Text(area, "wild_addr")));
                    }
                }
            }
        }
    }

    private static void ReadSwarm(JsonElement swarm, List<MercuryEncounterRecord> records)
    {
        foreach (var entry in swarm.GetProperty("entries").EnumerateArray())
        {
            // This schema has no map group/number, levels or time band. Do not borrow ordinary slots.
            records.Add(new MercuryEncounterRecord(MercuryEncounterSource.Swarm, "swarm",
                entry.GetProperty("species_id").GetInt32(), null, null, Number(entry, "mapsec_id"),
                null, null, null, null, null, null, Text(swarm, "addr"), Text(entry, "addr"), null, null));
        }
    }

    private static void ReadBroadcast(JsonElement broadcast, List<MercuryEncounterRecord> records)
    {
        foreach (var entry in broadcast.GetProperty("entries").EnumerateArray())
        {
            foreach (var set in entry.GetProperty("sets").EnumerateArray())
            {
                foreach (var slot in set.GetProperty("species").EnumerateArray())
                {
                    // Map group/number are not a proven conversion to a stored MetLocation/mapsec.
                    records.Add(new MercuryEncounterRecord(MercuryEncounterSource.Broadcast, "broadcast",
                        slot.GetProperty("species_id").GetInt32(), Number(entry, "map_group"), Number(entry, "map_num"),
                        null, null, null, null, null, Number(set, "day_of_week"), Number(slot, "slot"),
                        Text(broadcast, "addr"), Text(entry, "table_addr"), null, null));
                }
            }
        }
        foreach (var fallback in broadcast.GetProperty("fallback_tables").EnumerateObject())
        {
            foreach (var slot in fallback.Value.GetProperty("species").EnumerateArray())
            {
                records.Add(new MercuryEncounterRecord(MercuryEncounterSource.BroadcastFallback, "broadcast.fallback." + fallback.Name,
                    slot.GetProperty("species_id").GetInt32(), null, null, null, null, null, null, null, null, null,
                    Text(fallback.Value, "addr"), null, null, null));
            }
        }
    }

    private static int? Number(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetInt32() : null;

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;
}
