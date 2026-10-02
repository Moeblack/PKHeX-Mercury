# 来源与许可

## 项目代码

PKHeX Mercury基于[kwsch/PKHeX](https://github.com/kwsch/PKHeX)，最初采用的上游提交为`542111fc8584ff29c9d1455553b8acd0e1f8a59a`。仓库保留上游历史、署名与许可文件；项目代码使用GPL-3.0-or-later，完整条款见[LICENSE](../LICENSE)。

v0.2.0使用上游`PKHeX.WinForms`主窗口与编辑控件，并链接上游绘图项目`PKHeX.Drawing`、`PKHeX.Drawing.PokeSprite`、`PKHeX.Drawing.Misc`；水银专用的`PKHeX.Mercury.Core`负责其存档与数据适配，适配层经`PKHeX.WinForms/Mercury`接入原生主窗口。v0.1.0的独立界面属于历史实现。本项目是非官方改版适配，未获得上游作者的官方支持或背书。

## 上游第三方组件与图像署名

- QR码生成代码来自[QRCoder](https://github.com/codebude/QRCoder)，采用[MIT许可](https://github.com/codebude/QRCoder/blob/master/LICENSE.txt)。
- 上游闪光精灵图片集合来自[pokesprite](https://github.com/msikma/pokesprite)，采用[MIT许可](https://github.com/msikma/pokesprite/blob/master/LICENSE)。
- 上游《宝可梦传说：阿尔宙斯》精灵图片集合来自[National Pokédex – Icon Dex](https://www.deviantart.com/pikafan2000/art/National-Pokedex-Version-Delta-Icon-Dex-824897934)项目及其贡献者。

这些项目为上游程序提供资源或组件。水银专用预览另从用户配置的ROM读取，具体资源选择由水银适配器处理。

## 格式与文字资料

- 本版水银ROM中的读取、写入和资源加载代码，是存档及资源适配的依据。地址与解析关系记录在[存档格式文档](mercury-save-format.md)和[数据接口文档](mercury-rom-profile.md)。
- [Complete Fire Red Upgrade](https://github.com/Skeli789/Complete-Fire-Red-Upgrade)及[pret/pokefirered](https://github.com/pret/pokefirered)提供函数定位和引擎结构参考。
- [Sum-Light/azoth-wiki](https://github.com/Sum-Light/azoth-wiki)及其公开HOME页面提供格式线索和可导入的中文字符映射。程序解析导入文件中的资料，不执行其中的JavaScript。

v0.2.0未内嵌HOME完整数据库。字表在用户选择导入或下载后保存在本机，游戏数值仍来自所配置ROM。

## 发行包与游戏资源

发行包包含程序、代码许可和使用说明，不附带游戏ROM、个人存档、提取音频或水银图片集合。用户配置的`rom-cache.gba`与资料文件保存在本机数据目录。

代码许可适用于代码；游戏、美术、音乐及第三方资料的权利归各自权利人所有。Pokémon及相关名称属于其权利人。

## 贡献者须知

提交源码时，不加入个人存档、本机profile、`rom-cache.gba`或未经确认可分发的资源包。新增外部资料需记录来源、版本及相应许可。
