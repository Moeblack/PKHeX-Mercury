using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>个体/努力页：六项数值，显示顺序与数据顺序显式区分，支持 6V 与总和校验。</summary>
internal sealed class StatsTab : MonTab
{
    // 数据顺序为 HP,Atk,Def,Spe,SpA,SpD；界面按 HP/攻击/防御/特攻/特防/速度 展示。
    private static readonly int[] DisplayOrder = [0, 1, 2, 4, 5, 3];
    private static readonly string[] StatNames = ["HP", "攻击", "防御", "特攻", "特防", "速度"];

    private readonly NumericUpDown[] _ivs = new NumericUpDown[6];
    private readonly NumericUpDown[] _evs = new NumericUpDown[6];
    private readonly Label _total = Ui.Label("");
    private readonly Label _warn = Ui.Label("");
    private readonly Button _sixV = Ui.Button("全 31 (6V)");

    public StatsTab()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            Padding = new Padding(8),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

        table.Controls.Add(Ui.Label("能力项"), 0, 0);
        table.Controls.Add(Ui.Label("个体值 IV (0-31)"), 1, 0);
        table.Controls.Add(Ui.Label("努力值 EV (0-255)"), 2, 0);
        table.SetColumnSpan(table.GetControlFromPosition(2, 0)!, 2);

        for (var i = 0; i < 6; i++)
        {
            var idx = DisplayOrder[i];
            _ivs[i] = Ui.Num(0, 31, 110);
            _evs[i] = Ui.Num(0, 255, 110);
            _ivs[i].ValueChanged += (_, _) => Commit();
            _evs[i].ValueChanged += (_, _) => Commit();
            table.Controls.Add(Ui.Label(StatNames[i]), 0, i + 1);
            table.Controls.Add(_ivs[i], 1, i + 1);
            table.Controls.Add(_evs[i], 2, i + 1);
        }

        _sixV.Click += (_, _) =>
        {
            Loading = true;
            foreach (var iv in _ivs)
                iv.Value = 31;
            Loading = false;
            Commit();
        };

        _total.ForeColor = Color.DimGray;
        table.Controls.Add(_total, 1, 7);
        table.SetColumnSpan(_total, 3);
        table.Controls.Add(_sixV, 1, 8);
        _warn.ForeColor = Color.Firebrick;
        table.Controls.Add(_warn, 1, 9);
        table.SetColumnSpan(_warn, 3);

        Controls.Add(table);
    }

    protected override void Reload()
    {
        Loading = true;
        try
        {
            var mon = Mon;
            var enabled = mon is not null && !mon.IsEmpty;
            foreach (var c in _ivs)
                c.Enabled = enabled;
            foreach (var c in _evs)
                c.Enabled = enabled;
            _sixV.Enabled = enabled;

            if (!enabled)
            {
                _total.Text = "";
                _warn.Text = "";
                return;
            }

            var ivs = EnsureLength(mon!.IVs, 6);
            var evs = EnsureLength(mon.EVs, 6);
            for (var i = 0; i < 6; i++)
            {
                _ivs[i].Value = Math.Clamp((int)ivs[DisplayOrder[i]], 0, 31);
                _evs[i].Value = Math.Clamp((int)evs[DisplayOrder[i]], 0, 255);
            }
            UpdateTotal();
            _warn.Text = "";
        }
        finally
        {
            Loading = false;
        }
    }

    private void Commit()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        var ivs = new byte[6];
        var evs = new byte[6];
        for (var i = 0; i < 6; i++)
        {
            ivs[DisplayOrder[i]] = (byte)Math.Clamp((int)_ivs[i].Value, 0, 31);
            evs[DisplayOrder[i]] = (byte)Math.Clamp((int)_evs[i].Value, 0, 255);
        }
        Mon.IVs = ivs;
        Mon.EVs = evs;
        UpdateTotal();
        Mark();
    }

    private void UpdateTotal()
    {
        var total = 0;
        foreach (var ev in _evs)
            total += (int)ev.Value;
        _total.Text = $"努力值总和：{total} / 510";
        _warn.Text = total > 510 ? "警告：努力值总和超过 510，保存后原版可能不生效。" : "";
    }
}
