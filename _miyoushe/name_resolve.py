# -*- coding: utf-8 -*-
"""卡片名称解析（第 0a 步）：把「匹配器」变成一个可自证的工具。

为什么单独成文件
----------------
原实现散在 `table_all.py` 里（`find_name_in_text` / `lookup` / `resolve_name`），
**没有办法单独验证**——只能靠跑全流程看结果，而结果又依赖一堆中间产物。
于是「匹配器对不对」这件事从来没有被独立测过，直到发现 7 个条目被错误合并。

本模块只做一件事：**给一段卡片 OCR 文本，返回它属于哪个元数据名（或 None）**。
可 `--selftest` 自证，不碰任何数据、不碰界面。

三条规则（每条都有失败的实测反例支撑）
------------------------------------
1. **只在「标题区」匹配**（`Data` / `HP:` 之前）
   - 反例：`12/047` 卡是「虹彩史莱姆」，注释里有「（草史莱姆为 8%）」
     → 旧实现从注释里捞到 **草史莱姆**（出现位置 1282）
   - 反例：`05/030` 卡是「深邃墓结株」→ 捞到 **贪食匿叶龙山王**（位置 1147）
   - 用**结构化判据**而不是「前 N 字」：卡版式固定（状态标签 + 中英名 + 类别Lv + `Data HP:`）

2. **位置优先，其次最长**（而不是「最长优先」）
   - 反例（等长歧义）：`04/010` 的标题区有「深海龙蜥·吞雷」，若全文里另有
     「深海龙蜥·原种」，两者归一化后**都是 6 字** → 旧实现按长度排序，**谁赢看字典序**
   - 标题里的名字**一定在最前面**，所以「出现位置最早」是比「最长」更可靠的判据
   - 这条同时让结果**与索引的插入顺序无关**（旧的 `sorted(...)` 依赖 `meta_by_name` 的字典序）
   - 同位置时再用「最长」打破平局（更长 = 更具体）

3. **别名是「回退层」，不参与主索引**
   - 反例：`12/047` 的真名「虹彩史莱姆」不在元数据里，元数据只有
     「历经百战的虹彩史莱姆」（`历经百战` 是**系统性的地方传奇前缀**，元数据里有 43 条）
   - ⚠️ 别名**必须做碰撞检测**：`历经百战的深海龙蜥·原种` 剥前缀 → `深海龙蜥·原种`
     **与真实条目撞车**。实测 26 条别名因碰撞被拒绝；不检测就会造出新的误合并
   - ⚠️ 别名**不能并进主索引**：并入后它会与真实名字一起参与排序 → **静默抢匹配**
     （这正是我上一版探针两次结果不一致的可疑点）。故别名只在主索引 **全无命中** 时启用

已知**未修**的缺陷（不在 0a 范围，写在这里防遗忘）
------------------------------------------------
- `lookup()` 的共同后缀模糊太松：`狼追猎者` 与 `遗迹猎者` 只共享 2 字后缀就命中
  → `17/012` 仍会被误归到 `遗迹猎者`。（= 用户所说的 A2/B 类，属技术决策，另开一步）
- **一张卡覆盖两种怪**时，单名解析本质歧义：`11/027`（升扬之王 / 升华之后）、
  `12/013`（`·移即` / `·天虞`）→ 这类卡**必须按行分组**，本模块不解决。

用法：
    python name_resolve.py --selftest     # 已知答案 + 确定性 + 别名碰撞守卫
    python name_resolve.py --report       # 79 张 gap 卡的匹配结果（第 0b 步用）
"""
import argparse
import json
import random
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
ROOT = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher")
META = ROOT / "Resource/Metadata/Genshin/CHS/Monster.json"
OCR = HERE / "_ocr"
GEN = HERE / "_gen2"

# 元数据里「<前缀> + 真名」的地方传奇前缀。剥离后可作为**别名**（回退层）。
PREFIXES = ("历经百战的",)

MIN_KEY = 3          # 归一化后短于这个长度的名字不进索引（2 字名太容易误命中）
TITLE_END = re.compile(r"\bData\b|HP\s*:")
# 「同 token 内的分隔符」：卡上写变体名时用 `·`（OCR 有时读成 `：`）。
# 判定「匹配名只是更长真名的子串」时，这些字符不算 token 边界。
IN_TOKEN = "\u00b7-\uff0d\u2014\u30fb\uff1a:"


