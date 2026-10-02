# 并行修复集成记录（integration/mercury-parallel-fixes）

本文件记录把已完成的工作分支集成到同一条集成分支的过程。公开内容只描述相对源码与行为，不含本机路径；带本机绝对路径的详细验证日志保存在本机 `artifacts/` 集成记录中，不进入仓库。

## 基线

- 基线提交：`96cf5a744e0cc13a6be9c03b7394f620bfaf9f44`（v0.2.0）。
- 集成分支：`integration/mercury-parallel-fixes`，worktree 目录名 `integration`，从上述基线创建。
- 未包含仍在运行的分支：Issue 1、Issue 8、Issue 2+11。

## Cherry-pick 映射（按顺序，全部无冲突）

| 顺序 | 来源分支 | 源提交 | 集成后提交 | 主题 |
|---|---|---|---|---|
| 1 | fix/mercury-issue-04-docs | f00a34d1c13f3ecc866209057f1607262fcd03a6 | 83becec37 | docs: 按消费者反馈修订水银文档并分层开发资料 |
| 2 | fix/mercury-issue-10-recognition | 3f08da270 | f5eabdd1a | Mercury: explicit save recognition model (Issue 10) |
| 3 | fix/mercury-issue-06-legality | ac8aab3c0 | 21a05c048 | Add evidence-scoped Mercury legality checks and unknown reports |
| 4 | fix/mercury-issue-07-identifiers | ff7d6457d20107a5f163bb6cb2c0c0eff9494caa | 674597e56 | Preserve Mercury identifier evidence and unknown numeric values |
| 5 | fix/mercury-issue-02-11-forms | 72c61eaff | 4d1ce1cc5 | Expose explicit Mercury sprite frame and palette-page selection |
| 6 | fix/mercury-issue-07-identifiers | 772f737113e806b20c64751ace267c7e9215e44d（parent ff7d6457） | 251bfcc1e | Fix Mercury numeric-only strings factory empty ability slot |
| 7 | mercury（Issue 1 核心单独提交） | 3cb8d17d06bf688638d8d669a22b8c08df2a9d9f | 516bf4ee8 | Fix Mercury Unown forms and PID constraint preservation |
| 8 | fix/mercury-issue-08-type | 203c63ee87e73cca6e1e35d84af03a5deae7fcdd | ab442dd1a | Add Mercury type override field and native editor control |
| 9 | fix/mercury-issue-08-type | 0cfb69c40122b7a2ed4b9cbae06afcb38f8991ea | 3f9044f12 | Preserve unknown boxed Mercury type bits through the PKM bridge |
| 10 | mercury（Issue 1 UI） | d6129eb97c4d0f0bccb58ee2ee7f0d1c2c5b9e0f | 727e59ca7 | Synchronize Mercury Unown PID fields before native preview |
| 11 | mercury（Issue 1 UI） | 9372255e7 | 410144774 | Disambiguate duplicate Mercury species labels without changing IDs |
| 12 | fix/mercury-issue-05-provisioning | cb29075b6b2094f8a969901fed041b3f9be8d16f | 9546efb96 | fix(mercury): provision ROM profile with automatic charmap setup |
| 13 | fix/mercury-issue-02-mechanisms | 0f7a9a6c44f051f20364ac882b93fc8e1e3ff0df | 8d5e17c08 | Add evidence-scoped Mercury form mechanism catalog |
| 14 | fix/mercury-issue-02-mechanisms | 424073f11f79f0c726b1b9716abcd2ff60430220 | 0c5152d9a | Explain Mercury form mechanisms in native species tooltip |
| 15 | fix/mercury-issue-11-forms | 4555bb71e | c1707657d | Add read-only Mercury sprite resource preview from native image menu |
| 16 | fix/mercury-issue-06-legality | dc39b75d6 | 8cadd55be | Add Mercury encounter evidence parsing and unknown candidate matching |
| 17 | fix/mercury-issue-06-legality | aea37dcbd | 6053ea852 | Wire Mercury encounter evidence import into native legality reports |
| 18 | fix/mercury-issue-11-forms | 2f91811ba | 11d4a5187 | Allow explicit Mercury runtime resource selection in sprite preview |
| 19 | fix/mercury-issue-09-versions | 7b73a9e4f | 8c8a84dcc | Recognize exact Mercury ROM builds and resolve growth tables from consumer literal |
| 20 | fix/mercury-issue-09-versions | db67d82e7 | fd311a7ec | Load exact Mercury 1.0 data and bind profiles to their declared ROM version |
| 21 | fix/mercury-issue-02-mechanisms | 62201829faa025e549ed7b1278ee386382509175 | be7a9e981 | Expose proven conditional Mercury species transitions without form setters |
| 22 | fix/mercury-issue-09-host | d6ab8daf99e21bec6ba9fd9dec5a4aa3e0c5180d | 29e7777f3 | Authorize Mercury host editing and checks by verified ROM version |
| 23 | fix/mercury-issue-07-identifiers | 216f979d27b05561f31c96aacee1c3c005e88272 | a3fd4484c | Expose Mercury identifier observations without rewriting stored IDs |
| 24 | fix/mercury-issue-05-pack-runtime | 10dbc4444 | fc6c86173 | feat(mercury): export verified local data packs without full ROM |
| 25 | fix/mercury-issue-05-pack-runtime | 717ddc47c | 145174864 | feat(mercury): read independent sprite pools from local data packs |
| 26 | fix/mercury-issue-05-pack-runtime | 4aff0327c4868f35ea2053f69b99ff330746b034 | bdfb6d2cf | feat(mercury): load portable pack resources without ROM evidence promotion |
| 27 | fix/mercury-issue-05-pack-runtime | d7702cbac5f3cb25376ac158aa0108f12dc6dde3 | 6f0a22a95 | feat(mercury): install local default packs with backup rollback |
| 28 | fix/mercury-issue-06-shared-learnset | 786732724a7c47828df68b31e237023032677aaa | ec1836d90 | Cache shared native learnsets for Mercury species snapshots |
| 29 | fix/mercury-issue-06-fieldmatch | 4b5b8f1036120b48b9408faea3d4a30e13d4ee79 | 3c625819e | Separate Mercury encounter field matches from source coverage |

