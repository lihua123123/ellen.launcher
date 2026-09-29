# 全量扫描（只读）：变体嫌疑卡 + 存量缺失条目

扫描 `_ocr/**/*.txt` 全部 **600** 张卡。

## 节 0：概览

- 变体嫌疑（卡上真名比匹配名长）：**38** 张（6.3%）
- 其中在 gap 工单（低置信度）里的：**11** 张；不在的：**27** 张
- 参照：gap 工单共 79 张 / 全 600 张 = 13.2%
- ⚠️ **盲区**：`match_title` 返回 `None` 的卡 **194** 张（32.3%）——
  没有基名就无从谈「比基名长」，所以这些卡**不可能**被标记。「某篇 = 0」只能读成「未测」，不能读成「干净」。
- 登记表 `_provenance/variant_registry.json`：**38** 张

### 按篇

| 篇 | 卡片数 | 嫌疑数 | 嫌疑率 |
|---|---|---|---|
| `01` | 22 | 0 | 0% |
| `02` | 31 | 9 | 29% |
| `03` | 42 | 0 | 0% |
| `04` | 48 | 5 | 10% |
| `05` | 34 | 0 | 0% |
| `06` | 46 | 2 | 4% |
| `07` | 30 | 0 | 0% |
| `08` | 31 | 0 | 0% |
| `09` | 44 | 0 | 0% |
| `10` | 32 | 1 | 3% |
| `11` | 30 | 1 | 3% |
| `12` | 48 | 0 | 0% |
| `13` | 26 | 19 | 73% |
| `14` | 42 | 0 | 0% |
| `15` | 28 | 1 | 4% |
| `16` | 27 | 0 | 0% |
| `17` | 22 | 0 | 0% |
| `18` | 3 | 0 | 0% |
| `19` | 14 | 0 | 0% |

## 节 1：存量 —— `JSON 有、元数据没有`（= 界面完全看不到的条目）

档案条目 166 个，其中 1 个不在元数据里：

| name | 状态数 | 有抗性值 |
|---|---|---|
| `丘林` | 0 | 是 |

## 节 2：增量 —— 卡上有变体名、元数据只有基名

⚠️ 这类卡会被**挂到基名条目**上，变体的行变成基名的「状态」。
`verdict` 来自登记表（**逐张人眼核对中英修饰语义**，不是抄扫描结果）：
`confirmed` = 中英语义一一对应；`suspect` = 只是子串但非变体；`unclear` = OCR 读不清。