def _norm_with_map(s):
    """归一化，并记录每个归一化字符对应的**原串下标**（用于查邻接字符）。"""
    out, mp = [], []
    for i, ch in enumerate(s):
        if re.match(r"[\u4e00-\u9fffA-Za-z0-9]", ch):
            out.append(ch)
            mp.append(i)
    return "".join(out), mp


def longer_name_suspect(text, name):
    """匹配名是否只是标题里某个**更长 CJK 串**的子串（= 卡上真名比匹配名长）。

    为什么需要它：元数据里**没有**变体全名时，解析会退化到基名，而基名
    确实是标题的一部分（如 `焚天爆炎树` 里的 `爆炎树`）→ 该卡会被合并进基名条目。
    这是「7 个错误合并」的**同族问题**，但**不是多卡撞名**，单卡就会发生。

    两类 modifier 都要覆盖：
      - 前缀型：`焚天爆炎树`（命中 `爆炎树`）· `重武装兆载永劫龙兽`（命中 `兆载永劫龙兽`）
      - 后缀型：`古岩龙蜥·溟冬` · `魔像督军·协律师`（`·` 算同 token 内分隔符）

    自证（见 selftest 层 F）：全 79 张 gap 卡里标出 11 张，人眼逐张对 OCR 里的**英文名**核实，
    11/11 为真（卡上确实印了更长的变体名），0 漏 0 误报。
    """
    if not name:
        return False
    raw = title_region(text)
    nt, mp = _norm_with_map(raw)
    k = norm_key(name)
    p = nt.find(k)
    if p < 0:
        return False
    b, e = mp[p], mp[p + len(k) - 1]
    if b > 0 and re.match(r"[\u4e00-\u9fff]", raw[b - 1]):
        return True                      # 左侧还粘着 CJK → 前缀型 modifier
    j = e + 1
    while j < len(raw) and raw[j] in IN_TOKEN:
        j += 1
    return j < len(raw) and bool(re.match(r"[\u4e00-\u9fff]", raw[j]))   # 右侧 → 后缀型


def norm_key(s):
    """归一化：只保留中日韩字与字母数字。

    额外注意：OCR 会把 `·` 读成 `：`（如 `深海龙蜥·：原种`），而 `：` 也会被这一步去掉，
    所以不需要单独处理 —— 只要别把 `：` 当成保留字符即可。
    """
    return re.sub(r"[^\u4e00-\u9fffA-Za-z0-9]", "", s)


def title_region(text):
    """标题区 = 第一个 `Data` / `HP:` 之前。找不到标记时退化为全文（保守）。"""
    m = TITLE_END.search(text)
    return text[:m.start()] if m else text


def load_meta_names():
    data = json.loads(META.read_text(encoding="utf-8"))
    return sorted({m["Name"] for m in data if isinstance(m, dict) and m.get("Name")})


def build_index(names):
    """返回 `(main, alias)` 两张表：`归一化键 -> 元数据名`。

    别名只在「不与任何真实名字归一化撞车」时才建（碰撞检测，见模块 docstring 规则 3）。
    """
    main = {}
    for n in names:
        k = norm_key(n)
        if len(k) >= MIN_KEY:
            main.setdefault(k, n)
    alias = {}
    for n in names:
        for p in PREFIXES:
            if not n.startswith(p):
                continue
            k = norm_key(n[len(p):])
            if len(k) < MIN_KEY or k in main:
                continue          # 撞车 → 不建别名
            alias.setdefault(k, n)
    return main, alias


def _best(nt, table):
    """在归一化文本 `nt` 里选命中：**位置最早**，同位置取最长，再取字典序最小。

    纯 `min` over 全部候选 → **与 table 的插入顺序无关**（这是确定性的来源）。
    """
    cand = None
    for k, name in table.items():
        p = nt.find(k)
        if p < 0:
            continue
        item = (p, -len(k), k, name)
        if cand is None or item[:3] < cand[:3]:
            cand = item
    return cand[3] if cand else None