集成分支 pick HEAD（本次记录时）：`3c625819e`。二十九次 cherry-pick 均已落地：冲突发生于第 7、15、23、26 次并按规则解决；其余无冲突或仅语言文件纯新增自动合并。

第 29 次增量说明（Issue 6 仍部分）：字段匹配仅对已接受资料的普通三字段命中判 `Pass`，`source` 仍为 `Unknown`、聚合仍为 `Unknown`；新增 `docs/mercury-encounter-field-matching.md` 仅记本增量范围，**不是 gift 完整规则**；gift 131/25/134 尚未导入。验证：solution 构建 0 警告 0 错误；`MercuryEncounterFieldMatchTests` **26/26 通过、0 跳过**（`MERCURY_TEST_ROM` 设为已知 v1.1）。未跑全库、未重跑 installer。

第 28 次合并后接口同步（单独提交 `2a2fa246f9`）：提交把字段改为 `IReadOnlyList<MercurySpecies> _species`，但主构造函数第 3 参仍为 `List<MercurySpecies>`，与 `LoadPack` 传入的只读快照不兼容（CS1503）。按 Main 批准将其同步为 `IReadOnlyList<MercurySpecies>`，保留既有 `Array.AsReadOnly(species.ToArray())` 快照，不在 `LoadPack` 额外 `ToList` 复制；属接口同步，无行为变化。

