# 7.2.0.8 复盘：养成库存口径 / 旅行者同步 / 养成页性能

> 这一版只有两类事：一类是**把「需求」当成了「库存」**的语义错误，
> 一类是**看起来像布局问题、实际是虚拟化没生效**的渲染成本。
> 记录下来，避免下次再从「加个进度条」「换个布局参数」开始试。

---

## 1. 从养成计算器同步：数字虚高（175 显示成 341）

**现象**：同步提示成功，三阶嵌合材料显示 188 / 341 / 651，游戏内实际是 687 / 175 / 38。

**根因**：`overall_consume.num - lack_num` 不是持有量，是**被本次计算满足的需求**
（`min(持有量 + 合成额度, 该材料需求)`）。持有 175、需求 341 时，175 + 687/3 ≥ 341，
`lack_num` 直接是 0，于是 `num - lack_num` = 341 —— 恰好等于需求，而不是库存。

更麻烦的是 `available_material` **不能单独用**：它只返回「库存付清需求后仍有剩余」的条目，
剩余为 0 的材料（恰恰是最稀缺、最需要显示的那几个）根本不出现。上一版「只在
`available_material` 里出现时才写」于是让稀缺材料永远停在旧值。

**真实模型**（用 15:12:54 那次同步的原始响应 516 项逐条校验通过）：

```
available[i]  = max(owned[i] - demand[i], 0)        // 为 0 时服务端省略该条目
satisfied[i]  = demand[i] - lack[i] = owned[i] + credit[i]
credit[i]     = floor(available[lower] / ratio)     // 合成额度只吃低阶「剩余」，不再扣低阶自身需求
```

| 材料 | demand | lack | available | 反解 | 游戏内 |
|---|---|---|---|---|---|
| 112149 嵌合种 | 188 | 0 | 499 | 499 + 188 | **687** ✓ |
| 112150 并生嵌合胞 | 409 | 68 | 缺失 | 341 − ⌊499/3⌋ = 341 − 166 | **175** ✓ |
| 112151 源生嵌合体 | 531 | 493 | 缺失 | 531 − 493 | **38** ✓ |

**实现**（`InventoryService.DeriveOwnedCounts`）三种情形反解：

1. 在 `available_material` 里 → `剩余 + 需求`；
2. 不在、但低阶有剩余 → `被满足的需求 − 低阶剩余/比例`；
3. 不在、且没有合成来源 → `被满足的需求`。

因为 `credit` 只依赖**原始 `available`**（不依赖别项 owned），**不需要拓扑排序**。
合成链来源是已有的 `ICultivationMetadataContext.ResultMaterialIdCombineMap`，只接受
「单素材 + `Result.Count == 1` + `Materials[0].Count > 1`」的配方 —— `Combine.json` 里
269 条单素材配方中有 23 条是 1:1 的摆设/名片转化，不能当 3:1 链。

全量校验 516 项：三项真值全部命中、0 项负数、0 项「未出现却 owned > demand」，
量级也合理（摩拉 12,886,920、精锻用魔矿 3251、大英雄的经验 1637）。

---

## 2. 旅行者同步 `-500001` 复发

上一版把「50 条分批 + 递归二分 + 响应合并」当成解法，实际上 Remastered 之所以从不报错，
是因为**它压根不发旅行者**（元数据里没有 10000005，`PromotionDeltaFactory.Create()` 直接跳过）。
本项目元数据带旅行者，所以必须自己解决。

真正的关键字段是 `element_attr_id`：同一个 `avatar_id` 对应七种元素形态，服务端靠它定位
技能分组。7.2.0.7 实测通过的组合是「`element_attr_id` 回填 + 单条去掉 `skill_list`」，
而中间有一轮为了逐字对齐 Remastered 把 `element_attr_id` 删了 —— 于是变成从未验证过的
「e0 + 无 skills」，删掉分批只是让这个错误组合第一次真的发出去。

**修复**：恢复 `CreateElementAttrIdMap(avatars)`（按 calculator 返回的 `Avatar.ElementAttrId`
回填，官方网页同样带该字段）；旅行者一律 `SkillList = default`，保留等级/突破与配对武器；
请求回到单次全量，只保留一次「整套省略 `skill_list`」的完整重试（探针已证与体积无关：
174 条去 skills 返回 0，响应约 7.5 MB）。

---

## 3. 同步不再清空库存

`RefreshInventoryByCalculatorAsync` 原来是「先删全项目库存，再写响应子集」，于是响应里
没出现的材料被清成 0。改为 `UpsertInventoryItemRangeByProjectId`：单个
`SaveChanges()` 内更新已存在 ItemId、插入新 ItemId，响应缺失的本地库存保持不变；
返回空集合时也不覆盖。

