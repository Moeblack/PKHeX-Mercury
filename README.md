# PKHeX Mercury · 宝可梦水银版

基于 [kwsch/PKHeX](https://github.com/kwsch/PKHeX) 的非官方水银 **1.1** 格式适配。使用上游原生 `PKHeX.WinForms` 主窗口、编辑控件与中文本地化；水银内部编号、58 字节压缩记录与 25 盒存储布局由专用适配器处理，不冒充原版 PK3／SAV3。

**v0.2.0 使用原生界面，启动程序为 `PKHeX.exe`。** 请从 [GitHub Releases](https://github.com/Moeblack/PKHeX-Mercury/releases) 下载 Windows x64 便携包，解压后直接运行，无需另装 .NET。历史 v0.1.0 使用旧独立界面。源码构建需要 .NET 10 SDK；CI 发布目录为 `artifacts/native-publish/`。

## 使用方法

1. 在原生“工具 → 水银”菜单中配置自己的水银 1.1 `.gba`，或导入已有 profile／ROM 原生研究目录。只读取 ROM，不修改游戏文件。
2. 同一菜单支持导入 HOME 页面、`game_data.js` 或字符映射 JSON，以及显式下载公开 HOME 字表。字表只补充文字标签，不替换 ROM 数值。
3. 使用原生“打开”功能载入游戏内保存的 `.srm`／`.sav`。支持 128 KiB 和附有 16 字节 RTC 尾部的格式；即时存档不属于此格式。
4. 使用上游原有操作：右键“查看／设置／删除”，或 Ctrl／Shift／Alt 加单击；编辑左侧详情后，以“设置”写入目标格。盒子切换、队伍、拖放和文件导出沿用原生控件，不再使用“应用到当前格／放弃草稿”的独立流程。
5. 从原生文件菜单导出存档。单体文件沿用原生导出入口，水银类型扩展名为 `.mercurypkm`；导入也接受已有 `.m3box` 的 58 字节记录。

本地资料缓存位于 `%LOCALAPPDATA%/PKHeX-Mercury/profile/`。其中的 `rom-cache.gba` 是你选择的 ROM 的本地副本，用于重启后恢复原生图片；**不要把此缓存或个人存档上传到仓库**。公开发行包不包含这些文件。

## 编辑范围

- **25 个盒子、每盒 30 格，以及 6 格队伍**；支持查找、复制、移动、克隆和新建。
- 物种／形态内部编号、昵称、初训家、PID、TID／SID、性别、性格、闪光、特性槽、携带物、亲密度及蛋标志。
- 四个招式槽、PP／PP 提升、六项个体值和努力值；招式资料来自水银 ROM。
- 等级／经验使用当前 ROM 的实际成长表；队伍数值考虑存档中的改版数值模式。
- 精灵球、相遇字段、病毒及标记；未编辑的未知字段保留。
- 训练师名称、性别、ID、金钱、扩展代币与游戏时间。
- 原生物品栏：普通道具、关键道具、球、技能机、树果及PC道具，按本ROM实际容量和存档映射读写。
- 原生普通／闪光精灵预览；图像只是第一帧、第一调色板页预览，不宣称还原全部运行状态。

特性名称池索引、存储特性 ID、内部物种编号、图像资源索引并非同一套编号。**当前水银存档的合法性检查已暂停**，手动检查会明确提示暂停；未检查不代表合法，原版存档检查不受影响。存档扇区校验和及必要的结构检查仍保留。水银单体记录不是 `.pk3`，不能靠改扩展名转换。

2026-10-02 精灵球数据源修订：水银不再用零售球道具索引访问其750项道具表。Main已从本ROM捕获代码确认：道具的type字节原样写入球字段，因此通过球类道具的type取得真实名称，0对应大师球而非空值；原生球图标与选择器使用ROM图块、调色板、OAM及动画首帧，不套用零售图片。0–255仍只是存储字节范围，不是256种已证明可获得的球；未映射字节保留明确编号。旧profile从本地已校验ROM补齐pocket/type字段。选择器保留点击、Enter选择及写回。同步隔离PP、性格／性别PID修改和摘要中的零售表调用，并加入水银单体文件筛选。程序已重新生成，未启动、未运行测试。

后续数据源接入：类型图标按本ROM的type+1描述表、实际图块和调色板绘制；相遇地点使用已证regionMapSectionId到区域名称的关系，表内无效指针明确标记而不冒充零售地点；来源字段按真实四位值读写，不再固定返回FR并忽略修改。若文件同时满足水银与原版结构及校验，使用上游原生版本选择对话框由用户明确选择；取消则不打开，不再以已加载ROM替用户断定存档格式。

完整地址、指令证据、适用条件和解包工作线仍需解释的特殊值记录在研究目录 `PKHeX原生适配对接.md`；共享总账本登记了 `pkhex-native-ui-integration` 入口。源码接入及程序生成不等于运行测试或UI验收。

2026-10-02 箱子页签缺失修复：Main核对发现MercurySaveFile构造只初始化Party，遗漏独立箱子缓冲的Box基址，导致上游HasBox恒为false；SAVEditor.ToggleViewBox因此移除箱子页签，BoxEditor也跳过箱名刷新。现初始化Box=0，不修改原生显隐、按钮或拖放操作。修正版位于`artifacts/native-box-fix/PKHeX.exe`，避免覆盖运行中的旧程序。使用最近打开的Pokemon_Mercury_FC.srm实际加载，确认盒子页签、6×5槽位和精灵图像显示；通过原生左右按钮确认Box 2→Box 3→Box 2。窗口取证为`artifacts/native-box-fix/box-load-debug.png`。没有保存或导出用户存档；仅验证本次箱子加载与切换，不代表全部UI、合法性警告或存档写回均已通过验证。

2026-10-02 道具与合法性修订：`artifacts/native-inventory-fix/PKHeX.exe` 已接入ROM已证背包/PC存储，修复原生道具窗口读取空Pouches造成的越界；道具图标不再做零售ID转换。用户授权水银暂时停止合法性检查：引擎保持未分析状态、不伪报合法；槽位、编辑器、招式与QR不显示合法性判定；原生手动检查入口明确提示暂停，原版存档路径保留。Main已运行限定内存检查，并实际打开物品栏、确认箱子警告消失和手动暂停提示；没有保存用户存档。图片证据为该目录`inventory-debug.png`与`legality-paused-debug.png`。背包依据及729号图像未闭合项见`docs/mercury-save-format.md`的Inventory节；这不是全UI完成声明。

后续修正版：`artifacts/native-ui-fix/PKHeX.exe`。原生EntityTemplates会选择MaxSpeciesID作为默认模板，水银显示1553号“桃歹郎”属于原生行为，未改为自创启动选择。已修复默认昵称调用零售种类表的问题，并纠正SetString把字符上限误当字节容量、导致中文昵称截断的问题；空持有物/招式/特性现在使用原生本地化“(无)”。限定回归已确认默认种类不变、昵称为完整“桃歹郎”且不是自定义昵称、原版默认名称不变。729号图像的实际背包链已追至BIOS LZ77，表项指向的资源头不符合该格式；未找到正确替代资源，仍未恢复图像，只增加明确诊断提示，不借图、不改ROM。详细地址见存档格式文档Item 729 follow-up。整体UI目标仍未完成。

## 支持的 ROM

当前配置严格对应以下输入，不会把固定偏移用于未经确认的版本：

```text
大小：33,554,432 字节
SHA-256：131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd
```

存档容器会核对扇区签名、完整 section 集合、计数器和校验和；最新槽无效时，仅在另一槽完整有效的情况下回退并提示。未修改导出应与输入逐字节相同；修改仅写目标字段及必要校验和，保留备用槽、额外区域和 RTC 尾部。

水银读取器需要对应版本的 ROM/profile。原版第三世代也使用相同的扇区签名；已加载的 ROM/profile 不能独自证明任意存档属于水银，请只使用相应版本的水银存档。没有配置水银资料时保留上游原版格式入口。

## 从源码构建

```powershell
dotnet build PKHeX.Mercury.slnx -c Release
dotnet publish PKHeX.WinForms/PKHeX.WinForms.csproj -c Release -r win-x64 --self-contained true -o artifacts/native-publish
```

`PKHeX.Mercury.slnx` 包含上游主程序、绘图项目和水银适配器。旧 `PKHeX.Mercury` 独立界面代码不再进入此解决方案或发行工作流。原版世代类不承担水银记录的解析。

按用户要求，本任务新增的测试代码、本地诊断项目及测试产物已移除；后续修改不编写或执行测试。

## 格式依据与许可

- [存档格式与实际 ROM 消费者](docs/mercury-save-format.md)
- [ROM 配置、文字标签与原生图片](docs/mercury-rom-profile.md)
- [第三方来源与许可范围](docs/mercury-notices.md)
- 代码沿用 **GPL-3.0-or-later**，完整许可见 [LICENSE](LICENSE)。保留上游历史、署名和原版说明；水银适配是独立贡献，不代表上游 PKHeX 官方支持。
- 游戏 ROM、提取图片、音频、剧情文本和私人存档不随本项目新增发布。代码许可不改变游戏资源及外部资料的权利归属。

---

## 上游 PKHeX 原始说明

PKHeX
=====
<div>
  <span>English</span> / <a href=".github/README-es.md">Español</a> / <a href=".github/README-fr.md">Français</a> / <a href=".github/README-de.md">Deutsch</a> / <a href=".github/README-it.md">Italiano</a> / <a href=".github/README-ko.md">한국어</a> / <a href=".github/README-zh-Hant.md">繁體中文</a> / <a href=".github/README-zh-Hans.md">简体中文</a>
</div>

![License](https://img.shields.io/badge/License-GPLv3-blue.svg)

Pokémon core series save editor, programmed in [C#](https://en.wikipedia.org/wiki/C_Sharp_%28programming_language%29).

Supports the following files:
* Save files ("main", \*.sav, \*.dsv, \*.dat, \*.gci, \*.bin)
* GameCube Memory Card files (\*.raw, \*.bin) containing GC Pokémon savegames.
* Individual Pokémon entity files (.pk\*, \*.ck3, \*.xk3, \*.pb7, \*.sk2, \*.bk4, \*.rk4)
* Mystery Gift files (\*.pgt, \*.pcd, \*.pgf, .wc\*) including conversion to .pk\*
* Importing GO Park entities (\*.gp1) including conversion to .pb7
* Importing teams from Decrypted 3DS Battle Videos
* Transferring from one generation to another, converting formats along the way.

Data is displayed in a view which can be edited and saved.
The interface can be translated with resource/external text files so that different languages can be supported.

Pokémon Showdown sets and QR codes can be imported/exported to assist in sharing.

PKHeX expects save files that are not encrypted with console-specific keys. Use a savedata manager to import and export savedata from the console ([Checkpoint](https://github.com/FlagBrew/Checkpoint), save_manager, [JKSM](https://github.com/J-D-K/JKSM), or SaveDataFiler).

**We do not support or condone cheating at the expense of others. Do not use significantly hacked Pokémon in battle or in trades with those who are unaware hacked Pokémon are in use.**

## Screenshots

![Main Window](https://i.imgur.com/pIHdoTp.png)

## Building

PKHeX is a Windows Forms application which requires [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

The executable can be built with any compiler that supports C# 14.

### Build Configurations

Use the Debug or Release build configurations when building. There isn't any platform specific code to worry about!

## Dependencies

PKHeX's QR code generation code is taken from [QRCoder](https://github.com/codebude/QRCoder), which is licensed under [the MIT license](https://github.com/codebude/QRCoder/blob/master/LICENSE.txt).

PKHeX's shiny sprite collection is taken from [pokesprite](https://github.com/msikma/pokesprite), which is licensed under [the MIT license](https://github.com/msikma/pokesprite/blob/master/LICENSE).

PKHeX's Pokémon Legends: Arceus sprite collection is taken from the [National Pokédex - Icon Dex](https://www.deviantart.com/pikafan2000/art/National-Pokedex-Version-Delta-Icon-Dex-824897934) project and its abundance of collaborators and contributors.

### IDE

PKHeX can be opened with IDEs such as [Visual Studio](https://visualstudio.microsoft.com/downloads/) by opening the .sln or .csproj file.