验证：solution 构建 0 警告 0 错误；`PKHeX.Core.Tests.Mercury.MercurySharedLearnsetTests` 过滤运行 **13/13 通过、0 跳过**（`MERCURY_TEST_ROM` 设为已知 v1.1）；隔离 Runtime 联动重跑 **68/68**（原 65 + 新增 3 条 pack 学习表缓存非空/重复查询同实例/来源 gate 仍 Unknown）。Issue 6 仍为部分实现，学习表缓存复用不构成“完整获取合法性”。

第 26 次冲突解决（Main 方案）：`GetLocationIdentifiers(string language = "zh")` 保留 language 参数，先取 `MercuryIdentifierCatalog.CreateLocations(...)`（保留 EvidenceNote/ObservedGameReadValue），`_pack` 非空时按 `location.Id` 覆写 `State`/`Name`；前置已核实 `MercuryDataPackValidator.RequireIds(locations, 256)` 要求 0..255 有序唯一，下标安全。

Pack runtime/安装验证（隔离副本重定向到本集成分支构建）：Runtime **65/65**（原 47 + 新增 18 条 5E/FD/FE EvidenceNote zh/en 断言）、Installer **20/20** 通过；0 网络、不触碰用户 LocalAppData、不改动源测试与用户数据。本机日志与隔离方式见本机集成报告。

第 23 次冲突解决：`PKMEditor.SetPKMFormatMode` 中 `ConfigureMercuryTypes(pk)`（Issue 8）与 `SetMercuryIdentifierTipFormat(pk)`（Issue 7）为相互独立的调用行，保留两者、不删任一侧；`InitializeMercuryIdentifierTips()`（构造）与 `RefreshMercuryIdentifierTips()`（`LoadFieldsFromPKM` 完成时）自动合入，未替换既有事件；未定义永久 Form setter。

Issue 9 已支持精确 v1.0/v1.1 并按各版本实际数据读取（160 限定 checks + 6 语言 checks 通过）；但存档布局本身无法唯一辨版本，1.1 遭遇证据不套 1.0，未游戏实测。Issue 7 已证来源备注（读 3 写 4）、球 27 运行态、5E 动态名与 ToolTip，无损保存；不标未知机制全部闭合。均未发布。

Issue 6：已能手动导入真实遭遇 JSON 并显示普通候选及独立动态摘要（`MercuryEncounterEvidence*.cs`、`MercuryEncounterMatcher.cs`、`MercuryEncounterContext.cs`）；不宣称完整获取合法性。Issue 11：新增 338 `null`/`false`/`true` 运行态只读预览，不写存档、不冒充从存档得知状态。二者均未发布。

第 15 次语言文件冲突规则：`lang_en.txt` / `lang_zh-Hans.txt` 的 Issue 5 setup 键与 Issue 11 preview 键互不重叠、均为新增，故每个文件按 ours 全部键后接 theirs 全部键拼接，保留两侧、不删任何一侧。后续同类仅当确认键名集合不重叠且均纯新增时按此处理；出现相同键不同值必须回 Main，不盲目 both。

Issue 2 状态：已证 PID/性别/运行态机制已分类并接入原生提示（`MercuryFormCatalog.cs` / `MercuryFormMechanism.cs` / `EditMercuryForms.cs`，真实 ROM 2406 断言、原生提示 helper 与 65 控件断言通过）；其它持久形态映射待证。Issue 11 状态：多帧/多页只读预览已实现（`MercurySpritePreview.cs` / `Main.MercurySpritePreview.cs`，47 限定断言通过），默认 0/0 由真实消费者证实；运行时动画语义未全面闭合，不称自动动画适配。二者均未发布。

Issue 5 本次只并入“一步 ROM 配置 + 自动字表 + 无 profile 离线缓存”（新增 `MercuryDataSetup.cs`）；**未实现无 ROM 默认部署，仍标部分实现**，不能以 Issue 1/8 的验证结果证明新增 Issue 5 行为。保留了 `MercuryIntegration` 的空能力修复（`Math.Max(1, data.AbilityNames.Count)`）与后续 Issue 11 独立语言键的接入点（`GameStrings.CreateMercury` 签名未动）。

