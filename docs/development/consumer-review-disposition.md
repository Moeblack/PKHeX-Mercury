# 消费者文档反馈处置表（DOC-001 … DOC-035）

来源报告：[consumer-review.json](consumer-review.json)（只读，本表不改动它）。本表逐条记录处置结论、理由与落地文件，状态为：**采纳** / **部分采纳** / **不采纳** / **需Main事实确认**。

读者定位前提：`README.md` 面向下载使用者；本目录及 `docs/mercury-rom-profile.md`、`docs/mercury-save-format.md` 面向开发者；`docs/mercury-issues.md` 为内部跟踪，由 Main 独占维护，本执行线不改动它。`docs/mercury-notices.md` 为来源与许可，保留上游署名不可删除。

## 事实纠正（先于逐条处置）

- **DOC-001**：README 已给精确资产名与体积。但 README 中出现的 SHA-256 是**水银 1.1 输入 ROM 的哈希**，不是 ZIP 下载包的哈希，两者不可混同；下载包校验入口为发行标签下的 `SHA256SUMS.txt`（与包同目录）。采用“精确下载链接”建议，但不声称旧版没写版本号。
- **DOC-027**：本地 `artifacts/` 不是公开仓库资产，历史发行不删除、不改写来迎合反馈。
- **DOC-014**：“不拦任何修改、导出安全”不是产品事实。正确表述为“编辑和导出可用，获取来源/配招不判断，字段范围与校验和仍生效”。
- **DOC-009**：GUI 默认缓存目录由 `MercuryIntegration.ProfileDirectory` 固定且无用户编辑入口；库 API `SaveProfile(directory)` 由调用者指定目录。两者一致（见 DOC-009 行）。
- **DOC-032**：save-format 中的零基箱号 19–21 对应用户界面第 20–22 箱；只能说缺真实样本，不推断实际有/无故障。

## 逐条处置

