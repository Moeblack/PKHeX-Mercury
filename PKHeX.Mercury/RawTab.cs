using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>只读原始 58 字节（盒形态）视图；不提供直接字节编辑，避免破坏未知字段。</summary>
internal sealed class RawTab : MonTab
{
    private readonly TextBox _hex = new()
    {
        Multiline = true,
        ReadOnly = true,
        WordWrap = false,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        Font = new Font(FontFamily.GenericMonospace, 9f),
    };
    private readonly Label _note = Ui.Label("");

    public RawTab()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        _note.ForeColor = Color.DimGray;
        root.Controls.Add(_hex, 0, 0);
        root.Controls.Add(_note, 0, 1);
        Controls.Add(root);
    }

    protected override void Reload()
    {
        var mon = Mon;
        if (mon is null || mon.IsEmpty)
        {
            _hex.Text = "";
            _note.Text = "（未选择或空格）";
            return;
        }

        try
        {
            var bytes = mon.ToBoxBytes();
            _hex.Text = Ui.HexDump(bytes);
            _note.Text = $"原始盒形态数据：{bytes.Length} 字节（只读）。队伍形态为 {TryLength(mon)} 字节。";
        }
        catch (Exception ex)
        {
            _hex.Text = "";
            _note.Text = "无法读取原始字节：" + ex.Message;
        }
    }

    private static string TryLength(MercuryPokemon mon)
    {
        try
        {
            return mon.ToPartyBytes().Length.ToString();
        }
        catch
        {
            return "?";
        }
    }
}
