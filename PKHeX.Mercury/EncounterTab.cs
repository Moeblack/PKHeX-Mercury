using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>相遇页：OT、球、相遇等级/地点/游戏、病毒与标记。未知字段保持原样。</summary>
internal sealed class EncounterTab : MonTab
{
    private readonly TextBox _txtOt = Ui.Text(220);
    private readonly Label _lblOtNote = Ui.Label("");
    private readonly ComboBox _cmbOtGender = Ui.Combo(110);
    private readonly NumericUpDown _numBall = Ui.Num(0, 255);
    private readonly NumericUpDown _numMetLevel = Ui.Num(0, 100);
    private readonly NumericUpDown _numMetLocation = Ui.Num(0, 255);
    private readonly NumericUpDown _numMetGame = Ui.Num(0, 255);
    private readonly NumericUpDown _numPokerus = Ui.Num(0, 255);
    private readonly NumericUpDown _numMarkings = Ui.Num(0, 255);
    private readonly NumericUpDown _numLanguage = Ui.Num(0, 255);
    private readonly Label _note = Ui.Label("");

    public EncounterTab()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(8),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;
        void Row(string label, Control control, Control? extra = null)
        {
            table.Controls.Add(Ui.Label(label), 0, row);
            table.Controls.Add(control, 1, row);
            if (extra is not null)
                table.Controls.Add(extra, 2, row);
            row++;
        }

        _cmbOtGender.Items.Add(new ListEntry(0, "男性"));
        _cmbOtGender.Items.Add(new ListEntry(1, "女性"));

        Row("初训家 OT", _txtOt, _lblOtNote);
        Row("OT 性别", _cmbOtGender);
        Row("精灵球", _numBall);
        Row("相遇等级", _numMetLevel);
        Row("相遇地点", _numMetLocation);
        Row("游戏版本", _numMetGame);
        Row("病毒状态", _numPokerus);
        Row("标记", _numMarkings);
        Row("语言", _numLanguage);
        Row("说明", _note);

        _lblOtNote.ForeColor = Color.DimGray;
        _note.ForeColor = Color.DimGray;
        _note.Text = "本页只编辑 Core 确认的字段；未编辑的额外/未知字节会原样保留。";

        Controls.Add(table);

        _txtOt.TextChanged += (_, _) => CommitOt();
        _cmbOtGender.SelectedIndexChanged += (_, _) => CommitOtGender();
        _numBall.ValueChanged += (_, _) => Commit(() => Mon!.Ball = (byte)_numBall.Value);
        _numMetLevel.ValueChanged += (_, _) => Commit(() => Mon!.MetLevel = (byte)_numMetLevel.Value);
        _numMetLocation.ValueChanged += (_, _) => Commit(() => Mon!.MetLocation = (byte)_numMetLocation.Value);
        _numMetGame.ValueChanged += (_, _) => Commit(() => Mon!.MetGame = (byte)_numMetGame.Value);
        _numPokerus.ValueChanged += (_, _) => Commit(() => Mon!.Pokerus = (byte)_numPokerus.Value);
        _numMarkings.ValueChanged += (_, _) => Commit(() => Mon!.Markings = (byte)_numMarkings.Value);
        _numLanguage.ValueChanged += (_, _) => Commit(() => Mon!.Language = (byte)_numLanguage.Value);
    }

    protected override void Reload()
    {
        Loading = true;
        try
        {
            var mon = Mon;
            var enabled = mon is not null && !mon.IsEmpty;
            foreach (Control c in new Control[] { _txtOt, _cmbOtGender, _numBall, _numMetLevel, _numMetLocation, _numMetGame, _numPokerus, _numMarkings, _numLanguage })
                c.Enabled = enabled;

            if (!enabled)
            {
                _txtOt.Text = "";
                _lblOtNote.Text = "";
                return;
            }

            _txtOt.Text = UiText.Decode(mon!.OTNameBytes);
            UpdateOtNote();
            SelectOtGender(mon.OTGender);
            _numBall.Value = mon.Ball;
            _numMetLevel.Value = Math.Clamp((int)mon.MetLevel, 0, 100);
            _numMetLocation.Value = mon.MetLocation;
            _numMetGame.Value = mon.MetGame;
            _numPokerus.Value = mon.Pokerus;
            _numMarkings.Value = mon.Markings;
            _numLanguage.Value = mon.Language;
        }
        finally
        {
            Loading = false;
        }
    }

    private void UpdateOtNote()
    {
        var cap = Mon?.OTNameBytes?.Length ?? 0;
        if (cap <= 0)
        {
            _lblOtNote.Text = "该记录无 OT 名栏位";
            return;
        }
        var ok = AppState.Codec.CanEncode(_txtOt.Text, cap);
        _txtOt.ForeColor = ok ? SystemColors.WindowText : Color.Firebrick;
        _lblOtNote.Text = ok ? $"占 {cap} 字节" : $"超出 {cap} 字节，未写入";
    }

    private void CommitOt()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        var cap = Mon.OTNameBytes?.Length ?? 0;
        UpdateOtNote();
        if (cap <= 0 || !AppState.Codec.CanEncode(_txtOt.Text, cap))
            return;
        try
        {
            Mon.OTNameBytes = AppState.Codec.Encode(_txtOt.Text, cap);
            Mark();
        }
        catch
        {
            // 保持原值。
        }
    }

    private void CommitOtGender()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        Mon.OTGender = (byte)(_cmbOtGender.SelectedItem is ListEntry e ? e.Id : 0);
        Mark();
    }

    private void Commit(Action apply)
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        apply();
        Mark();
    }

    private void SelectOtGender(byte value)
    {
        for (var i = 0; i < _cmbOtGender.Items.Count; i++)
        {
            if (_cmbOtGender.Items[i] is ListEntry e && e.Id == value)
            {
                _cmbOtGender.SelectedIndex = i;
                return;
            }
        }
        _cmbOtGender.SelectedIndex = 0;
    }
}
