# 7.2.0.7 复盘：养成计算器同步 / 树脂估计 / 元数据品质

> 这一版看起来是「网络问题」，实际三个故障全部是**契约字段与枚举映射**级别的错误。
> 记录在这里，避免下次再从「重试、换网络、换设备」开始排查。

---

## 1. 从养成计算器同步：同步成功，但背包全是 0

**现象**：点「从养成计算器同步」提示成功，材料清单里每一项的持有量仍然是 0。

**根因（两层，叠在一起）**

1. `AvatarPromotionDelta.FromUserSync` 在 `CreateFromAvatar` 里被显式设成 `false`，
   属性上又挂着 `[JsonIgnore(Condition = WhenWritingDefault)]`。
   `bool` 的默认值就是 `false`，于是 `from_user_sync` **根本不会出现在请求体里**。
   服务端看不到这个字段 → 按纯本地计算处理 → 返回的 `overall_consume.num` 与
   `lack_num` 相等 → `num - lack_num = 0`。
2. `avatar_level_current` / `avatar_level_target` / `element_attr_id` 等也带
   `WhenWritingDefault`，值为 0 的字段被整段吞掉，请求体形状与网页端不一致。

**修复**：`FromUserSync` 默认 `true` 且始终序列化；去掉这批 `WhenWritingDefault`，
让字段恒定出现（与 Snap.Hutao.Remastered 的行为对齐）。

---

## 2. `-500001 | [BatchComputeAsync] 网络出小差了`

**先排除网络**：`-500001` 是服务端的业务码，不是本地合成的。
`Response.publicFailure = 0x26F19335`，`DefaultIfNull` 只在「非 0 且非 publicFailure」
时补 `[callerName]` 前缀；`SendAsync` 会先 `EnsureSuccessStatusCode()`，非 2xx 根本
进不到 JSON 解析。**能拿到 `-500001` 就说明 HTTP 200、JSON 已经解析成功。**

**根因**：旅行者 `10000005` 的 `skill_list` 用的是 `SkillDepot.Skills` 里的**基础形态**
分组 id（`830` / `832`），`CompositeSkillsNoInherents` 展开成 3 条；养成计算器只认
各元素形态的分组 id，于是整批 `batch_compute` 直接失败。

**修复**（`CalculateClient.ComputeChunkWithRetryAsync`）

- 失败后**逐层递归二分**，而不是只拆一层（服务端上限可能低到几条，拆一层仍会失败）；
- 单条仍然失败时，`CloneWithoutSkills` 去掉 `skill_list` 再试一次：
  角色本体、武器、元素宝石照样算得出来，只少天赋书，**总比整次同步失败强**。

**证据**：修复后探针日志 `overall=516`、`writing 516 inventory items`。

---

## 3. 批次大小 30 → 50：这不是「多发点请求」的问题

`overall_consume` 的 `num - lack_num` 实际语义是 **`min(持有量, 该批需求)`**，
不是全局持有量。所以分得越碎，持有量被重复计入的次数越少，结果越低估。

实测摩拉（item 202）：

| 批大小 | 计算 | 结果 |
|---|---|---|
| 10 | — | 22,551,867 |
| 50 | 409,073,750 − 386,521,883 | **22,551,867**（与 10 一致） |
| 单条 | — | 8,181,475（明显低估） |

结论：50 是实测能稳定通过的最大量级（单次响应约 4.8 MB）；再往上响应会到十几 MB，
没有必要。

---

## 4. 树脂估计只剩摩拉和经验书

**根因**：`MaterialExcelConfigData.json` 里的 `rankLevel` 是**整数**（2/3/4/5），
而 `ellen-wiki/scripts/lib/launcher-build.mjs` 里的 `QUALITY` 映射是**按名字键**的
（`QUALITY_GREEN: 2`）。`enumOf(QUALITY, row.rankLevel, 1, ...)` 拿数字去查名字表，
永远查不到，fallback 恒为 `1` → **10,673 条材料的 `RankLevel` 全是 `QUALITY_WHITE`**。

