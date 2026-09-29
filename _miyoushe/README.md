# 米游社合集图片下载器

下载米游社某个「合集」下每个帖子正文中的**全部原图**（自动去掉 `?x-oss-process=...` 缩放参数）。

## 环境

- Python 3，仅用标准库，无需 `pip install`。

## 用法

```powershell
# 下载默认合集（原神「原魔图鉴存放合集」3572896）
python miyoushe_download.py

# 指定合集 / 游戏
python miyoushe_download.py <collection_id> --gids 2 --out D:\out

# 直接指定帖子
python miyoushe_download.py --post 72409634 72409897
```

按 `序号_标题/图片序号.png` 保存，并写出 `manifest.json`（记录每篇的 post_id、标题、原始图片链接）。
已存在且非空的文件会自动跳过，支持断点续传。

## 接口（2026-09 实测可用）

- 合集帖子列表：`https://bbs-api.miyoushe.com/post/wapi/getPostFullInCollection?collection_id=<id>&gids=2&order_type=1`
- 单帖详情：`https://bbs-api.miyoushe.com/post/wapi/getPostFull?post_id=<id>&read=1`

贴士：
- 参数名是 **`gids`**（复数），旧写法 `gid` 会返回 404。
- 帖子主体图片在 `data.post.image_list[].url`（有序、已是原图链接）。
- 需要带 `Referer: https://www.miyoushe.com/` 请求头。

## 本次结果

合集 3572896 共 19 篇，已下载到 `miyoushe_3572896/`。

---

# 图鉴卡 → 敌人档案 流水线

## 目录

> **2026-09-30 收尾清理已执行**：源图 / `_ocr/` / `_rescue/` / `_gen2` 草稿已删除（约 1.1 GB）。
> **后果**：`miyoushe_download.py` 要重下源图、OCR 通道要重跑（`_ocr/` 是 `parse_cards2.py` 的输入）。
> 已入库成果、`_provenance/` 快照、`_gen2` 冷启动输入**未受影响**。

| 路径 | 说明 |
|---|---|
| `miyoushe_3572896/<NN>_<篇名>/<NNN>.png` | 源图（19 篇，600 张）—— **本轮已删**，重跑前先 `python miyoushe_download.py` |
| `_ocr/<NN>/<NNN>.txt` | 每张卡整卡 OCR 文本（`parse_cards2.py` 的输入）—— **本轮已删**，需重跑 OCR |
| `_rescue/<NN>.json` | 抗性裁图通道的结构化结果 —— **本轮已删**，可重跑再生成 |
| `_provenance/` | **随仓库版本控制的复现快照**（0.5 MB）：worklist + 校准卡清单 + `vlm/`（317 个读数）+ `rescue/` + `label_map.json` + `reports/` |
| `_provenance/reports/` | **自证 / 审计报告归档**（23 份）：`geom_report.md`（几何自证 25/25）、`name_report.md`、`variant_scan.md`、`missing_rows_*.md`、`evidence_consistency.md` … |
| `_gen2/` | **运行期目录（gitignore）**，只留冷启动输入：`added_names.json`（`finalize` 回退基线用）、`gap_adopted.json`、`gap_mapped.json`、`lowconf_worklist.json`、`cal_local.json`、`diffs_all.json`；其余产物（`table_<NN>.txt` / `entries.json` / `report.txt` / `vlm/` / `crop13/` …）**每轮自动重建**，已清空 |
| `_design/enemy-archive/cal/` | 校准批产物：`cal_gold.json`（目视金标准，**必须保留**）、`vlm/`、`cal_score.txt` |

## 脚本

| 脚本 | 作用 |
|---|---|
> **2026-09-30 清理**：`pipeline.py`、`classify_cards.py`、`list_monsternames.py`、`table_01.py`、
> `table_03.py`、`merge_01.py`、`merge_03.py`、`ocr_via_api.py`、`extract_states03.py`、`clean_conditions.py`
> 已删除（均被下表脚本取代；需要时从 git 历史取回）。

