using System;
using System.ComponentModel;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms.Controls;

public sealed partial class PKMEditor
{
    private ToolTip MercuryIdentifierTips = null!;
    private bool MercuryIdentifierTipFormat;
    private MercuryGameData? MercuryIdentifierTipData;
    private string? MercuryIdentifierTipLanguage;
    private MercuryIdentifier[] MercuryBallTipEntries = [];
    private MercuryIdentifier[] MercuryLocationTipEntries = [];
    private MercuryIdentifier[] MercuryOriginTipEntries = [];

    private void InitializeMercuryIdentifierTips()
    {
        // Separate from the native ID tooltips, with the same component-container lifetime as this editor.
        components ??= new Container();
        MercuryIdentifierTips = new ToolTip(components);
        CB_Ball.SelectedValueChanged += RefreshMercuryIdentifierTips;
        CB_MetLocation.SelectedValueChanged += RefreshMercuryIdentifierTips;
        CB_GameOrigin.SelectedValueChanged += RefreshMercuryIdentifierTips;
    }

    private void SetMercuryIdentifierTipFormat(PKM pk)
    {
        MercuryIdentifierTipFormat = pk is MercuryPKM;
        MercuryIdentifierTips.RemoveAll();
        MercuryIdentifierTipData = null;
    }

    private void RefreshMercuryIdentifierTips(object? sender, EventArgs e) => RefreshMercuryIdentifierTips();

    private void RefreshMercuryIdentifierTips()
    {
        if (!MercuryIdentifierTipFormat || Entity is not MercuryPKM pk)
        {
            MercuryIdentifierTips.RemoveAll();
            return;
        }

        var data = pk.GameData;
        string language = GameInfo.CurrentLanguage;
        if (!ReferenceEquals(MercuryIdentifierTipData, data) || MercuryIdentifierTipLanguage != language)
        {
            MercuryBallTipEntries = data.GetBallIdentifiers(language);
            MercuryLocationTipEntries = data.GetLocationIdentifiers(language);
            MercuryOriginTipEntries = MercuryIdentifierCatalog.CreateOrigins(language);
            MercuryIdentifierTipData = data;
            MercuryIdentifierTipLanguage = language;
        }

        // Selection supplies the exact stored ID. Refreshing evidence must never write to the entity.
        SetMercuryIdentifierTip(CB_Ball, MercuryBallTipEntries);
        SetMercuryIdentifierTip(CB_MetLocation, MercuryLocationTipEntries);
        SetMercuryIdentifierTip(CB_GameOrigin, MercuryOriginTipEntries);
    }

    private void SetMercuryIdentifierTip(ComboBox combo, MercuryIdentifier[] entries)
    {
        string? note = combo.SelectedValue is int id && (uint)id < (uint)entries.Length
            ? entries[id].EvidenceNote : null;
        MercuryIdentifierTips.SetToolTip(combo, note);
    }
}
