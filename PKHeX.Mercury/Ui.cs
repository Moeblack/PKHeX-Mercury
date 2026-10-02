using System.Globalization;
using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>对 UI 定位到某一格（箱子或队伍）的引用。</summary>
internal readonly record struct SlotRef(bool IsParty, int Box, int Slot)
{
    public static SlotRef BoxSlot(int box, int slot) => new(false, box, slot);
    public static SlotRef PartySlot(int slot) => new(true, 0, slot);

    public string Describe() => IsParty ? $"队伍第 {Slot + 1} 位" : $"箱子 {Box + 1} · 第 {Slot + 1} 格";

    public override string ToString() => Describe();
}

/// <summary>界面文案与常用格式化。</summary>
internal static class UiText
{
    public static readonly string[] Natures =
    [
        "勤奋", "怕寂寞", "勇敢", "固执", "调皮",
        "大胆", "坦率", "悠闲", "淘气", "乐天",
        "胆小", "急躁", "认真", "爽朗", "天真",
        "内敛", "慢吞吞", "冷静", "害羞", "马虎",
        "温和", "温顺", "温吞", "慎重", "浮躁",
    ];

    public static readonly string[] Genders = ["雄性", "雌性", "无性别"];

    public static string NatureName(int nature)
        => nature >= 0 && nature < Natures.Length ? $"{nature} {Natures[nature]}" : $"未知({nature})";

    public static string GenderName(int g)
        => g >= 0 && g < Genders.Length ? Genders[g] : $"未知({g})";

    public static string SpeciesName(int id)
    {
        try
        {
            var n = AppState.Data.SpeciesName(id);
            return string.IsNullOrWhiteSpace(n) ? $"#{id}" : n;
        }
        catch
        {
            return $"#{id}";
        }
    }

    public static string MoveName(int id)
    {
        if (id == 0)
            return "（空）";
        try
        {
            var n = AppState.Data.MoveName(id);
            return string.IsNullOrWhiteSpace(n) ? $"招式#{id}" : $"{id} {n}";
        }
        catch
        {
            return $"招式#{id}";
        }
    }

    public static string ItemName(int id)
    {
        if (id == 0)
            return "（无）";
        try
        {
            var n = AppState.Data.ItemName(id);
            return string.IsNullOrWhiteSpace(n) ? $"道具#{id}" : $"{id} {n}";
        }
        catch
        {
            return $"道具#{id}";
        }
    }

    public static string Decode(byte[]? bytes, string fallback = "")
    {
        if (bytes is null || bytes.Length == 0)
            return fallback;
        try
        {
            var s = AppState.Codec.Decode(bytes);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }
        catch
        {
            return fallback;
        }
    }

    public static string MonLabel(MercuryPokemon? mon)
    {
        if (mon is null || mon.IsEmpty)
            return string.Empty;
        var nick = Decode(mon.NicknameBytes);
        return string.IsNullOrWhiteSpace(nick) ? SpeciesName(mon.Species) : nick;
    }

    public static string Hex(uint value) => value.ToString("X8", CultureInfo.InvariantCulture);
    public static string Hex(byte value) => value.ToString("X2", CultureInfo.InvariantCulture);
}

/// <summary>控件构造与数值处理小工具。</summary>
internal static class Ui
{
    public static Label Label(string text, int width = 0)
        => new()
        {
            Text = text,
            AutoSize = width <= 0,
            Width = width <= 0 ? 0 : width,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3, 4, 3, 0),
        };

    public static NumericUpDown Num(decimal min, decimal max, int width = 96)
        => new()
        {
            Minimum = min,
            Maximum = max,
            Width = width,
            TextAlign = HorizontalAlignment.Right,
            Margin = new Padding(3, 2, 3, 2),
        };

    public static CheckBox Check(string text, int width = 0)
        => new()
        {
            Text = text,
            AutoSize = width <= 0,
            Width = width <= 0 ? 0 : width,
            Margin = new Padding(3, 4, 3, 2),
        };

    public static ComboBox Combo(int width = 160)
        => new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = width,
            Margin = new Padding(3, 2, 3, 2),
        };

    public static TextBox Text(int width = 160)
        => new()
        {
            Width = width,
            Margin = new Padding(3, 2, 3, 2),
        };

    public static Button Button(string text, int width = 96)
        => new()
        {
            Text = text,
            Width = width,
            AutoSize = false,
            Margin = new Padding(3, 2, 3, 2),
        };

    public static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(Label(label), 0, row);
        table.Controls.Add(control, 1, row);
    }

    public static void AddRow(TableLayoutPanel table, int row, string label, Control control, Control extra)
    {
        table.Controls.Add(Label(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
        extra.Margin = new Padding(3, 2, 3, 2);
        table.Controls.Add(extra, 2, row);
    }

    public static int ParseHex(string? text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;
        var t = text.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            t = t[2..];
        return int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    public static uint ParseHexU(string? text, uint fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;
        var t = text.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            t = t[2..];
        return uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    public static string HexDump(byte[] data, int perLine = 16, int maxLines = 200)
    {
        var sb = new System.Text.StringBuilder();
        var lines = Math.Min(maxLines, (data.Length + perLine - 1) / perLine);
        for (var line = 0; line < lines; line++)
        {
            var offset = line * perLine;
            sb.Append(offset.ToString("X4", CultureInfo.InvariantCulture)).Append("  ");
            for (var i = 0; i < perLine; i++)
            {
                var idx = offset + i;
                sb.Append(idx < data.Length ? data[idx].ToString("X2", CultureInfo.InvariantCulture) : "  ");
                sb.Append(i == perLine / 2 - 1 ? "  " : ' ');
            }
            sb.Append(' ');
            for (var i = 0; i < perLine; i++)
            {
                var idx = offset + i;
                if (idx >= data.Length)
                    break;
                var b = data[idx];
                sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }
            sb.AppendLine();
        }
        if (lines * perLine < data.Length)
            sb.AppendLine($"…（共 {data.Length} 字节，仅显示前 {lines * perLine} 字节）");
        return sb.ToString();
    }
}