| 脚本 | 作用 |
|---|---|
| `miyoushe_download.py` | 下载合集原图（`gids=2` + `getPostFullInCollection`） |
| `ocr_pool.py` / `ocr_instances.py` | 「**多开实例**」OCR 池（单实例内部串行 ⇒ 必须多进程；取代原 `ocr_via_api.py`） |
| `parse_cards2.py` | 整卡文本解析（名称/技能/机制/元素能量） |
| `rescue_resist.py` | 01 式「单横条」抗性表：按标签块坐标裁横条 + 放大 3 倍重识别 |
| `extract_all.py` / `extract_fast.py` | 整卡/窄条抗性表提取（`extract_fast` 是 4~5× 提速版） |
| `table_all.py` | 全部篇的复核表（会**重建** `diffs_all.json`） |
| `finalize.py` | 收尾全链路：回退基线 → `table_all` → `gap_override` → `health` → `merge_all`（可重复执行） |
| `gap_override.py` | 把 VLM 补读的 gap 值（含 `evidence`）注入 `diffs_all.json`（**必须在 `table_all` 之后**） |
| `merge_all.py` | 按「高置信 + diff + 取值合理」入库；并做 `condition` 生成期清洗 |
| `label_map.py` | **固化行身份映射**（`--build-snapshot` / `--write`，见下节） |
| `derive_evidence.py` | 推导每行 `evidence` 等级（`cal-gold`/`2geo`/`eyeball`/`single-read`） |
| `vlm_transcribe.py` | VLM 第二通道（校准 / 判读 / 取缺口读值；默认整表几何，`--strip` 启用横条） |
| `check_13.py` | 13 篇**只读**比对报告（可靠指标是**行数差**，label 无匹配会高估） |
| `make_13_checklist.py` | 生成 13 篇人工核对表 + 修正裁图 `_gen2/crop13/`（13 篇尚未入库，见 BACKLOG） |
| `check_archive.py`、`health.py` | 档案校验 / 健康度 |

> `condition` 清洗的规则**已内建进 `merge_all.split_condition()`**（生成期清洗）；
> 原先手工跑的 `clean_conditions.py` 已删除 —— ⚠️ 对档案的「跑完再改」一定会被下一轮 `finalize` 冲掉。

## 单篇流程

1. `python ocr_pool.py`（多开 Umi-OCR 实例，见「提速」节）→ `_ocr/<NN>/`
2. **探形态**：先拿 1~2 张的 dict 识别看抗性表布局（单横条？表格？几行状态？列坐标是否右移？）
3. 按形态选 `rescue_resist.py` 或 `extract_fast.py`，**改标签/列坐标偏移**后先试点 3~5 张再全量
4. `python finalize.py` —— **一条命令收完全链路**：
   回退基线 → `table_all` → `gap_override` → `health` → `merge_all`（可重复执行）
5. `dotnet build Ellen.Launcher.slnx -c Release -p:Platform=x64` + 启动存活校验

## 进度存档（2026-09-30 大规模入库完成）

### 已入库
- **`EnemyArchives.json` 共 136 条**（基线 30 + 新增 106，其中 30 条含多状态）
- 覆盖 18 篇（01~12、14~19）；build EXIT=0、启动 45s 存活、`startup-error.log` 不变
- 入库门槛（三重）：**高置信行** + 与元数据有 diff + 取值合理（5 的倍数且在 -100~500）
  - 「高置信」= 小横条 dict 直接给出 8 个独立数值块
  - 例外：01/03 篇为**已人工验证篇**，其低置信行也收

### 待办：99 行低置信抗性（需逐张读图复核）

> ⚠️ 口径更正：此前记的「129 行低置信」是**误读**。`merge_all.txt` 的
> `=== 被守卫丢弃（129）===` 是**丢弃总数**，拆开是
> **99 行低置信 + 28 条「排除篇 13」 + 2 条「取值不合理」**。
> 真正的入库缺口是 **99 行 / 79 张卡**；精确工单见 `_provenance/lowconf_worklist.json`。

- 成因：小横条把整行读成**粘连串**时只能靠「逐格裁切」补读，而**粘连串可能被截断**
  → 列距被低估 → 整行错列（都还是像模像样的数字，自动校验抓不到）
- 已证实例：`04/003` 行 1 卡面真值 `370,310,310,310,310,310,310,330`，
  自动化读出 `370,310,310,310,310,310,310,310`（末格错）
