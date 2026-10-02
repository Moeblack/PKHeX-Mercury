using System;
using System.Collections.Generic;
using PKHeX.Core;

namespace PKHeX.Mercury.Core;

/// <summary>Checks the current native save buffers without flushing, exporting, or running retail bulk rules.</summary>
public static class MercuryBulkLegalityAnalysis
{
    public static MercuryBulkLegalityResult Analyze(MercurySaveFile save, MercuryEncounterEvidence? encounterEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(save);
        var slots = new List<SlotCache>();
        SlotInfoLoader.AddFromSaveFile(save, slots);
        var results = new List<MercuryBulkLegalityEntry>();
        foreach (var slot in slots)
        {
            // Do not use retail BulkAnalysis.IsEmptyData or SlotCache.IsDataValid: preserve invalid nonempty records.
            if (slot.Entity.Species == 0)
                continue;
            var result = slot.Entity is MercuryPKM pk
                ? MercuryLegalityAnalysis.Analyze(pk, encounterEvidence)
                : new MercuryLegalityResult([
                    new("bulk.entity-type", MercuryCheckStatus.Invalid,
                        $"水银槽返回非MercuryPKM类型：{slot.Entity.GetType().Name}；未运行零售检查。"),
                ]);
            results.Add(new MercuryBulkLegalityEntry(slot, result));
        }
        return new MercuryBulkLegalityResult(results);
    }
}
