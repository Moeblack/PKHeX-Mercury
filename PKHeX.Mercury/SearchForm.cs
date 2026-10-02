using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

internal sealed record SearchHit(SlotRef Slot, string Text)
{
    public override string ToString() => Text;
}

/// <summary>查找物种/昵称并定位到箱子或队伍。</summary>
internal sealed class SearchForm : Form
{
    private readonly MercurySave _save;
    private readonly ComboBox _mode = Ui.Combo(140);
    private readonly TextBox _query = Ui.Text(240);
    private readonly ListBox _results = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _status = Ui.Label("");

    public SlotRef? SelectedSlot { get; private set; }

    public SearchForm(MercurySave save)
    {
        _save = save;
        Text = "查找宝可梦";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(520, 460);
        MinimizeBox = false;
        ShowInTaskbar = false;

        _mode.Items.Add(new ListEntry(0, "按物种"));
        _mode.Items.Add(new ListEntry(1, "按昵称"));
        _mode.SelectedIndex = 0;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };
        var go = Ui.Button("搜索", 80);
        go.Click += (_, _) => RunSearch();
        _query.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                RunSearch();
            }
        };
        top.Controls.Add(_mode);
        top.Controls.Add(_query);
        top.Controls.Add(go);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8),
        };
        var ok = Ui.Button("定位", 100);
        var cancel = Ui.Button("取消", 100);
        ok.Click += (_, _) => Accept();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        bottom.Controls.Add(ok);
        bottom.Controls.Add(cancel);

        _results.DoubleClick += (_, _) => Accept();
        _status.Dock = DockStyle.Bottom;
        _status.Height = 24;
        _status.ForeColor = Color.DimGray;

        Controls.Add(_results);
        Controls.Add(_status);
        Controls.Add(bottom);
        Controls.Add(top);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void RunSearch()
    {
        _results.Items.Clear();
        var query = _query.Text.Trim();
        if (query.Length == 0)
        {
            _status.Text = "请输入关键字。";
            return;
        }

        var byNickname = _mode.SelectedItem is ListEntry e && e.Id == 1;
        var numeric = int.TryParse(query, out var id);
        var count = 0;

        for (var box = 0; box < MercurySave.BoxCount; box++)
        {
            for (var slot = 0; slot < MercurySave.BoxCapacity; slot++)
            {
                if (Match(_save.GetBox(box, slot), query, numeric, id, byNickname))
                {
                    _results.Items.Add(new SearchHit(SlotRef.BoxSlot(box, slot), Describe(SlotRef.BoxSlot(box, slot), _save.GetBox(box, slot))));
                    count++;
                }
            }
        }

        for (var slot = 0; slot < (_save.PartyCount > 0 ? _save.PartyCount : 6); slot++)
        {
            var mon = _save.GetParty(slot);
            if (Match(mon, query, numeric, id, byNickname))
            {
                _results.Items.Add(new SearchHit(SlotRef.PartySlot(slot), Describe(SlotRef.PartySlot(slot), mon)));
                count++;
            }
        }

        _status.Text = $"找到 {count} 条结果。";
        if (_results.Items.Count > 0)
            _results.SelectedIndex = 0;
    }

    private static bool Match(MercuryPokemon mon, string query, bool numeric, int id, bool byNickname)
    {
        if (mon.IsEmpty)
            return false;

        if (byNickname)
        {
            var nick = UiText.Decode(mon.NicknameBytes);
            return nick.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        }

        if (numeric && mon.Species == id)
            return true;
        var name = UiText.SpeciesName(mon.Species);
        return name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string Describe(SlotRef slot, MercuryPokemon mon)
        => $"{slot.Describe()}  ·  {UiText.MonLabel(mon)}（物种 {mon.Species}）";

    private void Accept()
    {
        if (_results.SelectedItem is not SearchHit hit)
            return;
        SelectedSlot = hit.Slot;
        DialogResult = DialogResult.OK;
    }
}