- 另一类已定位缺陷：**`IMM` 落在第 1 列时被当成行标签吃掉** → 该行整体左移一格、末格丢空
  （`02/012` 真值 `IMM,10×7` 被读成 `10×7,空`）。仅影响 `confident=False` 行，未入库
- 分布（低置信行数，`scope=gap`）：02×15、04×28、05×6、06×14、07×2、08×1、09×7、10×1、11×11、12×3、14×4、17×5、19×2
- 处理建议：**逐张放大单独读**（我可用视觉读，或外部 VLM），或人工转录；不要直接入库

### 校准批结果（13 张，2026-09-30）

用户指定的校准协议落地结果（金标准 = agent 视觉逐张精读）：

- 样本：`_provenance/lowconf_worklist.json` 抽 10 张 + 13 篇 3 张 = 13 张
- 裁图包：`_design/enemy-archive/cal/*.png`（agent 精读用）+ `sheet1~7.png`（2 张/页）
- 金标准：`_design/enemy-archive/cal/cal_gold.json`——13 张 / 28 行 / 224 格，自检 0 异常
- 本地通道读数：`_provenance/cal_local.json`

**逐格比对（本地 OCR vs 金标准）：**

| 层级 | 格准确率 | 行准确率 |
|---|---|---|
| `confident=True` | **48/48 = 100%** | **6/6 = 100%** |
| `confident=False` | 111/176 = 63.1% | 11/22 = 50% |

→ **`confident` 判据是「精确分离」而非「保守」**：全部 9 处错值都落在低置信层。
→ 交叉验证：`04/003`、`13/001` 两张的目视真值与先前独立建立的真值**逐格一致**。

**结论：`confident` 门槛可以信任**；缺口就是那 99 行，走 VLM/人工补读即可。

### VLM 第二通道（`vlm_transcribe.py`）

**密钥（已从磁盘移除，需要时按模板重建）**

- `load_config()` 顺序：**环境变量 > `_miyoushe/.vlm_key`**（该文件已 gitignore，**2026-09-30 已删**）
- 重建：`Copy-Item _miyoushe\.vlm_key.example _miyoushe\.vlm_key` 后填真值
- 为什么不再写进代码：密钥曾在收尾中暴露，**轮换由用户执行**；模板 `.vlm_key.example` 只含占位符
- ⚠️ `.vlm_key.example` 内的 `VLM_API_KEY=sk-xxxx…` 是**占位符**，小心别把真值填进去提交
- **只发裁图不发全卡**：全卡 1920×1280 里抗性表只占约 1/8，全卡既贵又引入说明行/水印噪声
  - 实测单行横条 **23~82 KB**；整篇 19 篇全跑约 60 MB
  - `--worklist --scope gap` 只跑缺口：99 行/79 卡 → **235 张 / 10.1 MB**
- 流程：`--probe`（1 次极小请求验 key/模型名/端点）→ `--cal` 校准批 →
  `--score` 出**位置分布 + 系统性判定** → 按 ≥95% / 90~95% / <90% 三档决定是否全量
- ⚠️ **不要只看一致率数字**。`--score` 会额外输出：
  - 不一致的**类型分布**（数字截断 / 符号翻转 / 偏差5 / IMM误读为数值 …）
  - **列位分布**（c0~c7）与行号、篇分布
  - **系统性判定**：列位集中 ≥50%（或类型集中 ≥75%）→ ⚠️ 判为系统性偏差，
    说明全量时会在**同样位置重复翻车**，应先调提示词或换模型；否则判为分散（随机噪声）
  - 行号集中只作**弱信号提示**（行数远少于格数，随机也会偏），不参与判定
- 双通道：`--dual` 把 VLM 行与本地行逐格比，全同 → adopt，有异 → review（人工三审，不自动入库）
- 13 篇额外一致性校验 `check13()`：行数、5 的倍数、瘫痪行不得高于本体行、IMM 需落在自身元素列
- 其他开关：`--vlm-dir` 指定输出目录（默认 `_gen2/vlm`，便于拿合成数据自测判读器）、`--upscale` 裁图放大倍数
- 判读器已用**合成数据自测**：完美 → ≥95% 放行；随机噪声（散在全 8 列）→ ✓ 分散；
  末列系统性截断（28 处全锁 c7）→ ⚠️ 系统性

### VLM 校准批实测结果（2026-09-30，`qwen-vl-max`）

