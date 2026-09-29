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

| 路径 | 说明 |
|---|---|
| `miyoushe_3572896/<NN>_<篇名>/<NNN>.png` | 源图（19 篇，600 张，1096 MB，**保留**） |
| `_ocr/<NN>/<NNN>.txt` | 每张卡的整卡 OCR 文本（600 个，**保留**，重跑很贵） |
| `_gen2/entries.json`、`report.txt` | `parse_cards2.py` 对全部卡片的文本解析产物 |
| `_gen2/table_<NN>.txt` | 复核表（卡片 vs 元数据，**人工批的凭据**） |
| `_gen2/merge_<NN>.txt`、`merge_<NN>.json` | 入库清单与待核对清单 |
| `_rescue/<NN>.json` | 抗性裁图通道的结构化结果（PNG 复核图已清理，可重跑再生成） |

## 脚本

| 脚本 | 作用 |
|---|---|
| `miyoushe_download.py` | 下载合集原图（`gids=2` + `getPostFullInCollection`） |
| `ocr_via_api.py` | 走 Umi-OCR HTTP API 逐图 OCR（**必须先杀残留实例**） |
| `parse_cards2.py` | 整卡文本解析（名称/技能/机制/元素能量） |
| `rescue_resist.py` | 01 式「单横条」抗性表：按标签块坐标裁横条 + 放大 3 倍重识别 |
| `extract_states03.py` | 03 式「表格」抗性表：按列拼图识别（支持多状态行） |
| `table_01.py` / `table_03.py` | 生成复核表并与 `Monster.json` 对比 |
| `merge_01.py` / `merge_03.py` | 按 diff 门槛入库 |
| `classify_cards.py`、`check_archive.py`、`list_monsternames.py` | 统计 / 校验 / 名称查询 |

## 单篇流程

1. `python ocr_via_api.py`（需先起 Umi-OCR，跑完 `taskkill` 清残留）→ `_ocr/<NN>/`
2. **探形态**：先拿 1~2 张的 dict 识别看抗性表布局（单横条？表格？几行状态？列坐标是否右移？）
3. 按形态选 `rescue_resist.py` 或 `extract_states03.py`，**改标签/列坐标偏移**后先试点 3~5 张再全量
4. `python table_<NN>.py` 出复核表 → 人工扫一眼
5. `python merge_<NN>.py` 只收与元数据有差异的条目 → 追加进
   `src/Ellen.Launcher/Ellen.Launcher/Resource/EnemyArchives/EnemyArchives.json`
6. `dotnet build Ellen.Launcher.slnx -c Release -p:Platform=x64` + 启动存活校验

## 进度存档（2026-09-30 大规模入库完成）

### 已入库
- **`EnemyArchives.json` 共 136 条**（基线 30 + 新增 106，其中 30 条含多状态）
- 覆盖 18 篇（01~12、14~19）；build EXIT=0、启动 45s 存活、`startup-error.log` 不变
- 入库门槛（三重）：**高置信行** + 与元数据有 diff + 取值合理（5 的倍数且在 -100~500）
  - 「高置信」= 小横条 dict 直接给出 8 个独立数值块
  - 例外：01/03 篇为**已人工验证篇**，其低置信行也收

### 待办：129 行低置信抗性（需逐张读图复核）
- 成因：小横条把整行读成**粘连串**时只能靠「逐格裁切」补读，而**粘连串可能被截断**
  → 列距被低估 → 整行错列（都还是像模像样的数字，自动校验抓不到）
- 已证实例：`04/003` 行 1 卡面真值 `370,310,310,310,310,310,310,330`，
  自动化读出 `370,310,310,310,310,310,310,310`（末格错）
- 处理建议：**逐张放大单独读**（我可用视觉读，或外部 VLM），或人工转录；不要直接入库
- 分布（低置信行数）：02×15、04×28、05×6、06×14、07×2、09×7、11×11、12×3、14×4、17×5、19×2

### 13 篇（26 张）：本地 OCR 确定性失败，整体未入库
- 模板不同（2048×1080，抗性表在卡片顶部）；字体/配色导致识别输出 `OO2Q2O2OOO2OO2`、`QQ`
- 视觉模型可准确读取（已验证 13/001），但**拼页批量读会列对齐漂移**，需逐张读

## 提速（26/9/30）：4~5 倍，且精度不降反升
- 关键实测：**单实例内部串行**（同端口并发 4 次加速比 ×0.99）→ 必须多开进程（`ocr_instances.py`）
- 关键优化：**别再整图 dict**（1.2~4s，占旧流程 85%）。改为
  1. 左侧窄条 `(0~700)@0.5×` 定位标签（~0.5s）
  2. **只对状态行小横条 `(0~960 × 52px)` 做 dict**（0.13~0.25s）→ 一次拿到位置+文字
- 结果：64 张 48.5s = **0.76~0.9 s/张**（原 2.7~4 s/张）
- 精度提升实例：02/023~030 旧流程**静默丢行** 6 张，新流程全部找回；
  02/020 旧流程读出 8 个空值，新流程读到真值 `210/IMM/210…`（已目视确认）
