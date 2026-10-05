# P1-A 元数据预算

状态：**未开始**（计划排在 10-09）。此前被记为「✅ 已裁剪至 < 8 MB」，实测不成立。

## 实测（2026-10-05）

```
Metadata       18.81 MB  (156 文件)     目标 < 8 MB，差 2.35 倍
Resource 总计  52.91 MB
```

最大的 6 个文件：

| 文件 | 大小 |
|---|---|
| `CHS/Material.json` | 3,007,582 B |
| `CHS/Furniture.json` | 2,119,256 B |
| `CHS/MainQuest.json` | 1,578,448 B |
| `CHS/BeyondItem.json` | 713,257 B |
| `CHS/Achievement.json` | 709,554 B |
| `CHS/WeaponPromote.json` | 638,424 B |

本轮新增的两个派生文件（体量可忽略）：

```
Resource/Derived/AvatarScalingStat.json    49,776 B
Resource/CombatNotes/ResistanceTiers.json   2,336 B
```

## 裁剪口径

本 MVP 的评分/伤害/充能路径只需要 `Avatar/`、`ReliquaryAffixWeight.json`、
`Monster.json`（抗性主源）、以及 `AvatarCombatNotes.json`。仅随包**主角色与热数据**，
其余（`Material` / `Furniture` / `MainQuest` / `Achievement` / `BeyondItem` …）按需加载。

## 验收项

1. **10 条金标准向量在裁剪前后逐位一致**（`tolerance: 0`，不是"近似一致"）
2. `Monster.json` 的 `BaseValue.*SubHurt` 覆盖率仍为 474/562，档位仍为实测 16 档
3. 冷启动 P95 < 300 ms（真机 20 次）
4. 裁剪不引入运行时网络请求（红线 5 不得回退）

## 一个必须守住的边界

`ResistanceLiteralGate` 与 `weight_fallback.py` 依赖 `Monster.json` 与
`AvatarScalingStat.json`。裁剪若移除或改名这些文件，**门禁必须先失败**——
不允许通过放宽门禁来让裁剪「通过」。
