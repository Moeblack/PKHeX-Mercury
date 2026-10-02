using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>招式页：四个槽（含空槽）+ PPUps + 升级学习表快捷选择。</summary>
internal sealed class MovesTab : MonTab
{
    private readonly ComboBox[] _moves = new ComboBox[4];
    private readonly NumericUpDown[] _ppUps = new NumericUpDown[4];
    private readonly ComboBox _target = Ui.Combo(120);
    private readonly ListBox _learn = new()
    {
        Width = 260,
        Height = 200,
        IntegralHeight = false,
        Margin = new Padding(3, 2, 3, 2),
    };
    private readonly Button _btnFill = Ui.Button("填入所选槽", 120);
    private readonly Label _note = Ui.Label("");

    public MovesTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(8),
            AutoScroll = true,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

        var left = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var i = 0; i < 4; i++)
        {
            _moves[i] = Ui.Combo(240);
            _ppUps[i] = Ui.Num(0, 3, 60);
            var slot = i;
            _moves[i].SelectedIndexChanged += (_, _) => CommitMove(slot);
            _ppUps[i].ValueChanged += (_, _) => CommitPpUp(slot);

            var line = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
            line.Controls.Add(_moves[i]);
            line.Controls.Add(Ui.Label("PPUps", 50));
            line.Controls.Add(_ppUps[i]);
            left.Controls.Add(Ui.Label($"招式 {i + 1}"), 0, i);
            left.Controls.Add(line, 1, i);
        }

        _target.Items.Add(new ListEntry(0, "招式 1"));
        _target.Items.Add(new ListEntry(1, "招式 2"));
        _target.Items.Add(new ListEntry(2, "招式 3"));
        _target.Items.Add(new ListEntry(3, "招式 4"));
        _target.SelectedIndex = 0;
        _btnFill.Click += (_, _) => FillFromLearn();

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1 };
        right.Controls.Add(Ui.Label("升级学习表（ROM）"), 0, 0);
        right.Controls.Add(_learn, 0, 1);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        actions.Controls.Add(Ui.Label("目标槽", 56));
        actions.Controls.Add(_target);
        actions.Controls.Add(_btnFill);
        right.Controls.Add(actions, 0, 2);
        right.Controls.Add(_note, 0, 3);
        _note.ForeColor = Color.DimGray;

        root.Controls.Add(left, 0, 0);
        root.Controls.Add(right, 1, 0);
        Controls.Add(root);
    }

    protected override void Reload()
    {
        Loading = true;
        try
        {
            var mon = Mon;
            var enabled = mon is not null && !mon.IsEmpty;
            foreach (var combo in _moves)
            {
                combo.Items.Clear();
                foreach (var entry in EntryList.Moves())
                    combo.Items.Add(entry);
                combo.Enabled = enabled;
            }
            foreach (var pp in _ppUps)
                pp.Enabled = enabled;

            _learn.Items.Clear();
            _note.Text = "";

            if (!enabled)
            {
                _btnFill.Enabled = false;
                return;
            }

            var moves = mon!.Moves;
            var ppups = mon.PPUps;
            for (var i = 0; i < 4; i++)
            {
                var id = moves is { Length: > 0 } && i < moves.Length ? moves[i] : (ushort)0;
                EntryList.SelectId(_moves[i], id);
                _ppUps[i].Value = ppups is { Length: > 0 } && i < ppups.Length ? Math.Clamp((int)ppups[i], 0, 3) : 0;
            }

            var hasLearn = Species is { HasData: true } && Species.LevelUpMoves.Count > 0;
            _btnFill.Enabled = hasLearn;
            _learn.Enabled = hasLearn;
            if (hasLearn)
            {
                foreach (var lm in Species!.LevelUpMoves.OrderBy(x => x.Level).ThenBy(x => x.Move))
                    _learn.Items.Add(new ListEntry(lm.Move, $"Lv.{lm.Level}  {UiText.MoveName(lm.Move)}"));
            }
            else
            {
                _note.Text = "无 ROM 学习表，无法快捷选择；仍可直接从招式列表指定。";
            }

            if (AppState.Data.Moves.Count == 0)
                _note.Text = "无 ROM 招式表：招式槽只能保持空（数字 ID 模式）。" + _note.Text;
        }
        finally
        {
            Loading = false;
        }
    }

    private void CommitMove(int index)
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        var moves = EnsureLength16(Mon.Moves, 4);
        moves[index] = (ushort)Math.Clamp(EntryList.SelectedId(_moves[index]), 0, ushort.MaxValue);
        Mon.Moves = moves;
        Mark();
    }

    private void CommitPpUp(int index)
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        var ppups = EnsureLength(Mon.PPUps, 4);
        ppups[index] = (byte)Math.Clamp((int)_ppUps[index].Value, 0, 3);
        Mon.PPUps = ppups;
        Mark();
    }

    private void FillFromLearn()
    {
        if (Mon is null || Mon.IsEmpty || _learn.SelectedItem is not ListEntry entry)
            return;
        var slot = _target.SelectedItem is ListEntry t ? t.Id : 0;
        slot = Math.Clamp(slot, 0, 3);
        EntryList.SelectId(_moves[slot], entry.Id);
        CommitMove(slot);
    }

    private static ushort[] EnsureLength16(ushort[]? source, int length)
    {
        var result = new ushort[length];
        if (source is not null)
            Array.Copy(source, result, Math.Min(source.Length, length));
        return result;
    }
}
