namespace PKHeX.Mercury;

/// <summary>水银版专用说明：不冒充上游原版，也不做原版合法性判定。</summary>
internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "关于 PKHeX Mercury / 水银版";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 380);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Text =
                "PKHeX Mercury / 水银版\r\n" +
                "非官方 · 仅供《精灵宝可梦 水银》（Pokémon Mercury）同人改版使用\r\n\r\n" +
                "· 本项目是独立专用的桌面编辑器，复用上游 PKHeX 的部分基础代码，但不对应、不冒充上游官方发布，也不代表 Project Pokémon 或 PKHeX 官方。\r\n\r\n" +
                "· 本编辑器使用水银改版自己的存档结构（25 箱 × 30 格 + 6 只队伍、58 字节盒数据、专用校验与扇区规则），与原版零售 GBA（PK3/SAV3，80 字节与 14 箱）存档不兼容。切勿将水银存档当作原版存档处理。\r\n\r\n" +
                "· 原版的合法性判定（legality）对水银改版数据无效，因此本工具不提供合法性结论，只做数据编辑。\r\n\r\n" +
                "· 图片/资源预览来自用户自己提供的 ROM，只显示解析到的资源（通常为正面图首帧），不承诺完整还原所有场景、动画或调色板。未解析到的资源会明确留空，不显示占位假图。\r\n\r\n" +
                "· 本程序不附带任何 ROM、游戏图片、字表或私人存档。物种/招式等中文名称由用户导入自己的 ROM 或公开研究资料（如 HOME 字表）后生成，真实数值以 ROM 为准。\r\n\r\n" +
                "· 资料缓存只写入用户本机的 LocalApplicationData/PKHeX-Mercury/profile，不写入源码目录，也不会上传。\r\n\r\n" +
                "· 许可证：GPL-3.0-or-later。",
        };

        var ok = Ui.Button("关闭", 100);
        ok.Click += (_, _) => Close();
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8),
        };
        bottom.Controls.Add(ok);

        Controls.Add(text);
        Controls.Add(bottom);
        AcceptButton = ok;
    }
}
