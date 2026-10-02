namespace PKHeX.Mercury;

/// <summary>ComboBox 用的“编号 + 文本”条目。</summary>
internal sealed class ListEntry(int id, string text)
{
    public int Id { get; } = id;
    public string Text { get; } = text;

    public override string ToString() => Text;
}

internal static class EntryList
{
    private static object? _dataRef;
    private static List<ListEntry>? _items;
    private static List<ListEntry>? _moves;

    private static void Ensure()
    {
        if (ReferenceEquals(_dataRef, AppState.Data))
            return;
        _dataRef = AppState.Data;
        _items = null;
        _moves = null;
    }

    public static List<ListEntry> Items()
    {
        Ensure();
        if (_items is not null)
            return _items;

        var list = new List<ListEntry> { new(0, "（无）") };
        try
        {
            foreach (var item in AppState.Data.Items)
                list.Add(new ListEntry(item.Id, $"{item.Id} {item.Name}"));
        }
        catch
        {
            // 无道具表时保持仅“无”。
        }
        return _items = list;
    }

    public static List<ListEntry> Moves()
    {
        Ensure();
        if (_moves is not null)
            return _moves;

        var list = new List<ListEntry> { new(0, "（空）") };
        try
        {
            foreach (var move in AppState.Data.Moves)
                list.Add(new ListEntry(move.Id, $"{move.Id} {move.Name}"));
        }
        catch
        {
            // 无招式表时保持仅“空”。
        }
        return _moves = list;
    }

    public static void SelectId(ComboBox combo, int id)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ListEntry e && e.Id == id)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    public static int SelectedId(ComboBox combo)
        => combo.SelectedItem is ListEntry e ? e.Id : 0;
}