def match_title(text, main, alias):
    """返回 `(元数据名, 命中来源)`；`命中来源 ∈ {"main", "alias", None}`。"""
    nt = norm_key(title_region(text))
    got = _best(nt, main)
    if got:
        return got, "main"
    got = _best(nt, alias)          # 回退层：主索引全无命中才试
    if got:
        return got, "alias"
    return None, None


# ---------------------------------------------------------------- 自证

# 已知答案（**单一期望**，不许写「或」—— 写「或」就没法判对错了）
# 来源：卡面 OCR 标题区 + 元数据存在性实测（见 _gen2/name_testcases.md）
KNOWN = [
    # (take, 期望, 依据)
    ("12/047", "历经百战的虹彩史莱姆", "别名层：元数据只有带前缀的形态"),
    ("04/010", "深海龙蜥·吞雷", "标题区精确；旧实现在等长歧义下赢不稳"),
    ("04/011", "深海龙蜥·啮冰", "同上"),
    ("04/009", "深海龙蜥·原种", "OCR 把 `·` 读成 `：` → 归一化后仍可对上"),
    ("11/028", "多托雷", "多名卡（月的异端者 / 多托雷）"),
    ("02/023", "霜夜巡天灵主", "常规"),
    ("06/041", "秘源机兵·统御械", "常规"),
    # None 的四类（必须区分，不能压成一个 None）
    ("04/027", None, "OCR 漏字：元数据是 `隐山猊兽`，卡上是 `隐山兽` → **不放宽 fuzzy**"),
    ("05/030", None, "真名（深邃墓结株）缺元数据"),
    ("14/020", None, "真名（冲鸭机关）缺元数据"),
    ("17/012", None, "标题区无匹配；`lookup()` 仍会误命中 `遗迹猎者`（已知未修）"),
]

# 一张卡覆盖两种怪 → 单名解析**本质歧义**，不算 pass/fail，单列
AMBIGUOUS = {
    "11/027": ("升扬之王", "升华之后"),
    "12/013": ("赤璋岳巡府君·移即", "赤璋巡岳府君·天虞"),
}

def scan_suspects(main, alias):
    """返回「卡上真名比匹配名长」的 take 集合。**全 `_ocr` 张**，不只 gap 子集。

    ⚠️ 已知盲区：`match_title` 返回 None 时，`longer_name_suspect` 按定义不可能命中
    （没有基名就无从谈「比基名长」）。所以「未被标记」的卡里混着**没匹配上的卡**，
    不等于「干净」。`scan_all` 会把 None 数单独报出来。
    """
    out = set()
    for p in OCR.glob("*/*.txt"):
        txt = p.read_text(encoding="utf-8", errors="replace")
        nm, _ = match_title(txt, main, alias)
        if nm and longer_name_suspect(txt, nm):
            out.add("%s/%s" % (p.parent.name, p.stem))
    return out


# 层 F 的真值来源：`_provenance/variant_registry.json`（随仓库版本控制）。
# 它是一张**逐张人眼核对过的**登记表（cn_mod ↔ en_mod 语义是否一一对应）。
# ⚠️ 「登记」不等于「把扫描结果抄一遍」—— 每条都写过中英对照的证据，且分 verdict。
PROV = HERE / "_provenance"
REGISTRY = PROV / "variant_registry.json"


def load_registry():
    """返回 `{take: row}`；缺失或为空时返回 `{}`（层 F 会据此报告失败）。"""
    if not REGISTRY.exists():
        return {}
    doc = json.loads(REGISTRY.read_text(encoding="utf-8"))
    return {r["take"]: r for r in doc.get("rows", [])}


GAIN, BAD = [], []


def _ocr(take):
    tag, idx = take.split("/")
    return (OCR / tag / (idx + ".txt")).read_text(encoding="utf-8", errors="replace")


