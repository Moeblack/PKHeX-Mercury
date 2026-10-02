using System;
using System.Collections.Generic;
using PKHeX.Core;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms.Controls;

/// <summary>
/// Format-specific plumbing for the Mercury ROM hack.
/// <para>
/// The Mercury entity shares the Gen3 <see cref="EntityContext"/> but has its own 58/100-byte record
/// layout and internal id spaces. These methods are selected by <see cref="PKMEditor.SetPKMFormatMode"/>
/// before the context-only retail load/save set, so the original PK3 <c>G3PKM</c> path is never used.
/// All upstream controls, handlers and styling remain in place; only the data source changes.
/// </para>
/// </summary>
public partial class PKMEditor
{
    private void PopulateFieldsMercury()
    {
        if (Entity is not MercuryPKM pk)
            throw new FormatException(nameof(Entity));

        LoadMisc3(pk); // PID / nature / gender / language / ball / origin / met location
        LoadMisc1(pk); // species / level / exp / nickname / OT / IVs / EVs / moves
        LoadMisc2(pk); // pokerus / egg / held item / friendship

        if (CB_Ability.Items.Count > 0)
            CB_Ability.SelectedIndex = Math.Clamp(GetMercuryAbilitySlot(pk), 0, CB_Ability.Items.Count - 1);

        LoadPartyStats(pk);
        UpdateStats();
    }

    private MercuryPKM PrepareMercury()
    {
        if (Entity is not MercuryPKM pk)
            throw new FormatException(nameof(Entity));

        SaveMisc3(pk); // PID first, then nature/gender derive from it
        SaveMisc2(pk); // egg flag before EXP is stored
        SaveMisc1(pk);

        // Slot index (0/1/2) rather than a stored id: RefreshAbility resolves the effective stored ability.
        pk.RefreshAbility(CB_Ability.SelectedIndex);

        SavePartyStats(pk);
        pk.RefreshChecksum();
        return pk;
    }

    /// <summary>
    /// Builds the complete byte-indexed Mercury location list, including unknown values and slots
    /// with no valid name pointer. A missing name never removes the stored value from the dropdown.
    /// </summary>
    private static List<ComboItem> BuildMercuryLocationList()
    {
        var names = GameInfo.Strings.MercuryLocationNames ?? throw new InvalidOperationException("Mercury location names are not loaded.");
        var list = new List<ComboItem>(names.Length);
        for (int i = 0; i < names.Length; i++)
            list.Add(new ComboItem(names[i], i));
        return list;
    }

    /// <summary>
    /// Gets the ability slot index (0/1/2) from the stored <see cref="PKM.AbilityNumber"/> bits (1/2/4),
    /// so two slots sharing the same id cannot be mistaken for each other.
    /// </summary>
    private static int GetMercuryAbilitySlot(MercuryPKM pk) => pk.AbilityNumber switch
    {
        4 => 2, // hidden ability
        2 => 1, // second ability
        _ => 0, // first ability
    };

    /// <summary>
    /// Builds the ability dropdown using the ROM's per-species display name for each slot while storing
    /// the genuine ability id as the item value (the name-pool alias index is never written to the slot).
    /// </summary>
    private static List<ComboItem> BuildMercuryAbilityList(MercuryPKM pk)
    {
        var pi = pk.PersonalInfo;
        int count = pi.AbilityCount;
        var result = new List<ComboItem>(count);
        var data = pk.GameData;
        for (int i = 0; i < count; i++)
        {
            int stored = pi.GetAbilityAtIndex(i);
            string name = data.AbilityName(pk.Species, i);
            char suffix = i == 2 ? 'H' : (char)('1' + i);
            result.Add(new ComboItem($"{name} ({suffix})", stored));
        }
        return result;
    }

    /// <summary>
    /// Hides/shows the original editor controls according to what the Mercury record actually stores.
    /// Unsupported fields follow the upstream capability convention instead of offering fake writable storage.
    /// </summary>
    private void ApplyMercuryInterface()
    {
        // Forms are encoded as distinct internal species ids; there is no separate form field.
        CB_Form.Enabled = CB_Form.Visible = Label_Form.Visible = false;
        FA_Form.Visible = FA_Form.TabStop = false;
        L_FormArgument.Visible = false;

        // No fateful encounter / contest / ribbon storage in the 58-byte record.
        CHK_Fateful.Visible = false;
        BTN_Ribbons.Visible = false;
        BTN_Medals.Visible = false;

        // No extra-byte block in the 58/100-byte record.
        FLP_ExtraBytes.Visible = false;
        L_ExtraBytes.Visible = false;
        TB_ExtraByte.Visible = false;

        if (Hidden_TC.TabPages.Contains(Hidden_Cosmetic))
        {
            Hidden_TC.TabPages.Remove(Hidden_Cosmetic);
            TC_Editor.TabPages.Remove(Tab_Cosmetic);
        }
    }

    /// <summary>Mercury legality is paused; clear warnings without reporting a legal result.</summary>
    private void UpdateMercuryLegality(MercuryPKM pk)
    {
        MC_Move1.HideLegality = MC_Move2.HideLegality = MC_Move3.HideLegality = MC_Move4.HideLegality = true;
        PB_WarnRelearn1.Visible = PB_WarnRelearn2.Visible = PB_WarnRelearn3.Visible = PB_WarnRelearn4.Visible = false;
        LegalityChanged?.Invoke(false, EventArgs.Empty);
    }

    /// <summary>
    /// Experience curve backed by the ROM growth table for a specific Mercury species.
    /// </summary>
    private sealed class MercuryExperienceScale : ExperienceBar.IExperienceScale
    {
        private readonly MercuryGameData _data;
        private readonly ushort _species;

        public MercuryExperienceScale(MercuryPKM pk)
        {
            _data = pk.GameData;
            _species = pk.Species;
        }

        public int MaxLevel => 100; // MercuryRomLayout.MaxLevel
        public uint GetTotalEXP(byte level) => _data.GetExperience(_species, level);
        public uint GetEXPToNext(byte level)
        {
            if (level >= MaxLevel)
                return 0;
            return _data.GetExperience(_species, (byte)(level + 1)) - GetTotalEXP(level);
        }

        public byte GetLevel(uint exp) => _data.GetLevel(_species, exp);
    }
}
