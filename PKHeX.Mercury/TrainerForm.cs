using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>
/// 训练师资料对话框。
/// 设计原则：
/// 1) Core 明确表示“未证实可写的字段 setter 抛 NotSupportedException”，
///    因此打开时先在一份独立副本上探测每个字段，未支持的字段直接禁用并标注，而不是等用户点应用后才报告。
/// 2) 提交是整体式的：先在副本上写全部“已启用且已支持”的字段，只有全部成功才调用一次 SetTrainer；
///    任一字段抛错则整体取消，不做任何部分写入。
/// </summary>
internal sealed class TrainerForm : Form
{
    private readonly MercurySave _save;
    private readonly MercuryTrainer _trainer;

    private readonly TextBox _txtName = Ui.Text(240);
    private readonly Label _lblNameNote = Ui.Label("");
    private readonly ComboBox _cmbGender = Ui.Combo(120);
    private readonly NumericUpDown _numTid = Ui.Num(0, 65535);
    private readonly NumericUpDown _numSid = Ui.Num(0, 65535);
    private readonly NumericUpDown _numMoney = Ui.Num(0, uint.MaxValue, 160);
    private readonly NumericUpDown _numCoins = Ui.Num(0, 999999999, 160);
    private readonly NumericUpDown _numHours = Ui.Num(0, 65535);
    private readonly NumericUpDown _numMinutes = Ui.Num(0, 59);
    private readonly NumericUpDown _numSeconds = Ui.Num(0, 59);
    private readonly Label _warn = Ui.Label("");

    private bool _supName;
    private bool _supGender;
    private bool _supId;
    private bool _supMoney;
    private bool _supCoins;
    private bool _supTime;

    public TrainerForm(MercurySave save)
    {
        _save = save;
        _trainer = save.GetTrainer();

        Text = "训练师资料";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 470);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(12),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;
        void Row(string label, Control control)
        {
            table.Controls.Add(Ui.Label(label), 0, row);
            table.Controls.Add(control, 1, row);
            row++;
        }

        _cmbGender.Items.Add(new ListEntry(0, "男性"));
        _cmbGender.Items.Add(new ListEntry(1, "女性"));

        Row("训练师名", _txtName);
        Row("", _lblNameNote);
        Row("性别", _cmbGender);
        Row("TID (表ID)", _numTid);
        Row("SID (里ID)", _numSid);
        Row("金钱", _numMoney);
        Row("代币", _numCoins);
        Row("游戏时间 · 时", _numHours);
        Row("游戏时间 · 分", _numMinutes);
        Row("游戏时间 · 秒", _numSeconds);
        Row("", _warn);

        _lblNameNote.ForeColor = Color.DimGray;
        _warn.AutoSize = true;
        _warn.MaximumSize = new Size(320, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
        };
        var ok = Ui.Button("确定", 100);
        var cancel = Ui.Button("取消", 100);
        ok.Click += (_, _) => Commit();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(buttons);
        Controls.Add(table);