def selftest(verbose=True):
    names = load_meta_names()
    main, alias = build_index(names)
    GAIN.clear()
    BAD.clear()

    def say(s):
        if verbose:
            print(s)

    say("=== A. 索引规模 ===")
    say("  元数据名 %d 个；主索引 %d 键；别名 %d 键（剥离前缀后建）" % (len(names), len(main), len(alias)))
    say("  别名键示例：%s" % ", ".join(sorted(alias)[:6]))

    say("")
    say("=== B. 别名碰撞守卫（剥离后与真实名撞车 → 必须不建）===")
    # 历经百战的深海龙蜥·原种 剥前缀 = 深海龙蜥·原种，而后者是真实条目 → 不能建别名
    assert "深海龙蜥原种" in main, "`深海龙蜥·原种` 应在主索引里"
    ok = "深海龙蜥原种" not in alias
    say("  %s `深海龙蜥·原种` 未进别名表（真实条目优先）" % ("OK " if ok else "!! "))
    if not ok:
        BAD.append("别名碰撞守卫失效：深海龙蜥原种 进了别名表")

    say("")
    say("=== C. 已知答案（单一期望）===")
    for take, want, why in KNOWN:
        got, src = match_title(_ocr(take), main, alias)
        good = got == want
        if not good:
            BAD.append("%s 期望 %s 实得 %s" % (take, want, got))
        else:
            GAIN.append(take)
        say("  %s %-9s -> %-22s (来源 %s)   %s"
            % ("OK " if good else "!! ", take, got or "None", src or "-", why))

    say("")
    say("=== D. 确定性（与索引插入顺序无关）===")
    takes = sorted({r["take"] for r in _gap_rows()})
    rng = random.Random(20260930)
    base = {t: match_title(_ocr(t), main, alias) for t in takes}
    unstable = []
    for trial in range(5):
        items = list(main.items())
        rng.shuffle(items)
        m2 = dict(items)
        a2 = dict(list(alias.items())[::-1])
        for t in takes:
            if match_title(_ocr(t), m2, a2) != base[t]:
                unstable.append(t)
    if unstable:
        BAD.append("确定性失败：%s" % sorted(set(unstable))[:5])
    say("  %d 张卡 × 6 种索引顺序 → %s" % (len(takes), "全部一致" if not unstable else "有 %d 张不一致" % len(set(unstable))))

    say("")
    say("=== E. 一张卡两种怪（已知歧义，不算 pass/fail）===")
    for take, pair in AMBIGUOUS.items():
        got, _ = match_title(_ocr(take), main, alias)
        say("  %-9s 卡上同时有 %s / %s → 当前返回 %s（**需按行分组**，0a 不解决）"
            % (take, pair[0], pair[1], got))

    say("")
    say("=== F. 疑似「卡上真名比匹配名长」（= 变体名比元数据细）===")
    reg = load_registry()
    if not reg:
        BAD.append("登记表读不到：%s" % REGISTRY)
    got_sus = scan_suspects(main, alias)
    want_sus = set(reg)
    miss, extra = sorted(want_sus - got_sus), sorted(got_sus - want_sus)
    for t in sorted(want_sus & got_sus)[:6] + miss + extra:
        tag = "OK  " if t in want_sus and t in got_sus else ("MISS" if t in want_sus else "EXTRA")
        nm, _ = match_title(_ocr(t), main, alias)
        say("  %s %-9s 匹配=%-22s %s" % (tag, t, nm, (reg.get(t) or {}).get("cn_mod", "-")))
    if miss:
        BAD.append("层 F 漏报：%s" % miss)
    if extra:
        BAD.append("层 F 多报（须先人眼核并登进 variant_registry.json）：%s" % extra)
    vc = {"confirmed": 0, "suspect": 0, "unclear": 0}
    for r in reg.values():
        vc[r.get("verdict", "unclear")] = vc.get(r.get("verdict", "unclear"), 0) + 1
    for r in reg.values():
        if r.get("verdict") == "confirmed" and not r.get("en_mod"):
            BAD.append("%s 标 confirmed 但无英文修饰证据" % r.get("take"))
    say("  扫描 %d 张 → 嫌疑 %d，登记表 %d 张；多报 %s，漏报 %s"
        % (len(list(OCR.glob('*/*.txt'))), len(got_sus), len(want_sus), extra or "无", miss or "无"))
    say("  verdict：confirmed %d / suspect %d / unclear %d"
        % (vc.get("confirmed", 0), vc.get("suspect", 0), vc.get("unclear", 0)))
    for r in sorted(reg.values(), key=lambda x: x["take"]):
        if r.get("verdict") != "confirmed":
            say("    ⚠️ %s = %s：%s" % (r["take"], r.get("verdict"), r.get("evidence")))

    say("")
    okall = not BAD
    say("=== 结论：%s ===" % ("PASS" if okall else "FAIL"))
    for b in BAD:
        say("  !! " + b)
    return okall