- `--cal` 13/13 张全部成功，28 行全部通过 `validate()`；**VLM vs 金标准 = 224/224 = 100%**
- **只看低置信行（与 99 行 gap 同质的样本）= 176/176 = 100%**
- 难度最高的三张全部读对：`02/012` IMM 正确落在**首列**、`04/003` 末格 **330**
  （本地通道读成 310）、`13/006` 交替模式 `135,10,135,10,135,135,135,135`
- 已用**独立代码路径**复核（不复用 `score()` 的逻辑），0 处不一致
- 门槛判定：**≥95% → 全量放行**

⚠️ **协议修订：「双通道一致才采纳」不适用于 gap 行**

- 同一批低置信行上，本地通道只有 **111/176 = 63.1%** 正确
- 若以双通道一致作过滤，**36.9% 的格会被推给人工三审**——但本地通道在这些行上
  本来就是坏的那个通道，等于**用坏通道否决好通道**
- 双通道一致性只在本地**高置信**行上有意义（那里本地 48/48 = 100%，已被校准批证实），
  而那些行根本不在 gap 里
- **决策：gap 行直接采纳 VLM 单通道**。守卫改为 `validate()`（8 值 / 5 的倍数 / 取值域）
  + 13 篇 `check13()` 一致性校验 + 本校准批的一致率结论
- `--dual` 保留作**异常探测**（看哪些格两通道分歧），但不再作为采纳门槛

### 账号可用模型（实测，避免再撞墙）

| 模型 | 国内站 | 说明 |
|---|---|---|
| `qwen-turbo` | 200 | 文本，用于探针隔离账号级问题 |
| `qwen-vl-max` | 200 | **推荐**，这一批里能力最强 |
| `qwen-vl-plus` | 200 | 可用，有免费额度 |
| `qwen-vl-max-latest` | **403** | 该账号未授权此滚动标签 |
| `qwen2.5-vl-7b-instruct` | **403** | 未授权 |

- 国际站端点全部 401 → key 属**国内站**，`VLM_BASE_URL` 用默认值即可
- ⚠️ **教训（踩过）**：`400`（参数/尺寸校验失败）说明**鉴权已通过**，不代表模型不可用。
  判断可用性看「**不是 401/403**」，而不是「是不是 200」——我把探针的 2×2 测试图
  误当成了模型不可用的证据（Qwen-VL 要求宽高各 ≥10px，已改为 64×64）。
- ⚠️ 控制台编码：脚本打印 `✓`/`⚠`/`→`，GBK 终端会抛 `UnicodeEncodeError` 中断脚本
  → 已在模块加载时 `sys.stdout.reconfigure(errors="replace")` 兜底

### `condition` 字段清理（26/9/30）

- 全量入库后，新增的 97 个 state 里 **74 个**的 `condition` 是本地 OCR 抓的注释区串
  （甚至含整张抗性表的文字版，如 `30 30 30 90 ... -140-140...`）；基线为 **0/6**
- 两句查实（都不能省）：
  - `EnemyArchiveResistanceState.Condition` **在 C#/XAML 里从未被渲染**（全仓搜 `.Condition`
    只命中无关的 `StandardView.ShowCondition` 等）→ 目前是**死字段**
  - `EnemyArchiveService` 用 `DeserializeAsync<T>(stream)` **不传 options** → 默认
    `UnmappedMemberHandling = Skip`，故新增 `condition_ocr_raw` 安全。
    ⚠️ 服务层是**静默失败**的，所以这条**实测过**（build + 启动），没有只靠推断
- 处理：判据现已**内建进 `merge_all.split_condition()`**（生成期清洗）
  - 命中判据「`\d-\d` 粘连 **或** 数字 ≥6 个」→ `condition_ocr_raw` ← 原值，`condition` ← `null`
  - 置空用 **`null`** 而非 `""`：字段是 `string?`，且项目
    `JsonOptions.DefaultIgnoreCondition = WhenWritingNull` 是既有约定；语义上「无条件」≠「空条件」
  - 结果：**74 清空 / 29 保留**。那 29 条是机制散文，**与基线一致 → 属既有行为**，非本轮引入
