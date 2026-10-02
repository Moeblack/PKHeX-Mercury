using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>
/// 在真实 Core 语义之上做“盒↔队”转换与队伍写入准备。
/// 规则（Main 取证）：
/// · 队伍数值模式由 MercurySave.StatMode 给出（ROM 实际 11/12/13），必须传给 ToParty/RecalculatePartyStats。
/// · 只有统计相关字段（物种/经验/性格(PID)/IV/EV）实际改变时才重算队伍 6 项数值；
///   普通改名/亲密度等不重算，保留队伍 status/mail/currentPP。
/// · 箱→队必须重算并补齐当前 PP；队伍→队伍仅在招式槽实际改变时更新该槽 currentPP 到新招式满 PP。
/// </summary>
internal static class MonOps
{
    /// <summary>箱内 mon → 队伍形态（强制重算 6 项数值并补齐当前 PP）。</summary>
    public static MercuryPokemon ToPartyForm(MercuryPokemon source, int statMode)
    {
        var mon = source.Clone();
        try
        {
            var baseStats = ResolveBaseStats(mon.Species);
            var level = ResolveLevel(mon);
            mon.ToParty(baseStats, level, statMode);
            FillFullPp(mon);
        }
        catch
        {
            // 无 ROM 基础值时保持原状，由 Core 在 SetParty 时按零写尾部。
        }
        return mon;
    }

    /// <summary>
    /// 准备写入队伍槽的 mon：
    /// 箱来源强制转队伍形态；队伍来源仅在统计相关字段改变时重算，并按需更新改动招式槽的 currentPP。
    /// </summary>
    public static MercuryPokemon PreparePartyWrite(MercuryPokemon edited, MercuryPokemon existing, int statMode)
    {
        if (!IsPartyForm(edited))
            return ToPartyForm(edited, statMode);

        var target = edited.Clone();
        try
        {
            if (StatRelevantChanged(existing, edited))
            {
                var baseStats = ResolveBaseStats(target.Species);
                var level = ResolveLevel(target);
                target.RecalculatePartyStats(baseStats, level, statMode);
            }

            UpdateChangedMovePp(target, existing);
        }
        catch
        {
            // 保底：保留编辑后的字段，数值由 Core 决定。
        }
        return target;
    }

    /// <summary>队伍形态 → 盒形态。</summary>
    public static MercuryPokemon ToBoxForm(MercuryPokemon source)
    {
        var mon = source.Clone();
        try
        {
            mon.ToBox();
        }
        catch
        {
            // 保持原状。
        }
        return mon;
    }

    public static bool IsPartyForm(MercuryPokemon mon)
    {
        try
        {
            return mon.IsPartyForm;
        }
        catch
        {
            return false;
        }
    }

    public static MercuryPokemon EmptyBox()
    {
        try
        {
            return MercuryPokemon.FromBox(new byte[58]);
        }
        catch
        {
            return MercuryPokemon.Empty;
        }
    }

    public static MercuryPokemon EmptyParty()
    {
        try
        {
            return MercuryPokemon.FromParty(new byte[100]);
        }
        catch
        {
            return MercuryPokemon.Empty;
        }
    }

    public static void ClearSlot(MercurySave save, SlotRef slot)
    {
        if (slot.IsParty)
            save.DeleteParty(slot.Slot);
        else
            save.ClearBox(slot.Box, slot.Slot);
    }

    // ------------------------------------------------------------ 内部工具

    private static bool StatRelevantChanged(MercuryPokemon existing, MercuryPokemon edited)
    {
        if (existing.Species != edited.Species)
            return true;
        if (existing.Experience != edited.Experience)
            return true;
        if (existing.PID != edited.PID)
            return true;
        return !SameStatBytes(existing.IVs, edited.IVs, 6) || !SameStatBytes(existing.EVs, edited.EVs, 6);
    }

    private static bool SameStatBytes(byte[]? a, byte[]? b, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var va = a is not null && i < a.Length ? a[i] : (byte)0;
            var vb = b is not null && i < b.Length ? b[i] : (byte)0;
            if (va != vb)
                return false;
        }
        return true;
    }

    private static byte ResolveLevel(MercuryPokemon mon)
    {
        if (AppState.HasGrowthTables)
        {
            try
            {
                var level = AppState.Data.GetLevel(mon.Species, mon.Experience);
                if (level > 0)
                    return level;
            }
            catch
            {
                // 落到 PartyLevel。
            }
        }
        return (byte)Math.Clamp((int)mon.PartyLevel, 1, 100);
    }

    private static int[] ResolveBaseStats(int species)
    {
        try
        {
            var stats = AppState.Data.GetSpecies(species).BaseStats;
            return stats is { Length: >= 6 } ? stats : new int[6];
        }
        catch
        {
            return new int[6];
        }
    }

    /// <summary>按招式基础 PP + PPUps 计算满 PP（GBA 规则：每级 PP Up +20% 向下取整）。</summary>
    private static void FillFullPp(MercuryPokemon mon)
    {
        try
        {
            var moves = mon.Moves;
            var ppups = mon.PPUps;
            var pp = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                var id = moves is { Length: > 0 } && i < moves.Length ? moves[i] : (ushort)0;
                var up = ppups is { Length: > 0 } && i < ppups.Length ? Math.Min((int)ppups[i], 3) : 0;
                pp[i] = MaxPp(id, up);
            }
            mon.PartyCurrentPp = pp;
        }
        catch
        {
            // PartyCurrentPp 不可用时交给 Core 默认处理。
        }
    }

    /// <summary>仅当某个招式槽的招式 ID 实际改变时，把该槽 currentPP 更新为新招式满 PP；未变槽保留原值。</summary>
    private static void UpdateChangedMovePp(MercuryPokemon target, MercuryPokemon existing)
    {
        try
        {
            var newMoves = target.Moves;
            var oldMoves = existing.Moves;
            var ppUps = target.PPUps;

            var pp = target.PartyCurrentPp;
            if (pp is null || pp.Length < 4)
            {
                pp = new byte[4];
                var existingPp = existing.PartyCurrentPp;
                if (existingPp is { Length: > 0 })
                    Array.Copy(existingPp, pp, Math.Min(existingPp.Length, 4));
            }

            for (var i = 0; i < 4; i++)
            {
                var newMove = newMoves is { Length: > 0 } && i < newMoves.Length ? newMoves[i] : (ushort)0;
                var oldMove = oldMoves is { Length: > 0 } && i < oldMoves.Length ? oldMoves[i] : (ushort)0;
                if (newMove == oldMove)
                    continue;
                var up = ppUps is { Length: > 0 } && i < ppUps.Length ? Math.Min((int)ppUps[i], 3) : 0;
                pp[i] = MaxPp(newMove, up);
            }

            target.PartyCurrentPp = pp;
        }
        catch
        {
            // 保持原 PP。
        }
    }

    private static byte MaxPp(int moveId, int ppUps)
    {
        var basePp = LookupBasePp(moveId);
        if (basePp <= 0)
            return 0;
        var value = basePp + (basePp * Math.Clamp(ppUps, 0, 3)) / 5;
        return (byte)Math.Clamp(value, 0, 255);
    }

    private static int LookupBasePp(int moveId)
    {
        if (moveId <= 0)
            return 0;
        try
        {
            foreach (var move in AppState.Data.Moves)
            {
                if (move.Id == moveId)
                    return move.PP;
            }
        }
        catch
        {
            // 无招式表。
        }
        return 0;
    }
}
