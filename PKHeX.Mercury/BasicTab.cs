using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>基础页：物种、名字、等级/经验、PID/TID/SID、性格/性别/闪光/特性、携带物、亲密度、蛋。</summary>
internal sealed class BasicTab : MonTab
{
    private readonly Label _lblSpecies = Ui.Label("—");
    private readonly Label _lblCurrent = Ui.Label("—");
    private readonly TextBox _txtNick = Ui.Text(220);
    private readonly Label _lblNickNote = Ui.Label("");
    private readonly NumericUpDown _numLevel = Ui.Num(1, 100);
    private readonly Label _lblLevelNote = Ui.Label("");
    private readonly TextBox _txtExp = Ui.Text(140);
    private readonly Label _lblExpNote = Ui.Label("");
    private readonly TextBox _txtPid = Ui.Text(140);
    private readonly Button _btnPid = Ui.Button("按约束生成 PID");
    private readonly NumericUpDown _numTid = Ui.Num(0, 65535);
    private readonly NumericUpDown _numSid = Ui.Num(0, 65535);
    private readonly ComboBox _cmbNature = Ui.Combo(150);
    private readonly ComboBox _cmbGender = Ui.Combo(110);
    private readonly CheckBox _chkShiny = Ui.Check("闪光");
    private readonly ComboBox _cmbAbility = Ui.Combo(200);
    private readonly NumericUpDown _numFriend = Ui.Num(0, 255);
    private readonly CheckBox _chkEgg = Ui.Check("蛋");
    private readonly ComboBox _cmbItem = Ui.Combo(260);
    private readonly Label _lblPidNote = Ui.Label("");
    private object? _itemSource;

    public BasicTab()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Padding = new Padding(8),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
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

        Row("物种", _lblSpecies);
        Row("当前派生", _lblCurrent);
        Row("名字", _txtNick, _lblNickNote);
        Row("等级", _numLevel, _lblLevelNote);
        Row("经验", _txtExp, _lblExpNote);
        Row("PID (HEX)", _txtPid, _btnPid);
        Row("TID / SID", _numTid, _numSid);
        Row("目标性格", _cmbNature);
        Row("目标性别", _cmbGender);
        Row("目标特性槽", _cmbAbility, _chkShiny);
        Row("携带物", _cmbItem);
        Row("亲密度", _numFriend);
        Row("其他", _chkEgg);
        Row("", _lblPidNote);

        for (var i = 0; i < UiText.Natures.Length; i++)
            _cmbNature.Items.Add(new ListEntry(i, UiText.NatureName(i)));
        _cmbGender.Items.Add(new ListEntry(0, UiText.GenderName(0)));
        _cmbGender.Items.Add(new ListEntry(1, UiText.GenderName(1)));
        _cmbGender.Items.Add(new ListEntry(2, UiText.GenderName(2)));

        _lblNickNote.ForeColor = Color.DimGray;
        _lblLevelNote.ForeColor = Color.DimGray;
        _lblExpNote.ForeColor = Color.DimGray;
        _lblPidNote.ForeColor = Color.DimGray;

        Controls.Add(table);

