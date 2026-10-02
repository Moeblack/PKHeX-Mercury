using System;
using System.Collections.Generic;
using PKHeX.Core;
using static PKHeX.Core.InventoryType;

namespace PKHeX.Mercury.Core;

/// <summary>
/// Mercury (Pokémon Mercury 1.1) inventory bag.
/// <para>
/// Five pouches plus the PC item box share a single 3232-byte inventory buffer. The offsets and
/// capacities were proven from the ROM item-table accessors (0x09D3E49C entry list, 0x09DD7240
/// capacities): Items 450, KeyItems 75, Balls 50, TM/HMs 128, Berries 75, PC 30.
/// </para>
/// <para>
/// Counts are read directly (<c>ldrh</c> at 0x08099DA0); the retail FR/LG security-key XOR is not
/// applied, so <see cref="InventoryPouch3.SecurityKey"/> stays 0 for every pouch.
/// </para>
/// </summary>
public sealed class MercuryPlayerBag : PlayerBag
{
    /// <summary>Unified inventory buffer size in bytes (five pouches + PC, 4 bytes per slot).</summary>
    public const int InventorySize = 3232;

    private readonly MercurySaveFile _sav;
    private readonly MercuryItemStorage _info;

    public override IItemStorage Info => _info;
    public override IReadOnlyList<InventoryPouch3> Pouches { get; }

    /// <param name="sav">Mercury save whose backing container owns the inventory bytes.</param>
    public MercuryPlayerBag(MercurySaveFile sav)
    {
        ArgumentNullException.ThrowIfNull(sav);
        _sav = sav;
        _info = new MercuryItemStorage(sav);
        Pouches =
        [
            new(0x000, 450, 999, _info, Items),
            new(0x708, 075, 999, _info, KeyItems),
            new(0x834, 050, 999, _info, Balls),
            new(0x8FC, 128, 999, _info, TMHMs),
            new(0xAFC, 075, 999, _info, Berries),
            new(0xC28, 030, 999, _info, PCItems),
        ];
        Pouches.LoadAll(_sav.Backend.GetInventoryData());
    }

    /// <summary>
    /// Writes this bag's pouches back to another Mercury save's inventory buffer.
    /// </summary>
    public override void CopyTo(SaveFile sav)
    {
        if (sav is not MercurySaveFile target)
        {
            throw new ArgumentException(
                "Mercury inventory can only be copied to a MercurySaveFile.",
                nameof(sav));
        }

        var buffer = target.Backend.GetInventoryData();
        Pouches.SaveAll(buffer);
        target.Backend.SetInventoryData(buffer);
        target.State.Edited = true;
    }

    /// <summary>
    /// Item-storage view over the Mercury ROM item table. <see cref="GetItems"/> returns the
    /// table indices (<see cref="MercuryItem.Id"/>) grouped by the ROM's pocket field.
    /// </summary>
    private sealed class MercuryItemStorage(MercurySaveFile sav) : IItemStorage
    {
        private readonly MercurySaveFile _sav = sav;
        private readonly ushort[]?[] _cache = new ushort[]?[6];

        /// <summary>
        /// Structural range check only: an item index is accepted when it lies within
        /// <c>0..<see cref="SaveFile.MaxItemID"/></c>. Retail obtainability rules are intentionally not
        /// applied here (Mercury legality was suspended by the user); this is <b>not</b> a proof that
        /// the item is actually obtainable in-game.
        /// </summary>
        public bool IsLegal(InventoryType type, int itemIndex, int itemCount)
            => (uint)itemIndex <= (uint)_sav.MaxItemID;

        /// <inheritdoc/>
        public ReadOnlySpan<ushort> GetItems(InventoryType type)
        {
            int pocket = PocketOf(type);
            return _cache[pocket] ??= Build(pocket);
        }

        private static int PocketOf(InventoryType type) => type switch
        {
            Items => 1,
            KeyItems => 2,
            Balls => 3,
            TMHMs => 4,
            Berries => 5,
            PCItems => 0, // 0 == all pockets
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        private ushort[] Build(int pocket)
        {
            var items = _sav.GameData.Items;
            var result = new ushort[items.Count];
            int count = 0;
            foreach (var item in items)
            {
                if (item.Id <= 0)
                    continue;
                if (pocket != 0 && item.Pocket != pocket)
                    continue;
                result[count++] = (ushort)item.Id;
            }
            if (count == result.Length)
                return result;
            return result.AsSpan(0, count).ToArray();
        }
    }
}