| ID | 状态 | 处置与理由 | 落地位置 |
|---|---|---|---|
| DOC-001 | 部分采纳 | README 顶部已给精确资产名 `PKHeX-Mercury-v0.2.0-win-x64.zip`、标签 `mercury-v0.2.0`、体积。README 正文的 SHA-256 是输入 ROM 的哈希，不是下载包哈希；下载包完整性核对入口为发行标签下的 `SHA256SUMS.txt`。不声称 README 已给 ZIP 哈希，也不声称旧版未写版本号，也不声明“artifacts 仅供开发”（本地 artifacts 非公开资产）。 | `README.md`（Main 已改） |
| DOC-002 | 采纳 | README 只保留发行包内 `PKHeX.exe` 为唯一用户入口，内部修正版 exe 路径已移除。Main 已决定：历史 v0.2.0 发行包已包含 box/inventory/ui 三项修复。 | `README.md`（Main 已改） |
| DOC-003 | 采纳 | 带日期的开发过程日志移出 README，改由开发者资料与发行说明承载。 | `README.md`（Main 已改） |
| DOC-004 | 采纳 | Main 已决定：发布版 v0.2.0 的未知图腾未适配，工作区刚改、尚未发布。README“已知问题”按发布状态保留“未知图腾及部分特殊形态未适配”，不把工作区改动当作已发布能力。 | `README.md`（Main 已改）、`docs/mercury-issues.md`（Main） |
| DOC-005 | 采纳 | Main 已决定：开发参考保留原路径与包内位置，不搬文件；仅通过标题与导航分层。README 的“开发者资料”区分用户说明与逆向笔记，rom-profile、save-format 明确标注 developer-facing。 | `README.md`（Main 已改）、`docs/mercury-rom-profile.md`、`docs/mercury-save-format.md` |
| DOC-006 | 采纳 | README 不再附带上游英文支持的格式清单（.dsv/.dat/.gci/.bin/.pk* 等）。 | `README.md`（Main 已改） |
| DOC-007 | 采纳 | README“准备存档”说明游戏内存档来源、常见模拟器 `.srm`/`.sav` 位置，以及即时存档不可用。 | `README.md`（Main 已改） |
| DOC-008 | 采纳 | 保留 SHA-256 但改为可选核对（PowerShell `Get-FileHash`），并说明本工具不提供 ROM；不强迫用户先算哈希才能使用。 | `README.md`（Main 已改） |
| DOC-009 | 采纳 | Main 已决定：UI 层 `MercuryIntegration.ProfileDirectory` 固定为 `%LOCALAPPDATA%\PKHeX-Mercury\profile`，当前无用户编辑入口；库 API `SaveProfile(directory)` 由调用者指定目录。两者一致，非矛盾，不留“是否可改”悬案。 | `docs/mercury-rom-profile.md` §5 |
| DOC-010 | 采纳 | “不要上传到仓库”的贡献者规范移出用户使用段，README 只说明资料仅存本机；贡献者须知留在 notices。 | `README.md`（Main 已改）、`docs/mercury-notices.md` |
| DOC-011 | 采纳 | 首次使用首步只讲必须动作（选 ROM）；`profile`、研究目录作为进阶/开发用途单独说明并白话化。 | `README.md`（Main 已改） |
| DOC-012 | 采纳 | 删除“不再使用应用到当前格”的 v0.1.0 迁移语，改为正面描述“查看→修改→右键目标格设置→导出”。 | `README.md`（Main 已改） |
| DOC-013 | 采纳 | 统一单体导出为 `.mercurypkm`，`.m3box` 58 字节记录作为可导入的兼容格式，并说明与原版 `.pk3` 不同。 | `README.md`（Main 已改） |
| DOC-014 | 部分采纳 | 按事实纠正措辞：编辑和导出可用，程序不判断获取来源/配招，字段范围与校验和仍生效。不采纳“不拦任何修改/导出安全保证”。 | `README.md`（Main 已改） |
| DOC-015 | 采纳 | README 给出格式选择判据（水银游戏保存选“宝可梦水银”，未改版原版选对应原版，来源不明先确认）。 | `README.md`（Main 已改） |
| DOC-016 | 采纳 | README“已知问题”标注图像仅首帧、首调色板页预览，多帧/特殊状态图未完成。 | `README.md`（Main 已改） |
| DOC-017 | 采纳 | 移除 README 后半段的上游英文正文，改为一行指向上游仓库。 | `README.md`（Main 已改） |
| DOC-018 | 采纳 | 删除 3DS 存档管理器（Checkpoint/JKSM 等）段落，改用 GBA 游戏内存档说明。 | `README.md`（Main 已改） |
| DOC-019 | 采纳 | notices 更新为当前架构：上游 `PKHeX.WinForms` 主窗口 + `PKHeX.Mercury.Core` 适配层经 `PKHeX.WinForms/Mercury` 接入，旧独立界面标为历史。 | `docs/mercury-notices.md` |
| DOC-020 | 采纳 | notices 明确 v0.2.0 链接上游绘图项目 `PKHeX.Drawing`、`PKHeX.Drawing.PokeSprite`、`PKHeX.Drawing.Misc`，消除“不引用绘图项目”的旧表述。 | `docs/mercury-notices.md` |
| DOC-021 | 采纳 | notices 已提供中文说明，第三方许可条文以原始链接指向上游（QRCoder/pokesprite/Icon Dex 等），保留原始归属。 | `docs/mercury-notices.md` |
| DOC-022 | 采纳 | rom-profile 首段标注 developer-facing，说明其服务 `PKHeX.Mercury.Core` 与 WinForms 适配层，不作为用户操作步骤入口。 | `docs/mercury-rom-profile.md` |
| DOC-023 | 采纳 | Main 已决定：save-format 保留原路径与包内位置，不搬文件；仅标题/导航分层，首段标注 developer-facing 并给出用户层结论（读 128 KiB 存档含 RTC 尾部、自动校验，无需手工处理）。 | `docs/mercury-save-format.md` |
| DOC-024 | 采纳 | 已完成：`docs/mercury-issues.md` 标题加“（开发跟踪）”，保留在 `docs/` 位置；README 仅以“开发者资料”引用，不作为用户文档。 | `docs/mercury-issues.md`、`README.md`（Main 已改） |
| DOC-025 | 采纳 | 已完成：本机研究绝对入口原文保存到 `artifacts/document-evidence/local-research-paths.md`；公开的 issue 清单改写为“外部研究资料（不随仓库分发）：README_解包说明.md、ROM解包路径与阻塞复盘.md、Main与Task协作流程.md”，不构造仓库内假相对链接。 | `docs/mercury-issues.md`、`artifacts/document-evidence/local-research-paths.md` |
| DOC-026 | 采纳 | 已用 Python `zipfile` 只读发行包内 `build-info.json` 并另存为 `artifacts/document-evidence/release-build-info.json`，Main 已读取核对：`release` v0.2.0、`tag` mercury-v0.2.0、`commit` 96cf5a744、`entrypoint` PKHeX.exe。README 也直接写“窗口标题保留上游版本号 20260826；本包水银发行版本 v0.2.0”，不让用户去查 JSON。 | `README.md`（Main 已改）、`artifacts/document-evidence/release-build-info.json` |
| DOC-027 | 不采纳 | 本地 artifacts 不是公开仓库资产；不删除或改写 v0.1.0 历史发行来迎合反馈。历史与当前发行的关系由发行说明维护。 | 无（理由记录） |
| DOC-028 | 采纳 | 防御性措辞（“不宣称/不代表/不等于/未运行测试”）改为“已知未完成清单”：说明缺什么、对用户的影响、是否影响编辑读写。 | `README.md`（Main 已改） |
| DOC-029 | 采纳 | 删除 README 中“不编写或执行测试”的内部开发约束。 | `README.md`（Main 已改） |
| DOC-030 | 采纳 | 移除 README 指向作者本机研究目录与内部账本条目名的死链/黑话；证据索引归开发者文档。 | `README.md`（Main 已改） |
| DOC-031 | 采纳 | 用户层不再写“58 字节压缩记录/扇区签名/section 集合/计数器”；实现细节下沉 save-format，用户层只说明独立单体格式且自动处理。 | `README.md`（Main 已改）、`docs/mercury-save-format.md` |
| DOC-032 | 部分采纳 | Main 已决定：只说明“缺真实样本”，不推断实际有/无故障。save-format 已注明零基箱号 19–21 等于用户第 20–22 箱并保留“指令层已证明、缺真实样本”的限制；README“已知问题”写第 20–22 箱缺少非空真实样本。 | `docs/mercury-save-format.md`、`README.md`（Main 已改） |
| DOC-033 | 采纳 | README“已知问题”列 729 号道具图像缺失，并说明名称与数量仍可编辑。 | `README.md`（Main 已改） |
| DOC-034 | 部分采纳 | README 开篇明确需自备指定版本 ROM 与存档，不以“开箱即用”表述；issue #5（普通用户仍须自备 ROM/字表）由 Main 跟踪，不作为已完成步骤。 | `README.md`（Main 已改）、`docs/mercury-issues.md`（Main） |
| DOC-035 | 采纳 | “must not claim/NotSupportedException”等实现约束留在 rom-profile（已标注 developer-facing），不作为用户文档正文。 | `docs/mercury-rom-profile.md` |

## 待 Main 处理的项

无。DOC-024、DOC-025 已在本轮完成：`docs/mercury-issues.md` 标题标注“（开发跟踪）”，本机研究入口改写为“外部研究资料（不随仓库分发）”，绝对路径原文留存于 `artifacts/document-evidence/local-research-paths.md`。

## 覆盖统计

- 总计覆盖：**35/35**（DOC-001 … DOC-035）。
- 采纳：30；部分采纳：4（DOC-001、014、032、034）；不采纳：1（DOC-027）。
- 待 Main 处理：0。