def _gap_rows():
    return [r for r in json.loads((GEN / "lowconf_worklist.json").read_text(encoding="utf-8"))
            if r.get("scope") == "gap" and r.get("meta")]


def english_name(text):
    """从标题区取卡片**印在卡上的英文名**（第一段像名字的拉丁串）。

    这是**独立于中文匹配器的旁证**：中文匹配器可能错，但英文名是另一条印在卡上的信息，
    OCR 没理由同时把中英文读到「能互相印证」的程度。

    ⚠️ 两个实测坑（13 篇全部命中第一个）：
      1. **水印行在标题区最前面**：13 篇的标题区第一行是 `制图@Csxylic米游社`，
         直接取「第一段拉丁串」会拿到 `Csxylic`（作者水印），而不是怪物英文名。
         → 跳过含 `@` / `米游社` 的行。
      2. 英文名可能被 `:` / `-` 拆开（`Anemo Hypostasis:Storm Butterfly Intermezzo`、
         `All-Devouring Narwhal- Phantom`）→ 字符类里要含 `:` 与 `-`。
    另外要求 ≥2 个空白分隔词、总长 ≥8，用来排除 `HP` / `Lv.90` / 单词水印。
    """
    for ln in title_region(text).splitlines():
        if "@" in ln or "米游社" in ln:
            continue
        for m in re.finditer(r"[A-Za-z][A-Za-z'\-: ]{6,}", ln):
            s = re.sub(r"\s+", " ", m.group(0)).strip(" :-")
            low = s.lower()
            if low.startswith(("data", "hp", "lv")) or " lv" in low:
                continue
            if len(s) < 8 or len(s.split()) < 2:
                continue
            return s
    return ""


def _cjk_run_of(raw, name):
    """返回标题区里包含匹配名的那段**连续 CJK 串**（含 `·` 等分隔符），用来判断 modifier 在左/右。"""
    j = raw.find(name)
    if j < 0:
        return ""
    a = j
    while a > 0 and (re.match(r"[\u4e00-\u9fff]", raw[a - 1]) or raw[a - 1] in IN_TOKEN):
        a -= 1
    b = j + len(name)
    while b < len(raw) and (re.match(r"[\u4e00-\u9fff]", raw[b]) or raw[b] in IN_TOKEN):
        b += 1
    return raw[a:b].strip()


def modifier_side(raw, name):
    """中文串里 modifier 在基名的哪一侧：`"left"` / `"right"` / `"none"`。"""
    run = _cjk_run_of(raw, name)
    if not run:
        return "none"
    k = run.find(name)
    left = norm_key(run[:k])
    right = norm_key(run[k + len(name):])
    if left and right:
        return "both"
    if left:
        return "left"
    if right:
        return "right"
    return "none"


