using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>右侧详情面板：五个页签 + 显式的“应用/放弃”按钮。</summary>
internal sealed class DetailPanel : UserControl
{
    public event Action? Modified;
    public event Action? ApplyRequested;
    public event Action? DiscardRequested;

    private readonly Label _header = Ui.Label("未选择任何格子");
    private readonly Button _apply = Ui.Button("应用到当前格", 130);
    private readonly Button _discard = Ui.Button("放弃当前编辑", 130);
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly BasicTab _basic = new();
    private readonly MovesTab _moves = new();
    private readonly StatsTab _stats = new();
    private readonly EncounterTab _encounter = new();
    private readonly RawTab _raw = new();
    private bool _hasDraft;

    public DetailPanel()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _header.Font = new Font(Font, FontStyle.Bold);
        _header.Dock = DockStyle.Fill;

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _apply.Enabled = false;
        _discard.Enabled = false;
        _apply.Click += (_, _) => ApplyRequested?.Invoke();
        _discard.Click += (_, _) => DiscardRequested?.Invoke();
        actions.Controls.Add(_apply);
        actions.Controls.Add(_discard);

        _tabs.TabPages.Add(MakePage("基础", _basic));
        _tabs.TabPages.Add(MakePage("招式", _moves));
        _tabs.TabPages.Add(MakePage("个体/努力", _stats));
        _tabs.TabPages.Add(MakePage("相遇", _encounter));
        _tabs.TabPages.Add(MakePage("原始 58B", _raw));

        root.Controls.Add(_header, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(_tabs, 0, 2);
        Controls.Add(root);

        foreach (var tab in new MonTab[] { _basic, _moves, _stats, _encounter, _raw })
            tab.Modified += () => Modified?.Invoke();
    }

    private static TabPage MakePage(string title, Control content)
    {
        var page = new TabPage(title) { UseVisualStyleBackColor = true };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    public void Bind(MercuryPokemon? draft, SlotRef slot)
    {
        _hasDraft = draft is not null && !draft.IsEmpty;
        _header.Text = _hasDraft ? $"当前格：{slot.Describe()}" : $"当前格：{slot.Describe()}（空）";
        _apply.Enabled = _hasDraft;
        _discard.Enabled = _hasDraft;
        foreach (var tab in new MonTab[] { _basic, _moves, _stats, _encounter, _raw })
            tab.Bind(_hasDraft ? draft : null);
    }

    public void Clear()
    {
        _hasDraft = false;
        _apply.Enabled = false;
        _discard.Enabled = false;
        _header.Text = "未选择任何格子";
        foreach (var tab in new MonTab[] { _basic, _moves, _stats, _encounter, _raw })
            tab.Bind(null);
    }
}