| take | 篇 | 匹配到基名 | 中文修饰 | 卡上英文名（旁证） | verdict | 在gap |
|---|---|---|---|---|---|---|
| `02/008` | 02 | 无相之雷 | ·协奏曲紫电之光 | Electro Hypostasis: Violet Lightning Concerto | confirmed |  |
| `02/009` | 02 | 无相之风 | ·间奏曲暴风之蝶 | Anemo Hypostasis:Storm Butterfly Intermezzo | confirmed | 是 |
| `02/010` | 02 | 无相之岩 | ·奏鸣曲磐石之心 | Geo Hypostasis: Stone Heart Sonata | confirmed |  |
| `02/011` | 02 | 无相之冰 | ·奏鸣曲霜锐之阵 | Cryo Hypostasis: Sonata of the Snowy Path | confirmed |  |
| `02/012` | 02 | 无相之火 | ·三部曲 | Pyro Hypostasis: Ternary of the Blazing Hearth | confirmed | 是 |
| `02/013` | 02 | 无相之水 | ·谐曲束浪之池 | Hydro Hypostasis: Scherzo of the Rippling Pool | confirmed |  |
| `02/014` | 02 | 无相之草 | ·回旋曲森郁之笼 | Dendro Hypostasis: Rondo of the Forest Corral | confirmed | 是 |
| `02/017` | 02 | 雷音权现 | ·裂鸣 | Cracking Thunder Manifestation | confirmed |  |
| `02/020` | 02 | 水形幻人 | ：洪流 | Hydro Tulpa: Flashflood | confirmed | 是 |
| `04/003` | 04 | 爆炎树 | 焚天 | Sky-Searing Pyro Regisvine | confirmed | 是 |
| `04/008` | 04 | 古岩龙蜥 | ·溟冬 | Wintry Primo Geovishap | confirmed | 是 |
| `04/015` | 04 | 翠翎恐蕈 | 亚稳态 | MetastableJadeplume Terrorshroom | confirmed | 是 |
| `04/025` | 04 | 千年珍珠骏麟 | ·升怒 | Enraged Millennial | confirmed |  |
| `04/030` | 04 | 魔像督军 | ·协律师 | Legatus Golem:Harmonizer | confirmed | 是 |
| `06/004` | 06 | 遗迹重机 | 特异体 | AnomalousModel Ruin Grader | confirmed | 是 |
| `06/018` | 06 | 兆载永劫龙兽 | 重武装 | HeavyAssault Pack Aeonblight Drake | confirmed | 是 |
| `10/029` | 10 | 突角龙 | ·追灵光 | Tatankasaurus Warrior: Spiritlight Chaser | confirmed |  |
| `11/021` | 11 | 吞星之鲸 | ·幻影 | All-Devouring Narwhal- Phantom | confirmed | 是 |
| `13/001` | 13 | 熔岩辉龙像 | ·炽烈流焰 | Lava Dragon Statue: Blazing Flame | confirmed |  |
| `13/002` | 13 | 水形幻人 | ·极旋流 | IMM IMM IMM | confirmed |  |
| `13/003` | 13 | 秘源机兵·统御械 | ·毁灭武装 | Secret Source Automaton: Overseer Device - Obliterator | confirmed |  |
| `13/006` | 13 | 历经百战的皮皮潘偶像 | ·百诈瞬变 | Battle-Hardened Pipilpan Idol:Hundred Deceptions | confirmed |  |
| `13/007` | 13 | 兆载永劫龙兽 | ·歼灭构型 | Aeonblight Drake: Annihilation Configuration | confirmed |  |
| `13/008` | 13 | 历经百战的岩居蟹 | ·百万爆轰 | Battle-Scarred Rock Crab: One Million Blasts | confirmed |  |
| `13/009` | 13 | 历经百战的火刃突击队员 | ·决死武装 | Battle-Hardened Fireblade Shock Trooper: Forlorn Hope | confirmed |  |
| `13/010` | 13 | 霜夜巡天灵主 | ·惊恨憎愤 | Frostnight Herra: Hatred and Rage | confirmed |  |
| `13/011` | 13 | 历经百战的执灯人 | ·哀回响 | Battle-Hardened Lightkeeper: Mourning Echoes | confirmed |  |
| `13/013` | 13 | 深罪浸礼者 | ·肃烈狂音 | Iniquitous Baptist:Resonant Wrath | confirmed |  |
| `13/015` | 13 | 实验性场力发生装置 | -·极端势能 | Experimental Field Generator: Extreme Potential | confirmed |  |
| `13/016` | 13 | 深黯钓客 | ·袭掠锋刃 | Lord of the Hidden Depths: Whisperer of Nightmares | confirmed |  |
| `13/017` | 13 | 铁甲熔火帝皇 | ·敕命远征 | Emperor of Fire and Iron:Ordained Conquest | confirmed |  |
| `13/018` | 13 | 历经百战的十六倍曼陀草 | ·风滚狂蔓 | Battle-Hardened Hexadecatonic Mandragora: Stormvine | confirmed |  |
| `13/019` | 13 | 重拳出击鸭 | ·重甲武库 | Kunckle Duckle: Armored Arsenal | confirmed |  |
| `13/020` | 13 | 蕴光月守宫 | ·根牙磐错 | Radiant Moongecko:Entangled Fangs | confirmed |  |
| `13/023` | 13 | 先驱秘源统辖阵列 | 之影·蜂巢意志 | Battle-Hardened Precursor's Secret Source Control Array | confirmed |  |
| `13/024` | 13 | 秘源机兵·构型械 | ·裂解剪钳 | Secret Source Automaton: Configuration Device -Lysis Shears | confirmed |  |
| `13/025` | 13 | 嵌合翼骏狮 | ·摧枯伟力 | Chimeric Winged Lion: Power Overwhelming | confirmed |  |
| `15/005` | 15 | 雷电将军 | 反‖训练器 | Anti-RaidenShogun Trainer | suspect |  |

## 登记表里的 non-confirmed（A1 决策时需区别对待）

- `15/005` = **suspect**：**不是变体**：卡是「反雷电将军训练器」（训练桩）= 另一个实体。下游因无抗性表被 diff 门排除 → 无害

## 非 confirmed 卡的标题区原文（看中英版式）

- `15/005` → 基名 `雷电将军`：反雷电将军训练器 Anti-RaidenShogun Trainer ／ 物件Lv.100Data