def report():
    """第 0b 步：79 张 gap 卡的匹配结果（只读，不写任何数据）。"""
    names = load_meta_names()
    main, alias = build_index(names)
    rows = _gap_rows()
    cur = {}
    for r in rows:
        cur[r["take"]] = r["meta"]
    out = ["# 第 0b 步（只读）：79 张 gap 卡的名称解析结果", "",
           "⚠️ **「不变」不等于「正确」**：最后一列标出「卡上真名比匹配名长」的卡 ——",
           "它们会被合并进**基名**条目（变体全名不在元数据里）。", "",
           "| take | 当前条目 | 新规则结果 | 变化 | 来源 | 疑似变体全名（需建条目） | 备注 |",
           "|---|---|---|---|---|---|---|"]
    stat = {"改名": [], "None": [], "不变": []}
    sus = []
    for take in sorted(cur):
        got, src = match_title(_ocr(take), main, alias)
        look = longer_name_suspect(_ocr(take), got)
        if got is None:
            ch = "**None**"; note = "需人眼分类（别名可救/OCR漏字/真名缺元数据/fuzzy误匹配）"
            stat["None"].append(take)
        elif got != cur[take]:
            ch = "**改名**"; note = ""
            stat["改名"].append((take, cur[take], got))
        else:
            ch = ""; note = ""; stat["不变"].append(take)
        if look:
            sus.append((take, got))
            note = (note + " " if note else "") + "⚠️ **卡上真名更长**"
        if take in AMBIGUOUS:
            note = (note + " " if note else "") + "**一张卡两种怪**"
        out.append("| `%s` | %s | %s | %s | %s | %s | %s |"
                   % (take, cur[take], got or "None", ch, src or "-",
                      "⚠️" if look else "", note))
    out += ["", "- 改名 **%d** / None **%d** / 不变 **%d**"
            % (len(stat["改名"]), len(stat["None"]), len(stat["不变"])),
            "- ⚠️ **疑似变体全名（卡上真名更长）%d 张** —— 这 %d 张会被挂到基名条目上"
            % (len(sus), len(sus)), ""]
    out += ["## 疑似变体全名的卡（A1 决策的真正输入）", "", "| take | 被匹配到（基名） |", "|---|---|"]
    out += ["| `%s` | %s |" % s for s in sus]
    out += ["", "## 改名明细", "", "| take | 当前 | 新 |", "|---|---|---|"]
    out += ["| `%s` | %s | %s |" % d for d in stat["改名"]]
    out += ["", "## None 明细", ""] + ["- `%s`" % t for t in stat["None"]]
    (GEN / "name_report.md").write_text("\n".join(out), encoding="utf-8")
    print("报告 -> %s（改名 %d / None %d / 不变 %d）"
          % (GEN / "name_report.md", len(stat["改名"]), len(stat["None"]), len(stat["不变"])))


