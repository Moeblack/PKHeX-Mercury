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
| 7 | mercury（Issue 1 核心单独提交，UI 未完成） | 3cb8d17d06bf688638d8d669a22b8c08df2a9d9f | 516bf4ee8 | Fix Mercury Unown forms and PID constraint preservation |
| 8 | fix/mercury-issue-08-type | 203c63ee87e73cca6e1e35d84af03a5deae7fcdd | ab442dd1a | Add Mercury type override field and native editor control |
| 9 | fix/mercury-issue-08-type | 0cfb69c40122b7a2ed4b9cbae06afcb38f8991ea | 3f9044f12 | Preserve unknown boxed Mercury type bits through the PKM bridge |

集成分支 HEAD（本次记录时）：`3f9044f12`。九次 cherry-pick 均已落地（第 7 次冲突按 Main 单逐块解决；第 8、9 次自动合并），未选整文件 ours/theirs，未重写其它代码。

Issue 8 两次 pick 只涉及另一桥接/`TypeOverride` 与 UI 挂钩（新增 `EditMercuryTypes.cs`），保留了已合 Issue 1 的 `SetPersonality` 新参数、Unown form 与 PID 约束机制。

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

## 事实补记（Issue 5/9，未完成）

- Issue 9：用户已提供实际 v1.0 ROM（详见本机记录），读取与版本布局对照进行中；不以放宽哈希代替真实输入。
- Issue 5：公开资料入口无明确再分发许可，也没有可直接部署的资源包；现状仍需用户自备 ROM，不标完成。
- 详细本机报告保存在本机 `artifacts/`（公开文档不含本机路径）。

## 说明

- Issue 7 的 NumericOnly 修补仍有后续提交，待 Main 通知后再 pick；本次未包含。
- 本集成分支未提交（push）、未发布、未替换任何用户程序。