        Load += (_, _) => Load_();
    }

    private void Load_()
    {
        _txtName.Text = UiText.Decode(_trainer.NameBytes);

        // 在独立副本上探测各字段是否真的可写（不触碰存档）。
        _supName = (_trainer.NameBytes?.Length ?? 0) > 0 && ProbeName();
        _supGender = Probe(() => _trainer.Gender = _trainer.Gender);
        _supId = Probe(() => _trainer.ID32 = _trainer.ID32);
        _supMoney = Probe(() => _trainer.Money = _trainer.Money);
        _supCoins = Probe(() => _trainer.Coins = _trainer.Coins);
        _supTime = Probe(() =>
        {
            _trainer.PlayedHours = _trainer.PlayedHours;
            _trainer.PlayedMinutes = _trainer.PlayedMinutes;
            _trainer.PlayedSeconds = _trainer.PlayedSeconds;
        });

        _txtName.Enabled = _supName;
        _lblNameNote.Text = ( _trainer.NameBytes?.Length ?? 0) == 0
            ? "该存档无训练师名栏位"
            : _supName ? $"占 {_trainer.NameBytes!.Length} 字节" : "Core 未确认该字段可写，已禁用";
        _cmbGender.Enabled = _supGender;
        _numTid.Enabled = _supId;
        _numSid.Enabled = _supId;
        _numMoney.Enabled = _supMoney;
        _numCoins.Enabled = _supCoins;
        _numHours.Enabled = _supTime;
        _numMinutes.Enabled = _supTime;
        _numSeconds.Enabled = _supTime;

        var disabled = new List<string>();
        if (!_supName) disabled.Add("训练师名");
        if (!_supGender) disabled.Add("性别");
        if (!_supId) disabled.Add("TID/SID");
        if (!_supMoney) disabled.Add("金钱");
        if (!_supCoins) disabled.Add("代币");
        if (!_supTime) disabled.Add("游戏时间");
        if (disabled.Count > 0)
        {
            _warn.ForeColor = Color.DimGray;
            _warn.Text = "以下字段尚未由 Core 确认可写，已禁用且不会被写入：" + string.Join("、", disabled);
        }

        SelectGender(_trainer.Gender);
        _numTid.Value = _trainer.ID32 & 0xFFFF;
        _numSid.Value = (_trainer.ID32 >> 16) & 0xFFFF;
        _numMoney.Value = _trainer.Money;
        _numCoins.Value = Math.Min(_trainer.Coins, 999_999_999u);
        _numHours.Value = Math.Clamp((int)_trainer.PlayedHours, 0, 65535);
        _numMinutes.Value = Math.Clamp((int)_trainer.PlayedMinutes, 0, 59);
        _numSeconds.Value = Math.Clamp((int)_trainer.PlayedSeconds, 0, 59);
    }

    private bool ProbeName()
    {
        try
        {
            _trainer.NameBytes = _trainer.NameBytes;
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch
        {
            // 其它异常不代表字段不可写，保留为可编辑。
            return true;
        }
    }

    private static bool Probe(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch
        {
            return true;
        }
    }

    private void SelectGender(byte value)
    {
        for (var i = 0; i < _cmbGender.Items.Count; i++)
        {
            if (_cmbGender.Items[i] is ListEntry e && e.Id == value)
            {
                _cmbGender.SelectedIndex = i;
                return;
            }
        }
        _cmbGender.SelectedIndex = 0;
    }

    private void Commit()
    {
        var trainer = _save.GetTrainer();

        if (_supName)
        {
            var cap = _trainer.NameBytes?.Length ?? 0;
            if (cap > 0 && !AppState.Codec.CanEncode(_txtName.Text, cap))
            {
                Warn("训练师名超出可编码长度，未做任何写入。");
                return;
            }
        }

        try
        {
            // 先在副本上写全部已支持字段；任一失败都会在 SetTrainer 之前抛出，整体取消。
            if (_supName)
            {
                var cap = _trainer.NameBytes?.Length ?? 0;
                if (cap > 0)
                    trainer.NameBytes = AppState.Codec.Encode(_txtName.Text, cap);
            }

            if (_supGender)
                trainer.Gender = (byte)(_cmbGender.SelectedItem is ListEntry e ? e.Id : 0);
            if (_supId)
                trainer.ID32 = ((uint)_numSid.Value & 0xFFFF) << 16 | ((uint)_numTid.Value & 0xFFFF);
            if (_supMoney)
                trainer.Money = (uint)_numMoney.Value;
            if (_supCoins)
                trainer.Coins = (uint)_numCoins.Value;
            if (_supTime)
            {
                trainer.PlayedHours = (ushort)_numHours.Value;
                trainer.PlayedMinutes = (byte)_numMinutes.Value;
                trainer.PlayedSeconds = (byte)_numSeconds.Value;
            }

            _save.SetTrainer(trainer);
            DialogResult = DialogResult.OK;
        }
        catch (NotSupportedException ex)
        {
            Warn("有字段未被 Core 确认可写，已取消整体写入（存档未改动）：" + ex.Message);
        }
        catch (Exception ex)
        {
            Warn("写入训练师资料失败（存档未改动）：" + ex.Message);
        }
    }

    private void Warn(string message)
    {
        _warn.ForeColor = Color.Firebrick;
        _warn.Text = message;
    }
}
