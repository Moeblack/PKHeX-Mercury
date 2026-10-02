using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Mercury.Core;

namespace PKHeX.WinForms.Controls;

public partial class PKMEditor
{
    private ComboBox? CB_MercuryTypeOverride;
    private Label? L_MercuryTypeOverride;
    private bool _mercuryTypeEdited;
    private ushort _mercuryTypeSpecies;
    private int _mercuryTypeSelection;

    private void ConfigureMercuryTypes(PKM pk)
    {
        if (CB_MercuryTypeOverride is null && pk is MercuryPKM)
        {
            int tabIndex = 0;
            foreach (Control control in TLP_Main.Controls)
                tabIndex = Math.Max(tabIndex, control.TabIndex + 1);
            L_MercuryTypeOverride = new Label
            {
                Name = nameof(L_MercuryTypeOverride), Text = "类型覆盖:", AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 4, 0, 2),
            };
            CB_MercuryTypeOverride = new ComboBox
            {
                Name = nameof(CB_MercuryTypeOverride), DropDownStyle = ComboBoxStyle.DropDownList,
                Width = CB_Species.Width, DropDownWidth = 280, Margin = CB_Species.Margin,
                DisplayMember = nameof(ComboItem.Text), ValueMember = nameof(ComboItem.Value),
                TabIndex = tabIndex,
            };
            int row = TLP_Main.RowCount++;
            TLP_Main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            TLP_Main.Controls.Add(L_MercuryTypeOverride, 0, row);
            TLP_Main.Controls.Add(CB_MercuryTypeOverride, 1, row);
            CB_MercuryTypeOverride.SelectionChangeCommitted += CommitMercuryTypeSelection;
            CB_Species.SelectedValueChanged += RefreshMercuryTypeForSpecies;
        }
        if (CB_MercuryTypeOverride is null)
            return;
        CB_MercuryTypeOverride.Visible = L_MercuryTypeOverride!.Visible = pk is MercuryPKM;
        CB_MercuryTypeOverride.Enabled = pk is MercuryPKM;
    }

    private static List<ComboItem> BuildMercuryTypeList()
    {
        var items = new List<ComboItem> { new("自动（由游戏按物种/PID处理）", 0) };
        // ROM 09D60E64/09D60E98 validate raw-1; 09D60EBE returns raw-1 or type 20 for raw 31.
        // The Mercury profile has no type-name table. Never substitute the retail type-name list.
        for (int type = 0; type <= 24; type++)
        {
            if (type is <= 8 or (>= 10 and <= 17) or 23 or 24)
                items.Add(new ComboItem($"类型 {type}", type + 1));
        }
        items.Add(new ComboItem("类型 20（特殊编码）", 31));
        return items;
    }

    private void LoadMercuryTypes(MercuryPKM pk)
    {
        ConfigureMercuryTypes(pk);
        var items = BuildMercuryTypeList();
        int raw = pk.TypeOverride;
        int expanded = pk.Data[0x1E] | (pk.Data[0x1F] << 8);
        // Invalid expanded data decodes to zero, but remains in the lossless party template.
        // Show that original encoding without invoking the setter or making it a writable option.
        bool unknown = expanded != 0 && expanded != 0xA500 &&
            (expanded != (0xA500 | raw) || !items.Exists(z => z.Value == raw));
        if (unknown)
            items.Add(new ComboItem($"未知原始编码 0x{expanded:X4}（保留）", -1));
        _mercuryTypeSelection = unknown ? -1 : raw;
        CB_MercuryTypeOverride!.DataSource = items;
        CB_MercuryTypeOverride.SelectedValue = _mercuryTypeSelection;
        _mercuryTypeSpecies = pk.Species;
        _mercuryTypeEdited = false;
    }

    private void CommitMercuryTypeSelection(object? sender, EventArgs e)
    {
        if (!FieldsLoaded || Entity is not MercuryPKM || CB_MercuryTypeOverride?.SelectedItem is not ComboItem item)
            return;
        if (item.Value < 0)
        {
            CB_MercuryTypeOverride.SelectedValue = _mercuryTypeSelection;
            return;
        }
        _mercuryTypeSelection = item.Value;
        _mercuryTypeEdited = true;
    }

    private void SaveMercuryTypes(MercuryPKM pk)
    {
        if (pk.Species != _mercuryTypeSpecies)
        {
            LoadMercuryTypes(pk); // Species setter already cleared the override; discard a stale UI selection.
            return;
        }
        if (!_mercuryTypeEdited)
            return;
        pk.TypeOverride = _mercuryTypeSelection;
        _mercuryTypeEdited = false;
    }

    private void RefreshMercuryTypeForSpecies(object? sender, EventArgs e)
    {
        if (FieldsLoaded && Entity is MercuryPKM pk && pk.Species != _mercuryTypeSpecies)
            LoadMercuryTypes(pk);
    }
}