---

## 4. 养成页「还是不如背包页」

第一轮只做了 N+1 与后台化，用户仍反馈不跟手。量化后确认 **ViewModel 总耗时只有 64–114ms，
瓶颈 100% 在 XAML 实例化层**：单张条目卡 40–55ms，32 张全实现，t=377ms 出第一张、
t=4175ms 还在出。逐条对齐背包页后补上六件事：

| # | 问题 | 改法 |
|---|---|---|
| 1 | `RepositoryExtension.Query()` 每次 `CreateScope()` + 新建 `AppDbContext`，逐条目查询 = 每次刷新 **61 个 DbContext** | 新增 `GetCultivateItemImmutableArrayByProjectId`（一条 JOIN），两处调用改为「一次取全 + 按 EntryId 分桶」→ **3 次** |
| 2 | `EnumerateInventoryMaterial()` 在 UI 线程对 **10673** 条 `Material` 跑 `IsInventoryItem()`（内含正则 + 排序） | 判定加实例级缓存（`bool?` + `??=`），整轮从上万次正则降到每材料一次；视图构建挪到 `SwitchToBackgroundAsync` |
| 3 | `UniformStaggeredLayout.MeasureOverride` 为了算列高 `for (i < context.ItemCount) GetOrCreateElementAt(i)` —— **假虚拟化** | 换 `UniformGridLayout`（`ItemsStretch=Fill`），只实现视口内条目 |
| 4 | 页面没进 Frame 缓存，离开即拆 ViewModel / DI 作用域 | 根节点加 `NavigationCacheMode="Required"` + `PreserveOnUnload => true`（与 `BackpackPage` 同一套） |
| 5 | `Pivot` 会 measure 它持有的每一个 `PivotItem`，一次发布所有标签页 → 所有列表同时实例化 | `SelectedIndex` 双向绑定 + `SelectedTabIndex`/`SelectedMaterialTabIndex`，只构建当前可见页；离开统计页不白算但保留 dirty |
| 6 | 赋值 `ItemsSource` 会拆掉并重建所有已实现容器；每次勾选还先 `StatisticsItems = null`（issue #2044 的 hack）把列表拉回顶部 | `IsSameStatistics` 逐项比对（Id/Count/Current），相同则不赋值；删掉 `= null` 那句 |

外加卡片本身：去掉 `AcrylicGridCardStyle` 与 `Effects.Shadow`，固定 380px 高度、材料区改为
卡内 `ScrollViewer` —— 亚克力与阴影在 500+ 卡片规模下是纯亏。

**注意**：勾选完成状态其实不会改变统计数字（`Current` 来自背包、`Count` 来自条目），
所以第 6 条的比对命中率极高，这也是「勾一下卡一下」消失的主要原因。

---

## 5. 教训

1. **字段名里有 `num` 不代表它是库存**：`num` 是需求、`lack_num` 是被合成补上的部分，
   `num - lack_num` 是「被满足的需求」。要库存就找 `available`，或者自己反解。
2. **「接口没返回」不等于「值为 0」**：`available_material` 省略的条目才是稀缺材料，
   按「没返回就跳过」处理必然把最需要的数据留在旧值。
3. **虚拟化要看布局的 `MeasureOverride`**：名字里带 `VirtualizingLayout` 不代表真虚拟化，
   只要它为了算列高遍历 `ItemCount`，就等于全量实例化。
4. **UI 卡顿先量 `ElementPrepared` 的时间间隔**，别先怀疑数据层。这次数据层只有 100ms，
   渲染层 4 秒。
5. XAML 报一片 `WMC0001/WMC9999` 时**先看 C# 编译**，它们几乎都是级联错误。

---

## 6. 本次发布

| 项 | 值 |
|---|---|
| 版本 | `7.2.0.8` |
| 安装包 | `Ellen.Launcher-7.2.0.8-Setup.exe` |
| size | `248,655,138` B |
| sha256 | `D375514DF768B621FF83D5904C3110A62270220AB206C293AA9BB96F71041183` |
| GitHub Release | <https://github.com/lihua123123/ellen.launcher/releases/tag/v7.2.0.8> |

打包仍然只能走 `Installer/build-installer.ps1`：`dotnet build -p:Platform=x64` →
`robocopy /MIR` → `sync_bundle.py` + `sync_assets.py` → `ISCC`。其中 `robocopy /MIR`
会删掉 `Installer/Publish/Resource/EnemyArchives/Web753`，脚本靠「先 restore
`workspace/wiki-migration/reports/enemy-assets-acceptance.json` 再 `sync_assets`」兜住，
所以不要绕过脚本手工 robocopy。
