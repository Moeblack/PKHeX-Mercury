# PKHeX Mercury · 宝可梦水银版

基于 [kwsch/PKHeX](https://github.com/kwsch/PKHeX) 的非官方水银 **1.1** 专用存档编辑器。中文 Windows 桌面界面，使用水银自身的内部编号、58 字节压缩宝可梦记录与 25 盒存储布局；不把水银存档交给原版 PK3／SAV3 处理。

**下载入口：[Releases](https://github.com/Moeblack/PKHeX-Mercury/releases)**。解压 Windows x64 便携包，运行 **`PKHeX.Mercury.exe`**。便携包自带 .NET 运行时；从源码编译需要 .NET 10 SDK。

## 使用方法

1. 首次打开，在“ROM 数据”中选择自己的水银 1.1 `.gba`。只读取 ROM，不修改游戏文件。
2. 在“名称字表”中导入 HOME 页面／`game_data.js`／字符映射 JSON，或点击界面中的下载字表按钮。联网只发生在点击该按钮后；字表仅补充中文字形标签，不替换 ROM 数值。也支持导入已有的 ROM 原生解包研究目录或本地 profile。
3. 打开游戏内保存产生的 `.srm` 或 `.sav`，不要使用即时存档。支持 128 KiB，以及附有 16 字节 RTC 尾部的格式；尾部原样保留。
4. 选择盒子或队伍中的宝可梦，修改后点击“应用到当前格”。右键菜单提供复制、移动／交换、克隆、删除，以及 `.m3box` 单体导入导出。
5. 使用“另存为”导出修改后的存档，再自行放回模拟器／设备。默认生成新文件，不自动覆盖来源；显式选择覆盖时会确认并保存备份。

本地资料缓存位于 `%LOCALAPPDATA%/PKHeX-Mercury/profile/`。其中的 `rom-cache.gba` 是你选择的 ROM 的本地副本，用于重启后恢复原生图片；**不要把此缓存或个人存档上传到仓库**。公开发行包不包含这些文件。

## 编辑范围

- **25 个盒子、每盒 30 格，以及 6 格队伍**；支持查找、复制、移动、克隆和新建。
- 物种／形态内部编号、昵称、初训家、PID、TID／SID、性别、性格、闪光、特性槽、携带物、亲密度及蛋标志。
- 四个独立招式槽、PP 提升、升级学习表辅助、六项个体值和努力值。
- 等级／经验使用当前 ROM 的实际成长表；队伍数值考虑存档中的改版数值模式。
- 精灵球、相遇字段、病毒及标记；未编辑的未知字段保留。
- 训练师名称、性别、ID、金钱、扩展代币与游戏时间。
- 原生普通／闪光精灵预览；图像只是第一帧、第一调色板页预览，不宣称还原全部运行状态。

特性名称池索引、存储特性 ID、内部物种编号、图像资源索引并非同一套编号。界面使用水银规则，不提供原版 PKHeX 的“合法性通过”结论。`.m3box` 是水银的 **58 字节**记录，**不是 `.pk3`**，不能通过改扩展名互换。

## 支持的 ROM

当前配置严格对应以下输入，不会把固定偏移用于未经确认的版本：

```text
大小：33,554,432 字节
SHA-256：131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd
```

存档容器会核对扇区签名、完整 section 集合、计数器和校验和；最新槽无效时，仅在另一槽完整有效的情况下回退并提示。未修改导出应与输入逐字节相同；修改仅写目标字段及必要校验和，保留备用槽、额外区域和 RTC 尾部。

没有导入 ROM 时仅提供数字编号模式，需要种族值或成长表的操作会禁用。容器签名用于识别这类扩展布局，但不是某一改版的唯一身份证明；请仅使用自己的水银 1.1 存档。

## 从源码构建

```powershell
dotnet build PKHeX.Mercury.slnx -c Release
dotnet run --project Tests/PKHeX.Mercury.Tests -c Release
dotnet publish PKHeX.Mercury/PKHeX.Mercury.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish
```

`PKHeX.Mercury.slnx` 是水银入口；`PKHeX.sln`／`PKHeX.slnx` 和原版项目保留作为上游源码。水银项目复用 `PKHeX.Core`，没有修改原版各世代存档类来兼容私有格式。

回归检查默认只生成合成数据，不需要 ROM 或私人存档。可选 `--rom`、`--research`、`--save` 参数用于本地只读核对；`--save` 不覆盖原文件。检查覆盖范围及其限定见 [验证说明](docs/verification.md)，不把二进制检查表述为模拟器或实机游玩验收。

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
