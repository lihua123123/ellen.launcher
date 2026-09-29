# 被多张卡映射到同一个条目名的（merge_all 会合并）

`merge_all` 按 `meta` 分组，所以这些卡的行会被**合并进同一个条目**。
逐张看 OCR 开头的真实标题，判断是否真是同一种怪。

| 条目名 | 卡数 | take | OCR 开头（含真实标题） |
|---|---|---|---|
| `深海龙蜥·原种` | 3 | `04/009` | 成体幼体 深海龙蜥·：原种 Primordial Bathysmal Vishap 盘踞深海的水栖猛兽 Deep Sea-Dwelling Beast 精英-成体Lv.100  |
| `深海龙蜥·原种` | 3 | `04/010` | 成体幼体 深海龙蜥·吞雷 BolteaterBathysmal Vishap 盘距深海的水栖猛兽 DeepSea-DwellingBeast 精英-成体Lv.100 Data  |
| `深海龙蜥·原种` | 3 | `04/011` | 成体幼体 深海龙蜥·啮冰 Rimebiter Bathysmal Vishap 盘踞深海的水栖猛兽 Deep Sea-Dwelling Beast 精英-成体 Lv.100 D |
| `爆炎树` | 2 | `04/002` | 正常瘫痪 爆炎树 Pyro Regisvine 恒常燃烧的巨藤 Giant Everblaze Calamus 首领 Lv.103 Data HP: 275905 (7.0H) |
| `爆炎树` | 2 | `04/003` | 正常瘫痪削弱瘫痪 焚天爆炎树 Sky-Searing Pyro Regisvine 温顺的潜在伙伴 Gentle Potential Companion 首领 Lv.103 D |
| `古岩龙蜥` | 2 | `04/007` | 起身正常弹反 古岩龙蜥 Primo Geovishap 曾与「龙王」摇撼山岳的岩龙 Geo Dragon Who Once Raged With the King 首领Lv.1 |
| `古岩龙蜥` | 2 | `04/008` | 起身正常盾反 古岩龙蜥·溟冬 Wintry Primo Geovishap 圆滚滚肚皮 Round Tummy Tum Tum 首领Lv.103 Data HP: 155476 |
| `风蚀沙虫` | 2 | `04/016` | 正常深渊 风蚀沙虫 Setekh Wenut 一万万砂砾最后的亲王 The Last Prince of the Sands 首领 Lv.103 Data HP: 853091 |
| `风蚀沙虫` | 2 | `04/027` | 正常起身爆发 隐山兽 Solitary Suanni 悠古的群山之主 Ancient Lord of the Mountains 首领Lv.103Data HP: 101152 |
| `魔像督军` | 2 | `04/029` | 入战带甲深渊带甲破甲 魔像督军 Legatus Golem 「白石与黄铜的造像」 "Statue of Marble and Brass' 首领 Lv.103 Data HP: |
| `魔像督军` | 2 | `04/030` | 入战带甲词条带甲破甲 魔像督军·协律师 Legatus Golem:Harmonizer 无情烈火的旋律 Rhythm of Ruthless Flames 首领Lv.105  |
| `兆载永劫龙兽` | 2 | `06/017` | 起身正常 兆载永劫龙兽 AeonblightDrake 永续永动·Gl-Faf Perpetual Motion GL-Faf 首领 Lv.103 Data HP: 10358 |
| `兆载永劫龙兽` | 2 | `06/018` | 起身/升空 正常 重武装兆载永劫龙兽 HeavyAssault Pack Aeonblight Drake 永续永动·Gl-Faf Perpetual Motion GL-Fa |
| `重拳出击鸭` | 2 | `06/043` | 正常歼灭歼灭深渊 瘫痪 重拳出击鸭 KnuckleDuckle 工坊主人的得意之作 Creator's Pride 首领Lv.103Data HP: 1645248 (27.0 |
| `重拳出击鸭` | 2 | `14/020` | 正常深渊 冲鸭机关 Stamping Devices 其他Lv.100Data HP: 388220 (7.0F) ATK: 7387 (3.0B2) Wt/Ed:100/21 |
| `若陀龙王` | 2 | `11/008` | 水二火 若陀龙王 Symphony Cryo Hypostasis 被封印的岩龙之王 Sealed Lord of Vishaps 首领 Lv.90 Data HP: <306 |
| `若陀龙王` | 2 | `11/010` | 水二火 若陀龙王 Azhdaha 被封印的岩龙之王 Sealed Lord of Vishaps 周本-第三阶段 Lv.90 Data HP: <306862 (12.6H 3 |
| `祸津御建鸣神命` | 2 | `11/014` | 正常眩晕 祸津御建鸣神命 Magatsu Mitake Narukami no Mikoto 雷电之稻妻殿 Raiden no Inazuma Tono 周本-刀形态Lv.90 |
| `祸津御建鸣神命` | 2 | `11/015` | 恶耀无盾眩晕 祸津御建鸣神命 Magatsu Mitake Narukami no Mikoto 雷电之稻妻殿 Raiden no Inazuma Tono 周本-恶耀影向天魔 |

- 单条目被 **9** 个名字映射到多卡；涉及 **19** 张卡
- 其余 60 个名字各自只有 1 张卡

## 逐张对照：真实标题 vs 匹配名（只列被怀疑的 4 个）

- `12/047` 匹配名 `草史莱姆` —— OCR 开头：`正常聚光& 塌缩 虹彩史莱姆 PrismSlime 地方传奇Lv.103Data HP:7507205 (123.2F) ATK: 28719 (11.0B2) Wt/Ed: `
- `05/030` 匹配名 `贪食匿叶龙山王` —— OCR 开头：`正常瘫痪瘫痪再恢开场 深邃墓结株 Tenebrous Papilla 虚界空想植株 Void Fantasy Plant 首领Lv.103 Data HP: 974962 (1`
- `04/027` 匹配名 `风蚀沙虫` —— OCR 开头：`正常起身爆发 隐山兽 Solitary Suanni 悠古的群山之主 Ancient Lord of the Mountains 首领Lv.103Data HP: 101152`
- `14/020` 匹配名 `重拳出击鸭` —— OCR 开头：`正常深渊 冲鸭机关 Stamping Devices 其他Lv.100Data HP: 388220 (7.0F) ATK: 7387 (3.0B2) Wt/Ed:100/21`