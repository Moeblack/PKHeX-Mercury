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

集成分支 HEAD（本次记录时）：`4d1ce1cc5`。五次 cherry-pick 均干净落地，无冲突、无 ours/theirs 选择、未重写代码（第 5 次对 `MercuryGameData.cs` 自动合并成功）。

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

## 说明

- Issue 7 的 NumericOnly 修补仍有后续提交，待 Main 通知后再 pick；本次未包含。
- 本集成分支未提交（push）、未发布、未替换任何用户程序。
