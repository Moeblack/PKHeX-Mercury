using System.Net.Http;
using PKHeX.Mercury.Core;

namespace PKHeX.Mercury;

/// <summary>资料设置：选择 ROM / 导入研究目录 / 导入 Profile / 导入字表 / 下载公开 HOME 字表。</summary>
internal sealed class DataSetupForm : Form
{
    private const string HomeCharmapUrl = "https://sum-light.github.io/azoth-wiki/home/app.html";

    private readonly Label _source = Ui.Label("");
    private readonly Label _status = Ui.Label("");
    private readonly Button _btnRom = Ui.Button("选择 ROM 并解析…", 180);
    private readonly Button _btnResearch = Ui.Button("导入研究目录…", 180);
    private readonly Button _btnProfile = Ui.Button("导入 Profile 目录…", 180);
    private readonly Button _btnCharmap = Ui.Button("导入字表 (json / game_data.js)…", 260);
    private readonly Button _btnDownload = Ui.Button("下载公开 HOME 字表（联网）", 260);

    public event Action? DataChanged;

    public void BeginImportCharmap() => _ = PickCharmapAsync();

    public void BeginDownloadCharmap() => _ = DownloadCharmapAsync();

    public DataSetupForm()
    {
        Text = "ROM 数据 / 资料设置";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 360);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(Ui.Label("当前数据源："), 0, 0);
        _source.Text = AppState.DataSourceLabel;
        root.Controls.Add(_source, 0, 1);

        var romFlow = new FlowLayoutPanel { AutoSize = true };
        romFlow.Controls.Add(_btnRom);
        root.Controls.Add(romFlow, 0, 2);

        var researchFlow = new FlowLayoutPanel { AutoSize = true };
        researchFlow.Controls.Add(_btnResearch);
        root.Controls.Add(researchFlow, 0, 3);

        var profileFlow = new FlowLayoutPanel { AutoSize = true };
        profileFlow.Controls.Add(_btnProfile);
        root.Controls.Add(profileFlow, 0, 4);

        var charmapFlow = new FlowLayoutPanel { AutoSize = true };
        charmapFlow.Controls.Add(_btnCharmap);
        root.Controls.Add(charmapFlow, 0, 5);

        var downloadFlow = new FlowLayoutPanel { AutoSize = true };
        downloadFlow.Controls.Add(_btnDownload);
        root.Controls.Add(downloadFlow, 0, 6);

        _status.Dock = DockStyle.Bottom;
        _status.Height = 60;
        _status.ForeColor = Color.DimGray;
        _status.Text = "提示：资料只缓存到本机 LocalApplicationData/PKHeX-Mercury/profile；下载字表需要你主动点击。";