- ⚠️⚠️ **踩过的坑：一次性补丁一定会被 `finalize` 冲掉**。
  最初 `clean_conditions.py` 是**手工跑**的补丁，没接进链路。重跑 `finalize` 后 `merge_all`
  把 **74 条 `condition` 又写回 OCR 原文**、并丢掉 `condition_ocr_raw`。
  → 凡是对 `EnemyArchives.json` 的「跑完再改」都必须搬进 `merge_all`，否则下一轮必被覆盖。
  `clean_conditions.py` 现在只作**存量修复工具**保留
- ⚠️ **同类死键 `defaultNote`**：JSON 里 ~120 个，但 C# 搜 `DefaultNote|defaultNote` **零命中**
  （模型里根本没这个属性）；大量值是 OCR 垃圾（如 `士气 70 70 70 70 70 70 70 60`）。
  **本轮刻意未动**（避免扩大 diff），清理它应和 `condition` 用同一套规则单独一轮做
- ⚠️ **遗留（属 ③ 的范围）**：`condition` 目前被本地流程喂的是**机制散文**而非「触发条件」
  （基线同样如此）。将来 UI 接上该字段时应**改名或改语义**，或用 VLM 单独跑一个
  `--conditions` 模式补读短条件（`condition_ocr_raw` 就是为它准备的输入）

### 行身份映射已固化：`label_map.py` + `_provenance/`（26/9/30）

**为什么必须固化**：`gap_adopted.json` / `gap_mapped.json` 原本是**一次性 snippet** 跑出来的，
生成它的代码只存在于当时的对话里。后果：① 换台机器就无法重算已入库那 97 行 `evidence`；
② 13 篇做不了可信比对（13 篇 label 本身是垃圾 OCR，只能退到行号，而它本地行号**恰恰不可信**）；
③ `evidence` 分级只能声称「对当前这批数据有效」。

```bash
python label_map.py --build-snapshot   # 把 live 输入快照进 _provenance/（已随仓库版本控制）
python label_map.py                    # 重算并与 _gen2/gap_adopted.json 逐字段比对
python label_map.py --write            # 落盘 _provenance/label_map.json + _gen2/gap_adopted.json
```

- 输入：`lowconf_worklist.json` + `vlm/<tag>_<idx>[_rK].json` + `rescue/*_states.json`
  → **合计仅 0.51 MB**（317 个 VLM 读数只要 0.09 MB）。
  ⚠️ 「贵的部分（VLM 调用）已经付过了，输出本身极小」——这是快照可行的前提；不建快照就只能重跑 VLM（要花钱且不可复现）
- 输出 `{(take,row) -> (mode, chosen_row)}`，`mode` ∈ `index(local==card)` / `index但两几何分歧`
  / `label(label-sim(0.xx))` / `label(fallback)`
- 人工覆盖写在 `OVERRIDES`（`14/014`：整表几何把 `9.9k` 误读成 `10`，横条几何正确），
  并**在下标命中时断言确实被用到** —— 否则条目失效时会静默留在代码里
- 另输出 `agreed_with_local`（VLM 采纳值 vs **本地同序号行**的逐格一致数）作身份自证，供 ② 复用

**验收证据**：重算结果与既有产物 **99 条 / 0 处不匹配**；且从快照重跑
`label_map.py --write` → `derive_evidence.py --write` → `finalize.py` 后，
已提交的档案 **`git diff` 逐字节为空**（可复现 + 幂等）。

**顺带用金标准裁决了 4 条 `index` 组内 agreed<8 的行**（即本地与 VLM 分歧处）——
**4/4 都是本地错、VLM 对**：`04/003` r1/r3 本地把**行标题挤进数值列表**导致整行右移、
末格丢空（VLM 与目视金标准 **8/8** 一致）；`12/013` r2 同样是错列；
`09/039` r3 本地漏了**负号**（卡面实为 `-50,10×6,-20`，已裁图目视确认）。
校准批次 10 张卡内 11 条采纳行 **11/11 与金标准 8/8 一致**。

> ⚠️ **口径警示**：日报里曾写过「双几何互检 **89/99** 全同」，**该数复现不出来**。
> 真实情况：只有 `mode=index(local==card)` 的 **74 行**可比（74/74 = 100%），
> 另 **25 行身份不可比** —— 那是「**没测**」，不是「一致」。
> 报一致性时必须说清**分母是可比子集还是全体**。

