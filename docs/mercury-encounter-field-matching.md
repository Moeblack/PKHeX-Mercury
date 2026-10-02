# 水银遭遇字段、配信参考与 Method1 相关性诊断

报告区分普通遭遇记录字段匹配、公开配信原始内容匹配、基础 Method1 数值相关性与未覆盖的完整来源约束，不运行零售合法性分析，也不把已覆盖检查通过称为完整合法。

## 状态与资料门控

- `MercuryEncounterMatchResult.FieldMatchStatus`：至少一个普通候选满足 species、MetLocation/mapsec、MetLevel 闭区间时为 `Pass`，否则为 `Unknown`。原 `Status` 仍为 `Unknown`，保留完整来源语义。
- 分析报告新增稳定 code `encounter.record-fields`。只有经过原有 ROM、版本及 SHA 门控的证据能令它通过；未导入、无有效 ROM、版本不支持、SHA 不符均为 `Unknown`。资料门控没有放宽。
- 普通遭遇字段命中不提升 `encounter.source`：PID/IV、egg 状态、槽可选性以及其他生成约束未完整核对。公开配信完整内容匹配可单独通过限定来源项，范围见下节；总结果仍按所有检查汇总。
- 候选或输入缺少地点、等级字段时不以零补齐；动态记录不能冒充普通候选。没有候选不判非法：事件、进化前物种、等级修正及动态来源的全集覆盖仍不足。
- 匹配报告说明“通过所列字段约束匹配；未核对其他生成约束”。可能性检查不要求还原某只个体的真实捕获历史；未记录历史时刻本身不是永远保持完整来源未知的理由。

等级仍保留原始 `int` 字段。只有查询等级和两端点都能无损表示为 `byte` 时才调用原生 `LevelRangeExtensions.IsLevelWithinRange`；否则继续原有整数闭区间比较，不截断、不回绕、不引入零售等级上限。这里通过的是所列字段比较，不是对原始研究记录所有字段有效性的额外认证。

## 基础 Method1 数值相关性

`rng.method1` 直接调用原生 `MethodFinder.GetLCRNGMethod1Match(pid, iv32, out seed)`，不复制随机算法，也不调用通用 `MethodFinder.Analyze`。六 IV 按 HP/Atk/Def/Spe/SpA/SpD 打包为低 30 位，不带蛋或隐藏能力位。

此项不依赖 ROM 表，在 numeric 或缺资料时也可计算；species 0 空槽仍为 `Unknown`。匹配为 `Pass`，仅表示“符合基础 Method1 数值相关性，不证明该来源必用此方法，也不代表完整合法”。报告中的 `0x` 加八位十六进制 seed 是数值候选，不是恢复出的真实捕获种子。未匹配为 `Unknown` 而非非法：CFRU 同步性格、闪光后处理或 IV 覆盖可能改变关系。该诊断本身不提升 `encounter.source`，也不否定已发布的配信样例。

## 公开配信原始内容参考

`MercuryDistributionCatalog` 固定引用 [azoth-wiki 配信文档，commit `274fcd4dbd5ad7c06af9705c5f76796d853a275c`](https://github.com/Sum-Light/azoth-wiki/blob/274fcd4dbd5ad7c06af9705c5f76796d853a275c/docs/distribution/index.md) 的四条 PMH1 记录：梦想成为假面骑士的芭瓢虫、竹兰的圆陆鲨、壮壮妈、Lv.5 新叶喵。每项保留标签、固定版本来源 URL 和 payload；只用 BCL Base64URL 解码，原始 58 字节私有保存，不提供可变数组。

匹配直接比较 `MercuryPokemon.ToBoxBytes()` 的**完整 58 字节**，不重写现有盒/队伍归一化，不猜可变字段。完全相同时，`distribution.reference` 为 `Pass`，`encounter.source` 可为 `Pass`，其含义仅是“公开配信原始内容来源匹配”。这不证明真实领取或持有人身份，不是密码学认证，也不是完整游戏合法性。无需已加载野生 ROM/遭遇表即可比较公开参考内容。

无匹配时两项不因此判非法：`distribution.reference` 为 `Unknown`；升级、改名、进化或任意字节变化均可能不再与原始发布内容相同。本增量不追踪这些变体。普通 `encounter.record-fields`、范围、学习表及 `rng.method1` 继续独立报告；配信样例没有 Method1 匹配也不判非法。汇总规则不变：其他已覆盖字段的 `Invalid` 仍优先；存在任何 `Unknown` 仍汇总为未知，不强制总结果通过。

已读的公开 HOME 仅检查 PMH1 前缀、58 字节长度、非零物种、数据库覆盖与盒容量，随后保留 raw 字节；它不是宝可梦生成器或完整合法性引擎。目录只收录上述四个固定参考，不把“HOME 能接受任意格式有效记录”等同“所有记录都合法”，也不新增 PMH1 导入 UI、生成器或完整遭遇模板。

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

`MercuryDistributionReferenceTests` 固定使用四条公开 payload，不访问网络或 ROM，不写用户存档。覆盖 58 字节解析与物种/PID/OT/经验/性格/球/招式/6V/梦特等元数据、四条 exact-match、PID/IV/OT 单字节变化及经验变化后的 `Unknown`、现有 box/party 归一化、无 ROM 时来源限定匹配但汇总未知、输入不变与参考字节不对外暴露。固定记录的物种/招式/持有物均符合现有水银索引形状；未因样例放宽任何范围限制。