        var close = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8),
        };
        var ok = Ui.Button("关闭", 100);
        ok.Click += (_, _) => Close();
        close.Controls.Add(ok);
        Controls.Add(root);
        Controls.Add(_status);
        Controls.Add(close);

        _btnRom.Click += async (_, _) => await PickRomAsync();
        _btnResearch.Click += async (_, _) => await PickResearchAsync();
        _btnProfile.Click += async (_, _) => await PickProfileAsync();
        _btnCharmap.Click += async (_, _) => await PickCharmapAsync();
        _btnDownload.Click += async (_, _) => await DownloadCharmapAsync();
    }

    private void SetBusy(bool busy, string status)
    {
        if (IsDisposed)
            return;
        _btnRom.Enabled = !busy;
        _btnResearch.Enabled = !busy;
        _btnProfile.Enabled = !busy;
        _btnCharmap.Enabled = !busy;
        _btnDownload.Enabled = !busy;
        _status.Text = status;
    }

    private async Task PickRomAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择水银 ROM",
            Filter = "GBA ROM (*.gba)|*.gba|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var path = dialog.FileName;
        SetBusy(true, "正在读取并解析 ROM…");
        try
        {
            var data = await Task.Run(() =>
            {
                var rom = File.ReadAllBytes(path);
                return MercuryGameData.FromRom(rom);
            });
            AppState.Apply(data, $"ROM：{Path.GetFileName(path)}（SHA {AppState.ShaShort()}）");
            await CacheProfileAsync();
            DataChanged?.Invoke();
            _source.Text = AppState.DataSourceLabel;
            SetBusy(false, "ROM 解析完成。");
        }
        catch (InvalidDataException ex)
        {
            SetBusy(false, "ROM 版本不符，已拒绝：" + ex.Message);
            MessageBox.Show(this, "该 ROM 的哈希与支持的固定版本不一致，为避免按错误偏移写入，已拒绝加载。\r\n\r\n" + ex.Message,
                "ROM 版本不符", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            SetBusy(false, "ROM 解析失败：" + ex.Message);
            MessageBox.Show(this, "解析 ROM 失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task PickResearchAsync()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择已有的水银研究数据目录（含 manifest 与 ROM-native 输出）" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var dir = dialog.SelectedPath;
        SetBusy(true, "正在读取研究数据…");
        try
        {
            var data = await Task.Run(() => MercuryGameData.FromResearch(dir));
            AppState.Apply(data, $"研究目录：{Path.GetFileName(dir)}（SHA {AppState.ShaShort()}）");
            await CacheProfileAsync();
            DataChanged?.Invoke();
            _source.Text = AppState.DataSourceLabel;
            SetBusy(false, "研究数据导入完成。");
        }
        catch (Exception ex)
        {
            SetBusy(false, "导入研究数据失败：" + ex.Message);
            MessageBox.Show(this, "导入研究数据失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task PickProfileAsync()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择已保存的 Mercury Profile 目录" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var dir = dialog.SelectedPath;
        SetBusy(true, "正在读取 Profile…");
        try
        {
            var data = await Task.Run(() => MercuryGameData.LoadProfile(dir));
            AppState.Apply(data, $"Profile：{dir}（SHA {AppState.ShaShort()}）");
            await CacheProfileAsync();
            DataChanged?.Invoke();
            _source.Text = AppState.DataSourceLabel;
            SetBusy(false, "Profile 导入完成。");
        }
        catch (Exception ex)
        {
            SetBusy(false, "读取 Profile 失败：" + ex.Message);
            MessageBox.Show(this, "读取 Profile 失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task PickCharmapAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入字表",
            Filter = "字表 (*.json;*.js)|*.json;*.js|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var path = dialog.FileName;
        SetBusy(true, "正在导入字表…");
        try
        {
            var text = await Task.Run(() => File.ReadAllText(path));
            await Task.Run(() => AppState.SetImportedCharmap(text));
            DataChanged?.Invoke();
            SetBusy(false, "字表导入完成，界面将按新字表显示。");
        }
        catch (Exception ex)
        {
            SetBusy(false, "字表导入失败：" + ex.Message);
            MessageBox.Show(this, "字表导入失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DownloadCharmapAsync()
    {
        var confirm = MessageBox.Show(
            this,
            "将联网下载公开的 HOME 字表（含 charmap）并缓存到本机：\r\n" + HomeCharmapUrl +
            "\r\n\r\n来源为用户可公开访问的研究页面，程序只解析其文本，不执行其中的脚本。是否继续？",
            "下载公开字表",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.OK)
            return;

        SetBusy(true, "正在下载字表…");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var text = await client.GetStringAsync(HomeCharmapUrl);
            await Task.Run(() => AppState.SetImportedCharmap(text));
            DataChanged?.Invoke();
            SetBusy(false, "字表下载并导入完成。");
        }
        catch (Exception ex)
        {
            SetBusy(false, "下载字表失败：" + ex.Message);
            MessageBox.Show(this, "下载字表失败（可改用本地导入）：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Task CacheProfileAsync()
        => Task.Run(AppState.TryCacheProfile);
}
