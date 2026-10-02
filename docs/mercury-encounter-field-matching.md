# 水银遭遇字段与 Method1 相关性诊断

报告区分普通遭遇记录字段匹配、基础 Method1 数值相关性与完整来源检查，不实现完整获取合法性，也不运行零售合法性分析。

## 状态与资料门控

- `MercuryEncounterMatchResult.FieldMatchStatus`：至少一个普通候选满足 species、MetLocation/mapsec、MetLevel 闭区间时为 `Pass`，否则为 `Unknown`。原 `Status` 仍为 `Unknown`，保留完整来源语义。
- 分析报告新增稳定 code `encounter.record-fields`。只有经过原有 ROM、版本及 SHA 门控的证据能令它通过；未导入、无有效 ROM、版本不支持、SHA 不符均为 `Unknown`。资料门控没有放宽。
- `encounter.source` 仍为 `Unknown`：PID/IV、egg 状态、槽可选性以及其他生成约束未完整核对。字段匹配通过不会令总结果变为 `Pass`。
- 候选或输入缺少地点、等级字段时不以零补齐；动态记录不能冒充普通候选。没有候选不判非法：事件、进化前物种、等级修正及动态来源的全集覆盖仍不足。
- 匹配报告说明“通过所列字段约束匹配；未核对其他生成约束”。可能性检查不要求还原某只个体的真实捕获历史；未记录历史时刻本身不是永远保持完整来源未知的理由。

等级仍保留原始 `int` 字段。只有查询等级和两端点都能无损表示为 `byte` 时才调用原生 `LevelRangeExtensions.IsLevelWithinRange`；否则继续原有整数闭区间比较，不截断、不回绕、不引入零售等级上限。这里通过的是所列字段比较，不是对原始研究记录所有字段有效性的额外认证。

## 基础 Method1 数值相关性

`rng.method1` 直接调用原生 `MethodFinder.GetLCRNGMethod1Match(pid, iv32, out seed)`，不复制随机算法，也不调用通用 `MethodFinder.Analyze`。六 IV 按 HP/Atk/Def/Spe/SpA/SpD 打包为低 30 位，不带蛋或隐藏能力位。

此项不依赖 ROM 表，在 numeric 或缺资料时也可计算；species 0 空槽仍为 `Unknown`。匹配为 `Pass`，仅表示“符合基础 Method1 数值相关性，不证明该来源必用此方法，也不代表完整合法”。报告中的 `0x` 加八位十六进制 seed 是数值候选，不是恢复出的真实捕获种子。未匹配为 `Unknown` 而非非法：CFRU 同步性格、闪光后处理或 IV 覆盖可能改变关系。`encounter.source` 仍未知；不存在其他无效项时，总结果仍为 `Unknown`。

## 礼物样例（历史证据，非阻塞条件）

自本更新起，Issue 6 **不以静态游戏脚本礼物 131/25/134 或“全礼物获取链”作为阻塞或完成条件**；相关证据保留为历史记录、不删除。主要参考转为公开 **distribution → HOME 导入/检查代码**（`https://sum-light.github.io/azoth-wiki/distribution/`）。当前 4 条公开配信含 6V、梦特与特殊球；因此**不能仅凭野生 Method1 未匹配就判非法**——现有代码未匹配仍为 `Unknown` 而非 `Invalid`，保持不变，且未读公开代码前不修改当前算法。

已有单一样例的保存证据显示：脚本 `0x08161AE0` 的 `0x79` 指令生成 species **131**、初始 MetLevel **25**；静态关联地图为 `1:53`，该地图的区域编号为 **134**。地点字段取执行时的当前地图，因此 `134` 是在其静态关联地图执行这一条件下的已证值，不是已证明全局唯一的地点常量。

本增量**不导入**该礼物候选，也不新增 gift loader 或完整遭遇模板。其最终 PID/IV 生成允许集合及相关性、egg 状态约束尚未闭合；这才是不能称完整生成约束已核对的原因，不是无法证明该个体确实领取过礼物。训练家敌方队伍也不作为玩家获取来源。

已补核的局部差异：species 131 对应 `0x09E44652` 单字节为 `00`，bit3 未置位，因此不进入该处 CreateBoxMon 完美 IV 覆盖循环。已有创建函数 dump 中，flag `0x913` 非零时额外执行 Force→Check→Force→Check→Force（`0x09D06E36/E3C/E42/E48/E4E`），该序列不在所读 CFRU 创建函数末尾；调用次数不等于有效重掷次数。

显示判闪的既有 sprite 证据为四半字 XOR `<=7`，本增量不改此阈值。已有训练家取证结论另记 Force 候选 `Random&0xF` 后经 `<=7` 接受判定；但所保存 Force 原始 dump 止于 `0x09D06AE6`，不含后续循环及判闪函数完整比较体，因此不称本轮已重核该阈值的原始指令。

## 限定验证

`MercuryEncounterFieldMatchTests` 覆盖普通候选正匹配、闭区间端点、缺字段、无候选、动态记录、无 ROM、跨版本/SHA、无导入、总状态仍未知与报告措辞。整数越界用例验证不会发生 byte 回绕，并保留原始整数比较语义。

正向分析用例通过 `MERCURY_TEST_ROM` 指向用户自有且哈希验证的水银 1.1 ROM；未配置时明确跳过。跨版本防御分支使用仅用于拒绝测试的合成内部状态，不能作为水银 1.0 ROM 验证结论；跨 SHA 同时检查公开导入器拒绝和分析器独立拒绝。测试不附带 ROM 或研究表资源。

`MercuryMethod1Tests` 无需 ROM：以原生 `ClassicEraRNG` 的固定 seed 向量和原生 Method1 匹配器作预言机，覆盖正反例、六 IV 顺序、蛋/能力高位排除、输入字节不变、空槽、缺资料及完整来源不被提升。
