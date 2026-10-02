using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>选择物种（用于“新建”宝可梦）。无 ROM 物种表时退回按内部编号输入。</summary>
internal sealed class SpeciesPicker : Form
{
    private readonly ComboBox _combo = Ui.Combo(300);
    private readonly NumericUpDown _num = Ui.Num(0, ushort.MaxValue, 120);
    private bool _sync;

    public int SelectedSpecies { get; private set; }

    public SpeciesPicker(int initial)
    {
        Text = "选择物种";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 150);

        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var sp in AppState.Data.Species)
        {
            if (!sp.HasData)
                continue;
            _combo.Items.Add(new ListEntry(sp.Id, $"{sp.Id} {sp.Name}"));
        }

        table.Controls.Add(Ui.Label("物种"), 0, 0);
        table.Controls.Add(_combo, 1, 0);
        table.Controls.Add(Ui.Label("内部编号"), 0, 1);
        table.Controls.Add(_num, 1, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        var ok = Ui.Button("确定", 100);
        var cancel = Ui.Button("取消", 100);
        ok.Click += (_, _) =>
        {
            SelectedSpecies = _combo.SelectedItem is ListEntry e ? e.Id : (int)_num.Value;
            DialogResult = DialogResult.OK;
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(buttons);
        Controls.Add(table);
        AcceptButton = ok;
        CancelButton = cancel;

        _num.Value = Math.Clamp(initial, 0, ushort.MaxValue);
        if (_combo.Items.Count > 0)
        {
            for (var i = 0; i < _combo.Items.Count; i++)
            {
                if (_combo.Items[i] is ListEntry e && e.Id == initial)
                {
                    _combo.SelectedIndex = i;
                    break;
                }
            }
            if (_combo.SelectedIndex < 0)
                _combo.SelectedIndex = 0;
        }

        _combo.SelectedIndexChanged += (_, _) =>
        {
            if (_sync || _combo.SelectedItem is not ListEntry e)
                return;
            _sync = true;
            _num.Value = Math.Clamp(e.Id, 0, ushort.MaxValue);
            _sync = false;
        };
        _num.ValueChanged += (_, _) =>
        {
            if (_sync)
                return;
            for (var i = 0; i < _combo.Items.Count; i++)
            {
                if (_combo.Items[i] is ListEntry e && e.Id == (int)_num.Value)
                {
                    _sync = true;
                    _combo.SelectedIndex = i;
                    _sync = false;
                    return;
                }
            }
        };
    }
}