`CultivationResinStatisticsService` 的 switch 只认绿/蓝/紫/橙，于是 261 条树脂材料
全部被跳过 —— 天赋本、武器本、世界 BOSS、周本一个都统计不到。

**修复**：`QUALITY` 改为按数值映射，重建元数据。复检 265 条树脂候选材料，**0 条白色**：

| 类别 | 品质分布 |
|---|---|
| 角色天赋素材 | 绿 24 / 蓝 24 / 紫 24（另有智识之冕 1 条，按 id 排除） |
| 武器突破素材 | 绿 24 / 蓝 24 / 紫 24 / 橙 24 |
| 角色培养素材（BOSS/周本） | 紫 49 / 橙 43 |
| 角色经验素材 | 绿 1 / 蓝 1 / 紫 1 |
| 摩拉 | 蓝 1 |

**必须一起发 R2**：客户端的元数据优先链是
①同步快照 → ②Snap.Metadata 仓库 → ③随包 `Resource/Metadata` → ④空仓库。
只改随包数据没用 —— 换版本后同步会把随包修正覆盖回 R2 上的旧快照。
所以这次同时执行了 `update.py refresh → release → promote`。

---

## 5. 顺带修掉的三处

1. **晶石合成配方被异元素转化配方覆盖**
   `GetResultMaterialIdToCombineMapAsync` 原按 `Result.Id` 去重、「后者覆盖前者」，
   同一结果同时存在 3→1 合成配方与异元素转化配方时，晶石的合成配方被顶掉。
   改为**优先保留单素材合成配方**，没有合成配方的结果才沿用最后一条。
2. **自动合成预览**（新增开关）
   3→1 单向合成补齐高阶缺口，额外摩拉直接并入统计；规划器
   `Ellen.Launcher.Domain/Cultivation/MaterialSynthesisPlanner.cs` 带 11 个单测。
3. **单实例锁 / 激活管道按安装目录隔离**（`Core/InstanceScope.cs`）
   修掉「点正式版快捷方式被正在运行的开发版劫持」。同一安装目录升级前后仍互认。

另外：树脂统计对未知品质不再抛 `NotSupported`（跳过），自定义材料计划不会再把统计刷新打崩。

---

## 6. 教训

1. **契约字段不要用 `WhenWritingDefault`**：默认值恰恰是最需要发出去的那个值。
2. **枚举映射按值不按名**，并且要有「映射失败就报错」的自检 —— 这次是 `fallback 1`
   静默兜底，把「品质整体缺失」伪装成了「数据本来就这样」。
3. **数据产线必须在 R2 侧正确**，随包 `Resource` 只是兜底，会被同步快照覆盖。
4. **分块请求的 `num`/`lack_num` 是批内语义**，批大小会改变结果精度。
5. Windows 下给用户临时跑的一次性脚本必须是**纯 ASCII**：cmd.exe 按 GBK 解码，
   UTF-8 中文会把 `rem` 行拆成可执行垃圾（`'囷級锛?rem' 不是内部或外部命令`）。

---

## 7. 本次发布

| 项 | 值 |
|---|---|
| 版本 | `7.2.0.7` |
| 安装包 | `Ellen.Launcher-7.2.0.7-Setup.exe` |
| size | `251,864,134` B |
| sha256 | `37AD9016CC7F373BC5D1B5670E481245C9E4BCE7D9DE7CDE051FFE710312C4DA` |
| GitHub Release | <https://github.com/lihua123123/ellen.launcher/releases/tag/v7.2.0.7> |
| R2 元数据版本 | `20261123-215251`（manifest digest `0066dd1d8849…`，zip sha256 `f46fa8297e8d…`） |

R2 侧复检：`releases/20261123-215251/data/launcher/launcher-metadata.zip` 里
265 条树脂材料 **0 条白品质**，客户端下次同步即可拿到修复后的数据。