Issue 1、Issue 8 已通过限定联动验证：分别 2314 项与 4000 断言，验证 HEAD `6ead5dee`（含 201/434 同名选择与类型清零/保留联动）。此处仅记录已核对结果，未据此声称其它行为。

Issue 1 UI 两次 pick 包含 Main 已逐项审定的方案：`UpdateForm` 显式 `Update_ID`、`Update_ID` 先同步后预览、只在水银 `ComboItem` 显示层按 `OrdinalIgnoreCase` 对重复名追加 `Value` 后缀。保留了 Issue 8 的 `ConfigureMercuryTypes`/`LoadMercuryTypes`/`SaveMercuryTypes`（`EditMercuryTypes.cs`）及桥接侧车。

Issue 1 状态为“已实现，2314 项限定验证通过；集成控件联动复核待完成，未发布”，未声称集成联动已通过。

## 会话分工（一对一，完成项保持完成状态，不重复修改）

| Issue | 负责会话 |
|---|---|
| 1 形态核心 | core |
| 2 形态机制 | mechanisms |
| 3 资源 | resources |
| 4 文档兼集成 | docs |
| 5 资料部署 | provisioning |
| 6 合法性 | legality |
| 7 标识符 | identifiers |
| 8 类型覆盖 | type |
| 9 版本 | versions |
| 10 数据契约 | data-contracts |
| 11 形态 | forms |

第 7 次冲突解决要点：`MercurySpriteLoader` 保留新 selection/metadata 重载与 XML，旧重载改为转发并接受 `paletteIndex`；调色板读取保留 `paletteResourceIndex`（范围校验已存在）；`MercuryGameData.GetSpriteRgba` 用 `resolvedPaletteIndex = paletteIndex ?? (species == 201 ? 201 : index)`，Unown 默认强制调色板资源 201、调用者显式 paletteIndex 优先。自动合入的 `GetSpriteIndex` Unown 201 映射与 `MercuryPokemon`/`MercuryPKM` 的 PID/约束代码保留。Issue 1 的 UI 尚未完成，不标为完成。

NumericOnly 字符串构造已修：`PKHeX.WinForms/Mercury/MercuryIntegration.cs` 的 abilities 数组改为 `Math.Max(1, data.AbilityNames.Count)`，当名称为 0 条时在 index 0 写入数字占位 `"0"`（原生 `GameStrings` 会清洗 index 0）。该提交来自分支 `fix/mercury-issue-07-identifiers` 的最终追加提交，其父为 ff7d6457；不使用曾出现的 c4d02ea67。**仅修字符串构造**，不宣称“无 ROM 完整编辑”已实现；真实失败与修后 4 组、72 组合×2 的字节保真验证日志保留在该工作分支验证记录中。

Issue 11 本次只完成帧/页 API：`MercurySpriteSelection(FrameIndex, PalettePage)` 独立选择、`MercurySpriteMetadata` 报告完整帧/页数与尾部字节。**不包含 GUI，不证明动画帧/页配对**；Issue 2 仍缺持久 form 分组。已知资源形态：一个精灵的完整解压图块为 **4352 字节 = 2 个完整帧（各 2048 字节）+ 256 字节尾部**；尾部字节作为尾部保留、不当作额外帧。本提交不用来宣称 Issue 11 全部完成。

## 各 pick 引入的源码

- Issue 10：新增 `PKHeX.Mercury.Core/Native/MercurySaveRecognition.cs`；改动存档识别路径。
- Issue 6：新增 `PKHeX.Mercury.Core/Legality/MercuryLegalityAnalysis.cs`、`PKHeX.Mercury.Core/Legality/MercuryLegalityResult.cs`。
- Issue 7：新增 `PKHeX.Mercury.Core/Data/MercuryIdentifier.cs`、`PKHeX.Mercury.Core/Data/MercuryIdentifierCatalog.cs`。
- Issue 4：仅文档（README、notices、rom-profile、save-format、新增处置表与开发跟踪清单）。

