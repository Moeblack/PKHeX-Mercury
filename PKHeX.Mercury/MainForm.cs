using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>主窗口：箱子/队伍总览 + 详情编辑 + 安全读写。</summary>
internal sealed class MainForm : Form
{
    private MercurySave? _save;
    private string? _savePath;
    private MercuryPokemon? _draft;
    private bool _draftDirty;
    private SlotRef _current;
    private bool _slotSelected;
    private int _currentBox;
    private bool _loadingBox;

    private int _editVersion;
    private int _exportedVersion;
    private IReadOnlyList<string> _warnings = [];

    private MercuryPokemon? _clipboard;
    private bool _clipboardCut;
    private SlotRef _cutSource;
    private SlotRef _contextSlot;

    private readonly SlotControl[] _boxSlots = new SlotControl[MercurySave.BoxCapacity];
    private readonly SlotControl[] _partySlots = new SlotControl[6];
    private readonly ListBox _boxList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _boxOccupancy = Ui.Label("");
    private readonly Label _boxTitle = Ui.Label("箱子 1");
    private readonly DetailPanel _detail = new() { Dock = DockStyle.Fill };
    private readonly ContextMenuStrip _slotMenu = new();

    private readonly ToolStripStatusLabel _stFile = new("");
    private readonly ToolStripStatusLabel _stBox = new("");
    private readonly ToolStripStatusLabel _stTrainer = new("");
    private readonly ToolStripStatusLabel _stSource = new("");
    private readonly ToolStripStatusLabel _stDirty = new("");
    private readonly ToolStripStatusLabel _stWarn = new("");

    private readonly List<ToolStripMenuItem> _editItems = [];
    private MenuStrip? _menu;
    private bool _reloadAfterDataChange;

    public MainForm()
    {
        Text = "PKHeX Mercury / 水银版";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1280, 850);
        MinimumSize = new Size(1000, 680);
        AllowDrop = true;
        StartPosition = FormStartPosition.CenterScreen;

        BuildCreateMenuItem();
        BuildLayout();
        BuildSlotCreateMenuItem();

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        FormClosing += OnFormClosing;

        _detail.Modified += () =>
        {
            _draftDirty = true;
            UpdateStatus();
        };
        _detail.ApplyRequested += ApplyDraft;
        _detail.DiscardRequested += DiscardDraft;