def scan_all():
    """全量只读扫描：所有 `_ocr/**/*.txt` 里「卡上真名比匹配名长」的卡。

    分两节，因为它们是**两个不同的问题**（解法不同，不能混在一起谈）：
      节 1 存量：`JSON 有、元数据没有` → 档案条目已建但界面看不到（如 `丘林`）
      节 2 增量：`卡上有变体名、元数据只有基名` → 解析退化成基名，变体的行被挂到基名条目
    并统计**选择偏差**：嫌疑卡在 gap 工单（低置信度）里 vs 不在的比例。
    """
    names = load_meta_names()
    main, alias = build_index(names)
    ocr_files = sorted(OCR.glob("*/*.txt"))
    gap = {r["take"] for r in _gap_rows()}
    rows, per_tag, per_tag_all = [], {}, {}
    reg = load_registry()
    for p in ocr_files:
        take = "%s/%s" % (p.parent.name, p.stem)
        tag = p.parent.name
        per_tag_all[tag] = per_tag_all.get(tag, 0) + 1
        txt = p.read_text(encoding="utf-8", errors="replace")
        nm, _src = match_title(txt, main, alias)
        if not longer_name_suspect(txt, nm):
            continue
        raw = title_region(txt)
        side_cn = modifier_side(raw, nm) if nm else "none"
        en = english_name(txt)
        per_tag[tag] = per_tag.get(tag, 0) + 1
        rows.append((take, tag, nm or "(None)", side_cn, en, take in gap,
                     (reg.get(take) or {}).get("verdict", "**未登记**")))

    in_gap = sum(1 for r in rows if r[5])
    none_cnt = sum(1 for p in ocr_files
                   if match_title(p.read_text(encoding="utf-8", errors="replace"), main, alias)[0] is None)
    out = ["# 全量扫描（只读）：变体嫌疑卡 + 存量缺失条目", "",
           "扫描 `_ocr/**/*.txt` 全部 **%d** 张卡。" % len(ocr_files), "",
           "## 节 0：概览", "",
           "- 变体嫌疑（卡上真名比匹配名长）：**%d** 张（%.1f%%）" % (len(rows), 100.0 * len(rows) / max(1, len(ocr_files))),
           "- 其中在 gap 工单（低置信度）里的：**%d** 张；不在的：**%d** 张" % (in_gap, len(rows) - in_gap),
           "- 参照：gap 工单共 %d 张 / 全 %d 张 = %.1f%%" % (len(gap), len(ocr_files), 100.0 * len(gap) / max(1, len(ocr_files))),
           "- ⚠️ **盲区**：`match_title` 返回 `None` 的卡 **%d** 张（%.1f%%）——" % (none_cnt, 100.0 * none_cnt / max(1, len(ocr_files))),
           "  没有基名就无从谈「比基名长」，所以这些卡**不可能**被标记。"
           "「某篇 = 0」只能读成「未测」，不能读成「干净」。",
           "- 登记表 `_provenance/variant_registry.json`：**%d** 张" % len(reg), "",
           "### 按篇", "", "| 篇 | 卡片数 | 嫌疑数 | 嫌疑率 |", "|---|---|---|---|"]
    for t in sorted(per_tag_all):
        n = per_tag.get(t, 0)
        out.append("| `%s` | %d | %d | %.0f%% |" % (t, per_tag_all[t], n, 100.0 * n / per_tag_all[t]))
    out += ["", "## 节 1：存量 —— `JSON 有、元数据没有`（= 界面完全看不到的条目）", ""]
    arc = json.loads((ROOT / "Resource/EnemyArchives/EnemyArchives.json").read_text(encoding="utf-8"))
    miss = [e for e in arc if isinstance(e, dict) and e.get("name") not in names]
    out.append("档案条目 %d 个，其中 %d 个不在元数据里：" % (len(arc), len(miss)))
    out += ["", "| name | 状态数 | 有抗性值 |", "|---|---|---|"]
    out += ["| `%s` | %d | %s |" % (e.get("name"), len((e.get("resistances") or {}).get("states") or []),
                                    "是" if (e.get("resistances") or {}).get("values") else "否") for e in miss]
    out += ["", "## 节 2：增量 —— 卡上有变体名、元数据只有基名", "",
            "⚠️ 这类卡会被**挂到基名条目**上，变体的行变成基名的「状态」。",
            "`verdict` 来自登记表（**逐张人眼核对中英修饰语义**，不是抄扫描结果）：",
            "`confirmed` = 中英语义一一对应；`suspect` = 只是子串但非变体；`unclear` = OCR 读不清。", "",
            "| take | 篇 | 匹配到基名 | 中文修饰 | 卡上英文名（旁证） | verdict | 在gap |",
            "|---|---|---|---|---|---|---|"]
    for take, tag, nm, side, en, g, st in rows:
        r = reg.get(take) or {}
        out.append("| `%s` | %s | %s | %s | %s | %s | %s |"
                   % (take, tag, nm, r.get("cn_mod", "—"), en or "—",
                      r.get("verdict", "**未登记**"), "是" if g else ""))
    out += ["", "## 登记表里的 non-confirmed（A1 决策时需区别对待）", ""]
    for r in sorted(reg.values(), key=lambda x: x["take"]):
        if r.get("verdict") != "confirmed":
            out.append("- `%s` = **%s**：%s" % (r["take"], r.get("verdict"), r.get("evidence")))
    out += ["", "## 非 confirmed 卡的标题区原文（看中英版式）", ""]
    for take, tag, nm, side, en, g, st in rows:
        if st == "confirmed":
            continue
        p = OCR / take.split("/")[0] / (take.split("/")[1] + ".txt")
        lines = [ln.strip() for ln in title_region(p.read_text(encoding="utf-8", errors="replace")).splitlines() if ln.strip()]
        out.append("- `%s` → 基名 `%s`：%s" % (take, nm, " ／ ".join(lines)))
    (GEN / "variant_scan.md").write_text("\n".join(out), encoding="utf-8")
    print("扫描 %d 张；嫌疑 %d 张（其中 gap 内 %d / 外 %d）\n报告 -> %s"
          % (len(ocr_files), len(rows), in_gap, len(rows) - in_gap, GEN / "variant_scan.md"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--scan-all", action="store_true", help="全量只读扫描（变体嫌疑 + 存量缺失）")
    a = ap.parse_args()
    if a.selftest:
        return 0 if selftest() else 1
    if a.report:
        report()
        return 0
    if a.scan_all:
        scan_all()
        return 0
    ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