## 构建核对

- SDK：固定使用本机 .NET SDK `10.0.401`。
- 命令：`dotnet build PKHeX.Mercury.slnx -c Release -m:1`。
- 结果：**已成功生成，0 警告，0 错误**（用时约 8.3 秒），产出 `PKHeX.dll`、`PKHeX.Mercury.Core.dll` 等。
- 本次只做一次构建核对已合并代码，未运行额外测试或应用。
- 集成 Issue 1 核心提交（Unown/PID，含冲突解决）后再次构建：**已成功生成，0 警告，0 错误**（约 7.95 秒）。构建日志保存在本机 `artifacts/integration-build-core.log`。
- 集成 Issue 8 两次提交后构建：**已成功生成，0 警告，0 错误**（约 10.72 秒）。构建日志保存在本机 `artifacts/integration-build-issue8.log`。
- 集成 Issue 1 UI 两次提交后构建：**已成功生成，0 警告，0 错误**（约 6.97 秒）。构建日志保存在本机 `artifacts/integration-build-issue1ui.log`。
- 集成 Issue 5 提交后构建（覆盖 Issue 5 合并）：**已成功生成，0 警告，0 错误**（约 7.13 秒）。构建日志保存在本机 `artifacts/integration-build-issue5.log`。
- 集成 Issue 2 + Issue 11 提交后构建（覆盖前次 Issue 2 与本次 Issue 11）：**已成功生成，0 警告，0 错误**（约 7.35 秒）。构建日志保存在本机 `artifacts/integration-build-issue2-11.log`。
- 集成 Issue 6 两次 + Issue 11 运行态提交后构建：**已成功生成，0 警告，0 错误**（约 10.84 秒）。构建日志保存在本机 `artifacts/integration-build-issue6-11b.log`。
- 集成 host gate 与 Issue 7 identifier 提交后构建：**已成功生成，0 警告，0 错误**（约 7.07 秒）。构建日志保存在本机 `artifacts/integration-build-gate-ident.log`。
- 集成 pack 四提交后构建：**已成功生成，0 警告，0 错误**（约 7.59 秒）。构建日志保存在本机 `artifacts/integration-build-pack4.log`。
- 集成共享学习表提交后构建：**已成功生成，0 警告，0 错误**（约 6.88 秒）。日志 `artifacts/integration-build-shared-learnset.log`。
- 集成字段匹配提交后构建：**已成功生成，0 警告，0 错误**（约 7.43 秒）。日志 `artifacts/integration-build-field-match.log`。

## Issue 3（729 号道具图像）：阻塞结论

已核对用户两个真实 ROM 与真实消费者（含跳板校正）及公开 HOME，结论一致：

- 两个 ROM 的 729 表项相同：tile 指向 `09100840`、palette 指向 `09100930`，两者都落在音频 sample、首字节 `B6`；真实送 BIOS 前没有替换。
- 公开 HOME 的 `itemIcons` 749 项中唯一缺键为 729，未猜测图片 URL。

因此：现有输入无法恢复有效图像，Issue 3 标记为**阻塞**（非“已修复”），等待原作者素材或有效资源定位依据。名称与数量仍可编辑，保留条目，不加占位图、不借用 730 图。详细证据存于本机 `artifacts/`（公开文档不含本机路径）。不再反复调查同一坏表路径。

## 事实补记（Issue 5/9，未完成）

- Issue 9：用户已提供实际 v1.0 ROM（详见本机记录），读取与版本布局对照进行中；不以放宽哈希代替真实输入。
- Issue 5：公开资料入口无明确再分发许可，也没有可直接部署的资源包；现状仍需用户自备 ROM，不标完成。
- 详细本机报告保存在本机 `artifacts/`（公开文档不含本机路径）。

## 说明

- Issue 7 的 NumericOnly 修补仍有后续提交，待 Main 通知后再 pick；本次未包含。
- 本集成分支未提交（push）、未发布、未替换任何用户程序。