        UpdateEditingEnabled();
        UpdateStatus();
    }

    // ---------------------------------------------------------------- 菜单

    private static ToolStripMenuItem CreateMenuItem(string text, Action? handler = null, Keys? shortcut = null)
    {
        var item = new ToolStripMenuItem(text);
        if (shortcut is { } k)
            item.ShortcutKeys = k;
        if (handler is not null)
            item.Click += (_, _) => handler();
        return item;
    }

    private void BuildCreateMenuItem()
    {
        var menu = new MenuStrip();

        var file = new ToolStripMenuItem("文件(&F)");
        file.DropDownItems.Add(CreateMenuItem("打开存档…", OpenSaveDialog, Keys.Control | Keys.O));
        file.DropDownItems.Add(CreateMenuItem("另存为…", () => SaveAs(), Keys.Control | Keys.S));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(CreateMenuItem("关闭存档", CloseSave));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(CreateMenuItem("退出", Close));

        var edit = new ToolStripMenuItem("编辑(&E)");
        var applyItem = CreateMenuItem("应用到当前格", ApplyDraft);
        var discardItem = CreateMenuItem("放弃当前编辑", DiscardDraft);
        var deleteItem = CreateMenuItem("删除当前格…", () => DeleteSlot(_current, true));
        var copyItem = CreateMenuItem("复制当前格", () => CopySlot(_current));
        var pasteItem = CreateMenuItem("粘贴到当前格", () => PasteSlot(_current));
        var findItem = CreateMenuItem("查找…", OpenSearch, Keys.Control | Keys.F);
        edit.DropDownItems.Add(applyItem);
        edit.DropDownItems.Add(discardItem);
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(copyItem);
        edit.DropDownItems.Add(pasteItem);
        edit.DropDownItems.Add(deleteItem);
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(findItem);
        _editItems.AddRange([applyItem, discardItem, deleteItem, copyItem, pasteItem, findItem]);

        var rom = new ToolStripMenuItem("ROM 数据(&R)");
        var setupItem = CreateMenuItem("资料设置…", () => OpenDataSetup());
        rom.DropDownItems.Add(setupItem);

        var charmap = new ToolStripMenuItem("名称字表(&L)");
        var importCharmap = CreateMenuItem("导入字表 (json / game_data.js)…", () => OpenDataSetup(DataSetupAction.ImportCharmap));
        var downloadCharmap = CreateMenuItem("下载公开 HOME 字表（联网）…", () => OpenDataSetup(DataSetupAction.DownloadCharmap));
        charmap.DropDownItems.Add(importCharmap);
        charmap.DropDownItems.Add(downloadCharmap);

        var trainer = new ToolStripMenuItem("训练师(&T)");
        var trainerItem = CreateMenuItem("编辑训练师资料…", OpenTrainer);
        var movePartyItem = CreateMenuItem("查看队伍占用", () => _boxList.Focus());
        trainer.DropDownItems.Add(trainerItem);
        trainer.DropDownItems.Add(movePartyItem);
        _editItems.Add(trainerItem);

        var help = new ToolStripMenuItem("帮助(&H)");
        help.DropDownItems.Add(CreateMenuItem("使用说明", ShowHelp));
        help.DropDownItems.Add(CreateMenuItem("关于", () => new AboutForm().ShowDialog(this)));

        menu.Items.AddRange([file, edit, rom, charmap, trainer, help]);
        MainMenuStrip = menu;
        _menu = menu;
    }

    private enum DataSetupAction
    {
        None,
        ImportCharmap,
        DownloadCharmap,
    }

    // ---------------------------------------------------------------- 布局

    private void BuildLayout()
    {
        var status = new StatusStrip();
        _stFile.Spring = false;
        _stFile.Text = "未打开存档";
        _stBox.Text = "";
        _stSource.Text = "";
        _stDirty.Text = "";
        _stWarn.ForeColor = Color.Firebrick;
        status.Items.AddRange([_stFile, new ToolStripStatusLabel(" | "), _stBox, new ToolStripStatusLabel(" | "), _stTrainer, new ToolStripStatusLabel(" | "), _stSource, new ToolStripStatusLabel(" | "), _stDirty, _stWarn]);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 560));

        root.Controls.Add(BuildLeftPanel(), 0, 0);
        root.Controls.Add(BuildCenterPanel(), 1, 0);
        root.Controls.Add(_detail, 2, 0);

        Controls.Add(root);
        Controls.Add(status);
        if (_menu is not null)
            Controls.Add(_menu);
    }

    private Control BuildLeftPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(6) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.Controls.Add(Ui.Label("箱子（25 个）"), 0, 0);
        panel.Controls.Add(_boxList, 0, 1);
        _boxOccupancy.Dock = DockStyle.Fill;
        _boxOccupancy.ForeColor = Color.DimGray;
        panel.Controls.Add(_boxOccupancy, 0, 2);
        _boxList.SelectedIndexChanged += OnBoxListChanged;
        return panel;
    }

    private Control BuildCenterPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(6) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));

        _boxTitle.Dock = DockStyle.Fill;
        _boxTitle.Font = new Font(Font, FontStyle.Bold);
        panel.Controls.Add(_boxTitle, 0, 0);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 5 };
        for (var c = 0; c < 6; c++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
        for (var r = 0; r < 5; r++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        for (var i = 0; i < MercurySave.BoxCapacity; i++)
        {
            var slot = new SlotControl { Reference = SlotRef.BoxSlot(0, i) };
            slot.MouseDown += SlotMouseDown;
            slot.MouseDoubleClick += (_, _) => ApplyDraft();
            _boxSlots[i] = slot;
            grid.Controls.Add(slot, i % 6, i / 6);
        }
        panel.Controls.Add(grid, 0, 1);

        panel.Controls.Add(Ui.Label("队伍（最多 6 只，删除后会压紧）"), 0, 2);

        var party = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1 };
        for (var c = 0; c < 6; c++)
            party.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
        for (var i = 0; i < 6; i++)
        {
            var slot = new SlotControl { Reference = SlotRef.PartySlot(i), IsParty = true };
            slot.MouseDown += SlotMouseDown;
            slot.MouseDoubleClick += (_, _) => ApplyDraft();
            _partySlots[i] = slot;
            party.Controls.Add(slot, i, 0);
        }
        panel.Controls.Add(party, 0, 3);
        return panel;
    }

    // ---------------------------------------------------------------- 槽位菜单

    private void BuildSlotCreateMenuItem()
    {
        _slotMenu.Items.Add(CreateMenuItem("复制", () => CopySlot(_contextSlot)));
        _slotMenu.Items.Add(CreateMenuItem("剪切（移动/交换）", () => CutSlot(_contextSlot)));
        _slotMenu.Items.Add(CreateMenuItem("粘贴", () => PasteSlot(_contextSlot)));
        _slotMenu.Items.Add(new ToolStripSeparator());
        _slotMenu.Items.Add(CreateMenuItem("克隆到空格…", () => CloneSlot(_contextSlot)));
        _slotMenu.Items.Add(CreateMenuItem("新建宝可梦…", () => NewSlot(_contextSlot)));
        _slotMenu.Items.Add(CreateMenuItem("删除（需确认）…", () => DeleteSlot(_contextSlot, true)));
        _slotMenu.Items.Add(new ToolStripSeparator());
        _slotMenu.Items.Add(CreateMenuItem("导入 .m3box…", () => ImportM3Box(_contextSlot)));
        _slotMenu.Items.Add(CreateMenuItem("导出 .m3box…", () => ExportM3Box(_contextSlot)));
        _slotMenu.Opening += (_, e) => e.Cancel = _save is null;
    }

    private void SlotMouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not SlotControl slot)
            return;

        if (e.Button == MouseButtons.Left)
        {
            SelectSlot(slot.Reference);
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (!SelectSlot(slot.Reference))
                return;
            _contextSlot = slot.Reference;
            _slotMenu.Show(Cursor.Position);
        }
    }

    // ---------------------------------------------------------------- 存档读写

    private void OpenSaveDialog()
    {
        if (!GuardBeforeDestructive())
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "打开水银存档",
            Filter = "水银存档 (*.srm;*.sav)|*.srm;*.sav|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        LoadSave(dialog.FileName);
    }

    private void LoadSave(string path)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "读取存档失败：" + ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        MercurySave save;
        try
        {
            save = MercurySave.Load(bytes);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "该文件不是有效的水银存档，或结构校验未通过：\r\n\r\n" + ex.Message +
                "\r\n\r\n当前已打开的存档不会被替换。",
                "格式错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _save = save;
        _savePath = path;
        _draft = null;
        _draftDirty = false;
        _slotSelected = false;
        _clipboard = null;
        _clipboardCut = false;
        _editVersion = 0;
        _exportedVersion = 0;
        _warnings = save.Warnings;
        _currentBox = 0;

        _detail.Clear();
        RefreshAll();
        UpdateEditingEnabled();
        ShowWarnings();
    }

    private bool SaveAs()
    {
        if (_save is null)
            return false;

        using var dialog = new SaveFileDialog
        {
            Title = "另存为（默认不会覆盖来源）",
            Filter = "水银存档 (*.srm;*.sav)|*.srm;*.sav|所有文件 (*.*)|*.*",
            FileName = DefaultSaveName(),
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return false;

        var target = dialog.FileName;
        try
        {
            if (_savePath is not null && PathsEqual(target, _savePath))
            {
                var confirm = MessageBox.Show(this,
                    "你选择覆盖当前的来源存档：\r\n" + _savePath +
                    "\r\n\r\n程序会先备份为同名 .bak，再写入。是否继续？",
                    "确认覆盖来源", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                    return false;

                var backup = _savePath + ".bak";
                File.Copy(_savePath, backup, true);
            }

            var bytes = _save.Export();
            File.WriteAllBytes(target, bytes);
            _savePath = target;
            _exportedVersion = _editVersion;
            UpdateStatus();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：" + ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private string DefaultSaveName()
    {
        if (string.IsNullOrEmpty(_savePath))
            return "mercury_mercury.srm";
        var stem = Path.GetFileNameWithoutExtension(_savePath);
        var ext = Path.GetExtension(_savePath);
        if (string.IsNullOrEmpty(ext))
            ext = ".srm";
        return stem + "_mercury" + ext;
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void CloseSave()
    {
        if (_save is null)
            return;
        if (!GuardBeforeDestructive())
            return;

        _save = null;
        _savePath = null;
        _draft = null;
        _draftDirty = false;
        _slotSelected = false;
        _warnings = [];
        _detail.Clear();
        RefreshAll();
        UpdateEditingEnabled();
    }

    // ---------------------------------------------------------------- 草稿与守卫

    private bool ApplyOrDiscardDraft()
    {
        if (!_draftDirty)
            return true;

        var result = MessageBox.Show(this,
            "当前格的编辑尚未应用。\r\n\r\n是：应用到当前格\r\n否：放弃修改\r\n取消：返回",
            "未应用的编辑", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        switch (result)
        {
            case DialogResult.Yes:
                ApplyDraft();
                return !_draftDirty;
            case DialogResult.No:
                DiscardDraft();
                return true;
            default:
                return false;
        }
    }

    private bool GuardBeforeDestructive()
    {
        if (!ApplyOrDiscardDraft())
            return false;

        if (_save is not null && _editVersion != _exportedVersion)
        {
            var result = MessageBox.Show(this,
                "已应用的修改尚未导出到文件。\r\n\r\n是：另存为…\r\n否：放弃这些修改\r\n取消：返回",
                "未导出的修改", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            switch (result)
            {
                case DialogResult.Yes:
                    return SaveAs();
                case DialogResult.No:
                    return true;
                default:
                    return false;
            }
        }
        return true;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!GuardBeforeDestructive())
            e.Cancel = true;
    }

    private MercuryPokemon ReadSlot(SlotRef slot)
    {
        if (_save is null)
            return MonOps.EmptyBox();
        return slot.IsParty ? _save.GetParty(slot.Slot) : _save.GetBox(slot.Box, slot.Slot);
    }

    private void WriteSlot(SlotRef slot, MercuryPokemon mon)
    {
        if (_save is null)
            return;
        if (slot.IsParty)
        {
            var existing = _save.GetParty(slot.Slot);
            _save.SetParty(slot.Slot, MonOps.PreparePartyWrite(mon, existing, _save.StatMode));
        }
        else
        {
            _save.SetBox(slot.Box, slot.Slot, MonOps.ToBoxForm(mon));
        }
        MarkEdited();
    }

    private void MarkEdited()
    {
        _editVersion++;
        UpdateStatus();
    }

    private bool SelectSlot(SlotRef slot)
    {
        if (_save is null)
            return false;
        if (_slotSelected && _current == slot)
            return true;

        if (!ApplyOrDiscardDraft())
            return false;

        _current = slot;
        _slotSelected = true;
        _draft = ReadSlot(slot).Clone();
        _draftDirty = false;
        _detail.Bind(_draft, slot);
        RefreshSelection();
        UpdateStatus();
        return true;
    }

    private void LoadDraftFromSave()
    {
        if (!_slotSelected || _save is null)
            return;
        _draft = ReadSlot(_current).Clone();
        _draftDirty = false;
        _detail.Bind(_draft, _current);
        UpdateStatus();
    }

    private void ApplyDraft()
    {
        if (_save is null || !_slotSelected || _draft is null || _draft.IsEmpty)
        {
            if (_save is not null && _slotSelected && (_draft?.IsEmpty ?? true))
                MessageBox.Show(this, "空格没有可应用的内容；请先用“新建”或“粘贴”放入宝可梦。", "无内容", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            WriteSlot(_current, _draft);
            _draftDirty = false;
            RefreshBoxVisual(_current);
            RefreshBoxListText();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "应用失败：" + ex.Message, "应用失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DiscardDraft()
    {
        if (!_slotSelected)
            return;
        LoadDraftFromSave();
    }

    // ---------------------------------------------------------------- 槽位操作

    private void CopySlot(SlotRef slot)
    {
        if (_save is null || !IsValid(slot))
            return;
        _clipboard = ReadSlot(slot).Clone();
        _clipboardCut = false;
        _cutSource = default;
        UpdateStatus();
    }

    private void CutSlot(SlotRef slot)
    {
        if (_save is null || !IsValid(slot))
            return;
        _clipboard = ReadSlot(slot).Clone();
        _clipboardCut = true;
        _cutSource = slot;
        UpdateStatus();
    }

    private void PasteSlot(SlotRef target)
    {
        if (_save is null || !IsValid(target) || _clipboard is null)
            return;
        if (_clipboard.IsEmpty)
        {
            MessageBox.Show(this, "剪贴板为空。", "粘贴", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var cutSource = _cutSource;
            if (_clipboardCut)
            {
                if (cutSource == target)
                    return;

                var existing = ReadSlot(target);
                WriteSlot(target, _clipboard);

                if (existing.IsEmpty)
                {
                    // 移动：源格清空。
                    MonOps.ClearSlot(_save, cutSource);
                }
                else
                {
                    // 交换：源格放入目标原有内容。
                    WriteSlot(cutSource, existing);
                }

                MarkEdited();
                _clipboardCut = false;
                _cutSource = default;
                RefreshBoxVisual(cutSource);
                RefreshParty();
            }
            else
            {
                var existing = ReadSlot(target);
                if (!existing.IsEmpty)
                {
                    var confirm = MessageBox.Show(this, "目标格已有宝可梦，是否覆盖？", "覆盖确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes)
                        return;
                }
                WriteSlot(target, _clipboard);
            }

            AfterSlotChange(target);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "粘贴失败：" + ex.Message, "粘贴失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CloneSlot(SlotRef source)
    {
        if (_save is null || !IsValid(source))
            return;
        var mon = ReadSlot(source);
        if (mon.IsEmpty)
        {
            MessageBox.Show(this, "空格无法克隆。", "克隆", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (source.IsParty)
        {
            if (_save.PartyCount >= 6)
            {
                MessageBox.Show(this, "队伍已有 6 只，无法再克隆。", "克隆", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            for (var i = 0; i < 6; i++)
            {
                if (_save.GetParty(i).IsEmpty)
                {
                    WriteSlot(SlotRef.PartySlot(i), mon);
                    AfterSlotChange(SlotRef.PartySlot(i));
                    return;
                }
            }
            return;
        }

        for (var slot = 0; slot < MercurySave.BoxCapacity; slot++)
        {
            if (_save.GetBox(source.Box, slot).IsEmpty)
            {
                WriteSlot(SlotRef.BoxSlot(source.Box, slot), mon);
                AfterSlotChange(SlotRef.BoxSlot(source.Box, slot));
                return;
            }
        }
        MessageBox.Show(this, "该箱子没有空格可用于克隆。", "克隆", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void NewSlot(SlotRef target)
    {
        if (_save is null || !IsValid(target))
            return;
        using var picker = new SpeciesPicker(0);
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        if (picker.SelectedSpecies <= 0)
        {
            MessageBox.Show(this, "请选择有效的物种内部编号（大于 0）。", "新建", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var trainerId = _save.GetTrainer().ID32;
            var mon = MercuryPokemon.Create((ushort)picker.SelectedSpecies, trainerId);
            WriteSlot(target, mon);
            AfterSlotChange(target);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "新建失败：" + ex.Message, "新建失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteSlot(SlotRef target, bool confirm)
    {
        if (_save is null || !IsValid(target))
            return;
        if (ReadSlot(target).IsEmpty)
            return;
        if (confirm)
        {
            var result = MessageBox.Show(this, $"确定删除 {target.Describe()} 的宝可梦吗？", "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
                return;
        }

        try
        {
            MonOps.ClearSlot(_save, target);
            MarkEdited();
            AfterSlotChange(target);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "删除失败：" + ex.Message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportM3Box(SlotRef target)
    {
        if (_save is null || !IsValid(target))
            return;
        using var dialog = new OpenFileDialog
        {
            Title = "导入 .m3box（58 字节盒数据）",
            Filter = "Mercury 盒数据 (*.m3box)|*.m3box|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var bytes = File.ReadAllBytes(dialog.FileName);
            var mon = MercuryPokemon.FromBox(bytes);
            WriteSlot(target, mon);
            AfterSlotChange(target);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导入失败：" + ex.Message, "导入 .m3box", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportM3Box(SlotRef source)
    {
        if (_save is null || !IsValid(source))
            return;
        var mon = ReadSlot(source);
        if (mon.IsEmpty)
        {
            MessageBox.Show(this, "空格没有可导出的数据。", "导出 .m3box", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出 .m3box（58 字节盒数据，不是 .pk3）",
            Filter = "Mercury 盒数据 (*.m3box)|*.m3box|所有文件 (*.*)|*.*",
            FileName = $"{(mon.Species > 0 ? mon.Species.ToString() : "mon")}.m3box",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllBytes(dialog.FileName, mon.ToBoxBytes());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "导出失败：" + ex.Message, "导出 .m3box", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AfterSlotChange(SlotRef target)
    {
        if (_slotSelected && _current == target)
            LoadDraftFromSave();
        else
            RefreshBoxVisual(target);
        RefreshParty();
        RefreshBoxListText();
        RefreshSelection();
        UpdateStatus();
    }

    private static bool IsValid(SlotRef slot)
        => slot.IsParty ? slot.Slot is >= 0 and < 6 : slot.Box is >= 0 and < MercurySave.BoxCount && slot.Slot is >= 0 and < MercurySave.BoxCapacity;

    // ---------------------------------------------------------------- 查找/资料/训练师

    private void OpenSearch()
    {
        if (_save is null)
            return;
        using var form = new SearchForm(_save);
        if (form.ShowDialog(this) != DialogResult.OK || form.SelectedSlot is not { } hit)
            return;

        if (!hit.IsParty)
        {
            _loadingBox = true;
            _boxList.SelectedIndex = hit.Box;
            _loadingBox = false;
            _currentBox = hit.Box;
            RefreshBoxGrid();
        }
        SelectSlot(hit);
        Activate();
    }

    private void OpenDataSetup(DataSetupAction action = DataSetupAction.None)
    {
        // 注意：资料设置里的 ROM/字表解析是异步的，这里不主动 Dispose 该窗体，
        // 避免用户在任务完成前关闭对话框导致后续回调访问已释放控件；窗体由 GC 回收。
        var form = new DataSetupForm();
        form.DataChanged += () => _reloadAfterDataChange = true;
        if (action != DataSetupAction.None)
        {
            form.Shown += (_, _) =>
            {
                if (action == DataSetupAction.ImportCharmap)
                    form.BeginImportCharmap();
                else
                    form.BeginDownloadCharmap();
            };
        }
        form.ShowDialog(this);

        if (!_reloadAfterDataChange)
            return;
        _reloadAfterDataChange = false;
        SpriteCache.Clear();
        if (_slotSelected && _draft is not null)
            _detail.Bind(_draft, _current);
        RefreshAll();
    }

    private void OpenTrainer()
    {
        if (_save is null)
            return;
        using var form = new TrainerForm(_save);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;
        MarkEdited();
        UpdateStatus();
    }

    private void ShowHelp()
    {
        MessageBox.Show(this,
            "PKHeX Mercury / 水银版（非官方）\r\n\r\n" +
            "· 仅支持《精灵宝可梦 水银》改版存档（25 箱 × 30 格 + 6 队伍，58 字节盒数据）。\r\n" +
            "· 编辑后先点“应用到当前格”，再点“文件 → 另存为”导出；默认另存为 *_mercury.srm/.sav，不覆盖来源。\r\n" +
            "· 查找（Ctrl+F）可定位物种或昵称所在的箱子/队伍。\r\n" +
            "· 需要 ROM 基础值的功能（等级/经验换算、学习表、图片）在未加载 ROM 资料时会被禁用。\r\n" +
            "· 本工具不做原版合法性判定；资源预览只显示解析到的首帧，不承诺完整还原。",
            "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---------------------------------------------------------------- 刷新

    private void RefreshAll()
    {
        RefreshBoxList();
        RefreshBoxGrid();
        RefreshParty();
        RefreshSelection();
        UpdateEditingEnabled();
        UpdateStatus();
    }

    private void RefreshBoxList()
    {
        _loadingBox = true;
        try
        {
            _boxList.Items.Clear();
            for (var box = 0; box < MercurySave.BoxCount; box++)
                _boxList.Items.Add($"箱子 {box + 1}");
            if (_boxList.Items.Count > 0)
                _boxList.SelectedIndex = _currentBox;
            RefreshBoxListText();
        }
        finally
        {
            _loadingBox = false;
        }
    }

    private void RefreshBoxListText()
    {
        if (_save is null || _boxList.Items.Count == 0)
            return;
        var used = 0;
        for (var slot = 0; slot < MercurySave.BoxCapacity; slot++)
        {
            if (!_save.GetBox(_currentBox, slot).IsEmpty)
                used++;
        }
        _boxList.Items[_currentBox] = $"箱子 {_currentBox + 1}（{used}/{MercurySave.BoxCapacity}）";
        _boxOccupancy.Text = $"占用 {used} / {MercurySave.BoxCapacity}";
        _boxTitle.Text = $"箱子 {_currentBox + 1}";
    }

    private void RefreshBoxGrid()
    {
        if (_save is null)
        {
            foreach (var slot in _boxSlots)
                slot.SetMon(null);
            return;
        }
        for (var i = 0; i < MercurySave.BoxCapacity; i++)
        {
            _boxSlots[i].Reference = SlotRef.BoxSlot(_currentBox, i);
            _boxSlots[i].SetMon(_save.GetBox(_currentBox, i));
        }
    }

    private void RefreshParty()
    {
        for (var i = 0; i < 6; i++)
        {
            if (_save is null)
                _partySlots[i].SetMon(null);
            else
                _partySlots[i].SetMon(_save.GetParty(i));
        }
    }

    private void RefreshBoxVisual(SlotRef slot)
    {
        if (_save is null)
            return;
        if (slot.IsParty)
        {
            if (slot.Slot >= 0 && slot.Slot < 6)
                _partySlots[slot.Slot].SetMon(_save.GetParty(slot.Slot));
            return;
        }
        if (slot.Box != _currentBox)
        {
            RefreshBoxListText();
            return;
        }
        if (slot.Slot >= 0 && slot.Slot < MercurySave.BoxCapacity)
            _boxSlots[slot.Slot].SetMon(_save.GetBox(slot.Box, slot.Slot));
    }

    private void RefreshSelection()
    {
        for (var i = 0; i < MercurySave.BoxCapacity; i++)
            _boxSlots[i].Selected = _slotSelected && !_current.IsParty && _current.Box == _currentBox && _current.Slot == i;
        for (var i = 0; i < 6; i++)
            _partySlots[i].Selected = _slotSelected && _current.IsParty && _current.Slot == i;
    }

    private void UpdateEditingEnabled()
    {
        var enabled = _save is not null;
        foreach (var item in _editItems)
            item.Enabled = enabled;
    }

    private void UpdateStatus()
    {
        _stFile.Text = _save is null ? "未打开存档" : $"存档：{Path.GetFileName(_savePath ?? "(未命名)")}";
        if (_save is null)
        {
            _stBox.Text = "";
            _stTrainer.Text = "";
            _stDirty.Text = "";
        }
        else
        {
            var party = _save.PartyCount;
            _stBox.Text = $"箱子 {_currentBox + 1}/{MercurySave.BoxCount} · 队伍 {party}/6";
            _stTrainer.Text = TrainerSummary();
            var dirty = _draftDirty || _editVersion != _exportedVersion;
            _stDirty.Text = dirty ? "● 有未保存修改" : "已保存";
            _stDirty.ForeColor = dirty ? Color.Firebrick : SystemColors.ControlText;
        }
        _stSource.Text = AppState.DataSourceLabel;
        _stWarn.Text = _warnings.Count > 0 ? $"⚠ {_warnings.Count} 条警告" : "";
        Text = _save is null ? "PKHeX Mercury / 水银版" : $"PKHeX Mercury / 水银版 - {Path.GetFileName(_savePath ?? "未命名")}";
    }

    private string TrainerSummary()
    {
        if (_save is null)
            return "";
        try
        {
            var t = _save.GetTrainer();
            var name = UiText.Decode(t.NameBytes, "(无名)");
            var tid = t.ID32 & 0xFFFF;
            var sid = (t.ID32 >> 16) & 0xFFFF;
            return $"训练师 {name} · TID {tid} / SID {sid}";
        }
        catch
        {
            return "训练师：不可读";
        }
    }

    private void ShowWarnings()
    {
        if (_warnings.Count == 0)
            return;
        var text = string.Join("\r\n", _warnings.Select(w => "· " + w));
        MessageBox.Show(this,
            "存档加载时发现以下警告，请留意（程序不会静默修复）：\r\n\r\n" + text,
            "存档警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // ---------------------------------------------------------------- 拖放

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;
        var path = files[0];
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext is ".m3box")
        {
            if (!_slotSelected)
            {
                MessageBox.Show(this, "请先选择一个目标格，再拖入 .m3box。", "导入 .m3box", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                WriteSlot(_current, MercuryPokemon.FromBox(File.ReadAllBytes(path)));
                LoadDraftFromSave();
                AfterSlotChange(_current);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导入失败：" + ex.Message, "导入 .m3box", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        if (!GuardBeforeDestructive())
            return;
        LoadSave(path);
    }

    private void OnBoxListChanged(object? sender, EventArgs e)
    {
        if (_loadingBox || _save is null)
            return;
        var box = _boxList.SelectedIndex;
        if (box < 0 || box == _currentBox)
            return;
        if (!ApplyOrDiscardDraft())
        {
            _loadingBox = true;
            _boxList.SelectedIndex = _currentBox;
            _loadingBox = false;
            return;
        }

        _currentBox = box;
        _slotSelected = false;
        _draft = null;
        _detail.Clear();
        RefreshBoxGrid();
        RefreshSelection();
        RefreshBoxListText();
        UpdateStatus();
    }
}