        _txtNick.TextChanged += (_, _) => CommitNickname();
        _numLevel.ValueChanged += (_, _) => CommitLevel();
        _txtExp.Leave += (_, _) => CommitExp();
        _txtPid.TextChanged += (_, _) => CommitPid();
        _btnPid.Click += (_, _) => GeneratePid();
        _numTid.ValueChanged += (_, _) => CommitId();
        _numSid.ValueChanged += (_, _) => CommitId();
        _chkShiny.CheckedChanged += (_, _) => UpdateCurrentLabel();
        _cmbAbility.SelectedIndexChanged += (_, _) => UpdateCurrentLabel();
        _cmbItem.SelectedIndexChanged += (_, _) => CommitItem();
        _numFriend.ValueChanged += (_, _) => CommitFriendship();
        _chkEgg.CheckedChanged += (_, _) => CommitEgg();
    }

    protected override void Reload()
    {
        Loading = true;
        try
        {
            var mon = Mon;
            var enabled = mon is not null && !mon.IsEmpty;
            _lblPidNote.Text = "直接输入 PID 只显示派生结果，不会自动重抽；改动性格/性别/闪光/特性后请点“按约束生成 PID”。";
            if (!AppState.HasRomData)
                _lblPidNote.Text += "\r\n当前为数字 ID 模式：中文名、等级换算、PID 生成等依赖 ROM 数据的功能已禁用。";
            if (!enabled)
            {
                _lblSpecies.Text = "（未选择或空格）";
                _lblCurrent.Text = "—";
                _txtNick.Text = "";
                _txtPid.Text = "";
                _txtExp.Text = "";
                SetEnabledAll(false);
                return;
            }

            var speciesLabel = Species is { HasData: true }
                ? $"{mon!.Species} {Species.Name}"
                : $"{mon!.Species}（无 ROM 物种表）";
            _lblSpecies.Text = speciesLabel;

            _txtNick.Text = UiText.Decode(mon.NicknameBytes);
            _txtNick.Enabled = mon.NicknameBytes is { Length: > 0 };
            UpdateNickNote();

            var level = ResolveLevel(mon);
            _numLevel.Value = Math.Clamp(level, 1, 100);
            _numLevel.Enabled = AppState.HasGrowthTables;
            _lblLevelNote.Text = AppState.HasGrowthTables ? "" : "无 ROM 成长表，等级换算已禁用";
            _txtExp.Text = mon.Experience.ToString();
            _lblExpNote.Text = AppState.HasGrowthTables ? "" : "可手动编辑原始经验值";

            _txtPid.Text = UiText.Hex(mon.PID);
            var id32 = mon.ID32;
            _numTid.Value = id32 & 0xFFFF;
            _numSid.Value = (id32 >> 16) & 0xFFFF;

            SelectNature(mon.Nature);
            var ratio = Species?.GenderRatio ?? (byte)255;
            SelectGender(SafeGender(mon, ratio));
            _chkShiny.Checked = mon.IsShiny;
            BuildAbilityList(mon.Species);
            SelectAbility(EffectiveAbilitySlot(mon));

            RefreshItemList();
            EntryList.SelectId(_cmbItem, mon.HeldItem);

            _numFriend.Value = Math.Clamp((int)mon.Friendship, 0, 255);
            _chkEgg.Checked = mon.IsEgg;

            _btnPid.Enabled = Species is { HasData: true };
            SetEnabledAll(true);
            UpdateCurrentLabel();
        }
        finally
        {
            Loading = false;
        }
    }

    private void SetEnabledAll(bool enabled)
    {
        _txtNick.Enabled &= enabled;
        _numLevel.Enabled = enabled && AppState.HasGrowthTables;
        _txtExp.Enabled = enabled;
        _txtPid.Enabled = enabled;
        _btnPid.Enabled = enabled && Species is { HasData: true };
        _numTid.Enabled = enabled;
        _numSid.Enabled = enabled;
        _cmbNature.Enabled = enabled;
        _cmbGender.Enabled = enabled;
        _chkShiny.Enabled = enabled;
        _cmbAbility.Enabled = enabled;
        _numFriend.Enabled = enabled;
        _chkEgg.Enabled = enabled;
        _cmbItem.Enabled = enabled;
    }

    private int ResolveLevel(MercuryPokemon mon)
    {
        if (mon.PartyLevel > 0)
            return mon.PartyLevel;
        if (!AppState.HasGrowthTables)
            return Math.Clamp((int)mon.PartyLevel, 1, 100);
        try
        {
            return AppState.Data.GetLevel(mon.Species, mon.Experience);
        }
        catch
        {
            return 1;
        }
    }

    private static int SafeGender(MercuryPokemon mon, byte ratio)
    {
        try
        {
            return mon.GetGender(ratio);
        }
        catch
        {
            return 0;
        }
    }

    private void UpdateNickNote()
    {
        var cap = Mon?.NicknameBytes?.Length ?? 0;
        if (cap <= 0)
        {
            _lblNickNote.Text = "该记录无名字栏位";
            return;
        }
        var text = _txtNick.Text;
        var ok = AppState.Codec.CanEncode(text, cap);
        _txtNick.ForeColor = ok ? SystemColors.WindowText : Color.Firebrick;
        _lblNickNote.Text = ok ? $"占 {cap} 字节" : $"超出 {cap} 字节，未写入";
    }

    private void CommitNickname()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        var cap = Mon.NicknameBytes?.Length ?? 0;
        UpdateNickNote();
        if (cap <= 0)
            return;
        if (!AppState.Codec.CanEncode(_txtNick.Text, cap))
            return;
        try
        {
            Mon.NicknameBytes = AppState.Codec.Encode(_txtNick.Text, cap);
            Mark();
        }
        catch
        {
            // 编码失败保持原值。
        }
    }

    private void CommitLevel()
    {
        if (Loading || Mon is null || Mon.IsEmpty || !AppState.HasGrowthTables)
            return;
        try
        {
            var exp = AppState.Data.GetExperience(Mon.Species, (byte)_numLevel.Value);
            Mon.Experience = exp;
            Loading = true;
            _txtExp.Text = exp.ToString();
            Loading = false;
            Mark();
        }
        catch
        {
            Loading = false;
        }
    }

    private void CommitExp()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        if (!uint.TryParse(_txtExp.Text.Trim(), out var exp))
        {
            _txtExp.Text = Mon.Experience.ToString();
            return;
        }
        Mon.Experience = exp;
        if (AppState.HasGrowthTables)
        {
            try
            {
                Loading = true;
                _numLevel.Value = Math.Clamp((int)AppState.Data.GetLevel(Mon.Species, exp), 1, 100);
                Loading = false;
            }
            catch
            {
                Loading = false;
            }
        }
        Mark();
    }

    private void CommitPid()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        var value = Ui.ParseHexU(_txtPid.Text, Mon.PID);
        Mon.PID = value;
        UpdateCurrentLabel();
        Mark();
    }

    private void GeneratePid()
    {
        if (Mon is null || Mon.IsEmpty || Species is not { HasData: true })
            return;
        var nature = _cmbNature.SelectedItem is ListEntry n ? n.Id : Mon.Nature;
        var gender = _cmbGender.SelectedItem is ListEntry g ? g.Id : 0;
        var shiny = _chkShiny.Checked;
        var slot = _cmbAbility.SelectedItem is ListEntry a ? a.Id : Mon.AbilitySlot;
        try
        {
            Mon.SetPersonality(nature, gender, Species.GenderRatio, shiny, slot);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "无法生成满足该组合的 PID：" + ex.Message, "约束冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Loading = true;
        _txtPid.Text = UiText.Hex(Mon.PID);
        Loading = false;
        UpdateCurrentLabel();
        Mark();
    }

    private void CommitId()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        var tid = (uint)_numTid.Value & 0xFFFF;
        var sid = (uint)_numSid.Value & 0xFFFF;
        Mon.ID32 = (sid << 16) | tid;
        UpdateCurrentLabel();
        Mark();
    }

    private void CommitItem()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        Mon.HeldItem = (ushort)Math.Clamp(EntryList.SelectedId(_cmbItem), 0, ushort.MaxValue);
        Mark();
    }

    private void CommitFriendship()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        Mon.Friendship = (byte)Math.Clamp((int)_numFriend.Value, 0, 255);
        Mark();
    }

    private void CommitEgg()
    {
        if (Loading || Mon is null || Mon.IsEmpty)
            return;
        Mon.IsEgg = _chkEgg.Checked;
        Mark();
    }

    private void RefreshItemList()
    {
        if (ReferenceEquals(_itemSource, AppState.Data) && _cmbItem.Items.Count > 0)
            return;
        _itemSource = AppState.Data;
        _cmbItem.Items.Clear();
        foreach (var entry in EntryList.Items())
            _cmbItem.Items.Add(entry);
    }

    private void SelectNature(int nature)
    {
        for (var i = 0; i < _cmbNature.Items.Count; i++)
        {
            if (_cmbNature.Items[i] is ListEntry e && e.Id == nature)
            {
                _cmbNature.SelectedIndex = i;
                return;
            }
        }
        _cmbNature.SelectedIndex = 0;
    }

    private void SelectGender(int gender)
    {
        for (var i = 0; i < _cmbGender.Items.Count; i++)
        {
            if (_cmbGender.Items[i] is ListEntry e && e.Id == gender)
            {
                _cmbGender.SelectedIndex = i;
                return;
            }
        }
        _cmbGender.SelectedIndex = 0;
    }

    private void BuildAbilityList(int species)
    {
        _cmbAbility.Items.Clear();
        var sp = Species;
        for (var slot = 0; slot < 3; slot++)
        {
            if (!AbilityAvailable(sp, slot))
                continue;
            _cmbAbility.Items.Add(new ListEntry(slot, $"槽 {slot}：{AbilityLabel(species, slot)}"));
        }
        if (_cmbAbility.Items.Count == 0)
            _cmbAbility.Items.Add(new ListEntry(0, $"槽 0：{AbilityLabel(species, 0)}"));
    }

    /// <summary>该物种是否实际存在该特性槽（第一/第二/隐藏）；存储 ID 为 0 视为不存在。</summary>
    private static bool AbilityAvailable(MercurySpecies? sp, int slot)
    {
        if (sp is not { HasData: true })
            return slot == 0;
        if (slot == 0)
            return true;
        var abilities = sp.Abilities;
        return abilities is { Length: > 0 } && slot < abilities.Length && abilities[slot] != 0;
    }

    private static string AbilityLabel(int species, int slot)
    {
        try
        {
            var name = AppState.Data.AbilityName(species, slot);
            return string.IsNullOrWhiteSpace(name) ? $"特性#{slot}" : name;
        }
        catch
        {
            return $"特性#{slot}";
        }
    }

    private void SelectAbility(int slot)
    {
        for (var i = 0; i < _cmbAbility.Items.Count; i++)
        {
            if (_cmbAbility.Items[i] is ListEntry e && e.Id == slot)
            {
                _cmbAbility.SelectedIndex = i;
                return;
            }
        }
        if (_cmbAbility.Items.Count > 0)
            _cmbAbility.SelectedIndex = 0;
    }

    private void UpdateCurrentLabel()
    {
        var mon = Mon;
        if (mon is null || mon.IsEmpty)
        {
            _lblCurrent.Text = "—";
            return;
        }
        var ratio = Species?.GenderRatio ?? (byte)255;
        var gender = SafeGender(mon, ratio);
        var storedSlot = mon.AbilitySlot;
        var effectiveSlot = EffectiveAbilitySlot(mon);
        var ability = AbilityLabel(mon.Species, effectiveSlot);
        var slotNote = effectiveSlot == storedSlot ? "" : "（存储标记无对应特性，按 PID parity 回退）";
        _lblCurrent.Text =
            $"性格 {UiText.NatureName(mon.Nature)} · 性别 {UiText.GenderName(gender)} · 闪光 {(mon.IsShiny ? "是" : "否")} · 特性槽 {storedSlot}（{ability}{slotNote}）";
    }

    /// <summary>
    /// 实际生效的特性槽：存储槽存在时用它；隐藏标记存在但物种无隐藏特性时，
    /// 按 PID parity 判断第二特性（槽1），否则回退第一特性（槽0）。
    /// </summary>
    private int EffectiveAbilitySlot(MercuryPokemon mon)
    {
        var stored = mon.AbilitySlot;
        if (AbilityAvailable(Species, stored))
            return stored;

        if (stored == 2)
        {
            var odd = (mon.PID & 1) != 0;
            if (odd && AbilityAvailable(Species, 1))
                return 1;
            return 0;
        }

        return AbilityAvailable(Species, 0) ? 0 : stored;
    }
}