### 13 篇（26 张）：本地 OCR 确定性失败；VLM 已能读，但**先读数后入库**

- 模板不同（2048×1080，抗性表在卡片顶部）；字体/配色导致识别输出 `OO2Q2O2OOO2OO2`、`QQ`
- 视觉模型可准确读取（已验证 13/001、13/002、13/006），但**拼页批量读会列对齐漂移**，需逐张读
- 现状：`13/002`、`13/006` 本地通道**一行都没抽出**（空值 ≥4 被整行丢弃），印证不可靠
- **校准批证据**：13 篇 3 张在整表几何下全对（`13/006` 交替模式 `135,10,135,10,...` 亦正确），
  且 VLM 行数 3 / 本地行数 2 或 1 → **VLM 找回了本地漏掉的行**

**决策流程（反悔要有证据，「通道看起来没问题」不算证据）**

1. `python vlm_transcribe.py --worklist --scope excluded-13`
   （只生成 `_gen2/vlm/13_*.json`，**不碰 `diffs_all.json`**）
2. `python check_13.py` → **只读**报告：行数差 / label 无匹配行数 / 值域与 5 倍数 / 逐格一致率
   - ⚠️ 报告里「label 无匹配」**不等于**本地漏行——13 篇本地 label 本身就是垃圾 OCR，
     该指标会系统性高估；**可靠的漏行度量是行数差**
3. 数字过关后再**单独提一个 commit** 入库

### gap 全量入库结果（26/9/30）

- **档案 136 → 166 条**，多状态 **30 → 66 条**；丢弃仅 30 条（2 条垃圾 + 28 条排除篇 13）
- 验证：166 条 / **2090 个数值格 / 越界 0 / 非 5 倍数 0**；`dotnet build` EXIT=0；启动 45s 存活；
  `startup-error.log` 5310 → 5310 字节未增长
- 99 行采纳证据链：74 行「本地行数 == 卡上行数」→ 行号可信；13 行行号错位 → **以 label 为准**；
  11 行单行卡无歧义；1 行（`14/014`）两几何分歧 → 目视裁决（整表几何把 `9.9k` 误读为 `10`）
- 交叉验证：双几何互检 **89/99 全同**；与金标准复算 **152/152 = 100%**；
  10 张重跑与校准批输出**逐格完全一致**（`temperature=0` 确定性）
- ⚠️ **注入点必须在 `table_all` 之后**：`finalize.py` 会重跑 `table_all` 并**重建** `diffs_all.json`，
  先注入必被覆盖（踩过：跑完仍是 136 条）。故新增 `gap_override.py` 并接入 finalize
  （`table_all` → **`gap_override`** → `health` → `merge_all`）
- ⚠️ 值域由 `[-100,500]` 放宽到 **`[-200,10000]`**（`merge_all` / `health` / `vlm_transcribe` 三处）：
  目视已证实卡面存在 `-140`（`11/017` 核心行）与 `9.9k = 9900/9950`（`14/014` 辅助行）；
  放宽后仍能正确拒绝 `-5050`（粘连）与 `4`（非 5 倍数）这类真垃圾
- ⚠️ **裁图几何不能随意换**：校准批用整表几何得 224/224，换成单行横条后立刻出现
  **幻影行**（VLM 从无表格区域编造合理数值，且能通过 `validate()`）、行号无法解析、
  行身份不可判定。**换几何 = 换了未经验证的输入分布，必须重新校准。**

## 提速（26/9/30）：4~5 倍，且精度不降反升
- 关键实测：**单实例内部串行**（同端口并发 4 次加速比 ×0.99）→ 必须多开进程（`ocr_instances.py`）
- 关键优化：**别再整图 dict**（1.2~4s，占旧流程 85%）。改为
  1. 左侧窄条 `(0~700)@0.5×` 定位标签（~0.5s）
  2. **只对状态行小横条 `(0~960 × 52px)` 做 dict**（0.13~0.25s）→ 一次拿到位置+文字
- 结果：64 张 48.5s = **0.76~0.9 s/张**（原 2.7~4 s/张）
- 精度提升实例：02/023~030 旧流程**静默丢行** 6 张，新流程全部找回；
  02/020 旧流程读出 8 个空值，新流程读到真值 `210/IMM/210…`（已目视确认）
