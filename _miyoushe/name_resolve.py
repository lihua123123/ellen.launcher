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
    okall = not BAD
    say("=== 结论：%s ===" % ("PASS" if okall else "FAIL"))
    for b in BAD:
        say("  !! " + b)
    return okall


def _gap_rows():
    return [r for r in json.loads((GEN / "lowconf_worklist.json").read_text(encoding="utf-8"))
            if r.get("scope") == "gap" and r.get("meta")]


def report():
    """第 0b 步：79 张 gap 卡的匹配结果（只读，不写任何数据）。"""
    names = load_meta_names()
    main, alias = build_index(names)
    rows = _gap_rows()
    cur = {}
    for r in rows:
        cur[r["take"]] = r["meta"]
    out = ["# 第 0b 步（只读）：79 张 gap 卡的名称解析结果", "",
           "| take | 当前条目 | 新规则结果 | 变化 | 来源 | 备注 |", "|---|---|---|---|---|---|"]
    stat = {"改名": [], "None": [], "不变": []}
    for take in sorted(cur):
        got, src = match_title(_ocr(take), main, alias)
        if got is None:
            ch = "**None**"; note = "需人眼分类（别名可救/OCR漏字/真名缺元数据/fuzzy误匹配）"
            stat["None"].append(take)
        elif got != cur[take]:
            ch = "**改名**"; note = ""
            stat["改名"].append((take, cur[take], got))
        else:
            ch = ""; note = ""; stat["不变"].append(take)
        if take in AMBIGUOUS:
            note = (note + " " if note else "") + "**一张卡两种怪**"
        out.append("| `%s` | %s | %s | %s | %s | %s |"
                   % (take, cur[take], got or "None", ch, src or "-", note))
    out += ["", "- 改名 **%d** / None **%d** / 不变 **%d**" % (len(stat["改名"]), len(stat["None"]), len(stat["不变"])), ""]
    out += ["## 改名明细", "", "| take | 当前 | 新 |", "|---|---|---|"]
    out += ["| `%s` | %s | %s |" % d for d in stat["改名"]]
    out += ["", "## None 明细", ""] + ["- `%s`" % t for t in stat["None"]]
    (GEN / "name_report.md").write_text("\n".join(out), encoding="utf-8")
    print("报告 -> %s（改名 %d / None %d / 不变 %d）"
          % (GEN / "name_report.md", len(stat["改名"]), len(stat["None"]), len(stat["不变"])))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--report", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return 0 if selftest() else 1
    if a.report:
        report()
        return 0
    ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
