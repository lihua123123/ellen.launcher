# -*- coding: utf-8 -*-
"""任意篇的复核表：卡片抗性行 × 8 项 vs 元数据，输出供扫读确认。

用法：python table_all.py [tag ...]      省略则跑 _rescue/*_states.json 全部
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))

ROOT = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher")
META = ROOT / "Resource/Metadata/Genshin/CHS/Monster.json"
OCR = HERE / "_ocr"
GEN = HERE / "_gen2"

ORDER = [("pyro", "火", "FireSubHurt"), ("hydro", "水", "WaterSubHurt"),
         ("anemo", "风", "WindSubHurt"), ("electro", "雷", "ElecSubHurt"),
         ("dendro", "草", "GrassSubHurt"), ("cryo", "冰", "IceSubHurt"),
         ("geo", "岩", "RockSubHurt"), ("physical", "物", "PhysicalSubHurt")]
CN = [c for _, c, _ in ORDER]
STATE_CHARS = set("正常眩晕带盾常驻")
BAD_SUFFIX = ("武士", "历经百战", "部族首领", "山王", "暴君", "深邃拟态")

meta_by_name = {}
for m in json.loads(META.read_text(encoding="utf-8")):
    if m.get("Name"):
        meta_by_name.setdefault(m["Name"], m)


def pick_name(lines):
    """名称在首行的前导中文串；首行可能是状态标题或 HP 行，需跳过。"""
    for ln in lines[:4]:
        m = re.match(r"^([\u4e00-\u9fff·]+)", ln.strip())
        if not m:
            continue
        n = m.group(1)
        if not n or all(c in STATE_CHARS for c in n):
            continue
        if n in ("抗性数据", "元素能量"):
            continue
        return re.sub(r"[，,。\s]+$", "", n)
    return re.sub(r"[，,。\s]+$", "", lines[0])


def lookup(name):
    """精确匹配失败时按「共同后缀 + 幼前缀对齐」模糊解析（OCR 常丢首字）。"""
    if name in meta_by_name:
        return name

    def score(n):
        if "·" in n or any(k in n for k in BAD_SUFFIX):
            return None
        k = 0
        while k < min(len(n), len(name)) and n[-1 - k] == name[-1 - k]:
            k += 1
        if k < 2:
            return None
        pen = abs(len(n) - len(name))
        if n.startswith("幼") != name.startswith("幼"):
            pen += 3
        return (pen, -k)

    return min((n for n in meta_by_name if score(n)), key=score, default=None)


def norm_key(s):
    """归一化：只保留中日韩字与字母数字，去掉 ·／空格／标点。

    卡面常把名字拆成两行（「愚人众先遣队」+「火游击兵」），而元数据是
    「愚人众先遣队·火游击兵」→ 去掉分隔符后即可逐字对上。
    """
    return re.sub(r"[^\u4e00-\u9fffA-Za-z0-9]", "", s)


NORM_INDEX = sorted(((norm_key(n), n) for n in meta_by_name if len(norm_key(n)) >= 3),
                    key=lambda t: -len(t[0]))


# 第 0a 步：名称解析改为委托 `name_resolve`。导入失败时回退旧行为（避免整个管线起不来）。
try:
    import name_resolve  # noqa: E402

    _MAIN, _ALIAS = name_resolve.build_index(list(meta_by_name))
except Exception:  # pragma: no cover
    name_resolve = None
    _MAIN = _ALIAS = {}


def find_name_in_text(text):
    """在整卡 OCR 文本里找元数据名。

    ⚠️ 已改为委托 `name_resolve.match_title`（第 0a 步）。旧实现是「**全文**最长子串」，
    有两个实测反例：
      - `12/047`（卡是「虹彩史莱姆」）从**注释**里捞到 `草史莱姆`（出现位置 1282）
      - `05/030`（卡是「深邃墓结株」）捞到 `贪食匿叶龙山王`（位置 1147）
    新实现：① 只在**标题区**（`Data`/`HP:` 之前）匹配；② **位置优先**，其次最长
    （旧实现按长度排序，等长时谁赢看字典序 → `04/010` 的 `吞雷`/`原种` 就是这么飘的）；
    ③ 带前缀的元数据名（`历经百战的…`）作为**别名回退层**，不参与主索引排序。

    自证：`python name_resolve.py --selftest`（已知答案 + 6 种索引顺序下确定性 + 碰撞守卫）。
    """
    if name_resolve is not None:
        got, _src = name_resolve.match_title(text, _MAIN, _ALIAS)
        return got
    nt = norm_key(text)
    for key, name in NORM_INDEX:
        if key in nt:
            return name
    return None


def resolve_name(lines):
    """返回（卡面显示名, 元数据名）。"""
    text = "\n".join(lines)
    mid = find_name_in_text(text)
    disp = pick_name(lines)
    if not mid:
        mid = lookup(disp)
    if mid and (not disp or disp not in mid):
        disp = mid
    return disp, mid


def fmt(v):
    return v if v == "IMM" else (f"{v}%" if v else "-")


def meta_vals(mid):
    if not mid:
        return None
    bv = meta_by_name[mid].get("BaseValue") or {}
    return [round((bv.get(f) or 0) * 100) for _, _, f in ORDER]


def build(tag):
    states = json.loads((HERE / "_rescue" / f"{tag}_states.json").read_text(encoding="utf-8"))
    out, diffs = [], []
    stat = {"same": 0, "diff": 0, "miss": 0}
    for item in sorted(states, key=lambda r: r["src"]):
        _, idx = item["src"].split("/")
        txt = (OCR / tag / f"{idx}.txt").read_text(encoding="utf-8")
        lines = [ln.strip() for ln in txt.splitlines() if ln.strip()]
        name, mid = resolve_name(lines)
        mv = meta_vals(mid)
        out.append(f"== {idx} {name}" + ("" if mid else "   ⚠ 元数据缺失"))
        if not mid:
            stat["miss"] += 1
        for row in item.get("rows", []):
            vals = row["values"]
            out.append(f"   [{row['label'] or '常态'}] " +
                       " ".join(f"{CN[i]}={fmt(v)}" for i, v in enumerate(vals)))
            if mv:
                same = sum(1 for i, v in enumerate(vals) if fmt(v) == f"{mv[i]}%")
                out.append("        元数据 " + " ".join(f"{CN[i]}={mv[i]}%" for i in range(8))
                           + f"   → {'全同' if same == 8 else f'差异 {8 - same}/8'}")
                if same == 8:
                    stat["same"] += 1
                else:
                    stat["diff"] += 1
                    diffs.append({"src": item["src"], "idx": idx, "name": name, "meta": mid,
                                  "label": row["label"] or "常态", "values": vals,
                                  "confident": bool(row.get("confident", False))})
        if item.get("note_text"):
            out.append(f"   note: {item['note_text']}")
    head = (f"{tag} 篇 cards={len(states)}；行对比：全同 {stat['same']}、有差异 {stat['diff']}、"
            f"元数据缺失 {stat['miss']}")
    (GEN / f"table_{tag}.txt").write_text(head + "\n\n" + "\n".join(out), encoding="utf-8")
    return head, diffs


def main():
    tags = sys.argv[1:]
    if not tags:
        tags = sorted(p.stem.replace("_states", "") for p in (HERE / "_rescue").glob("*_states.json"))
    all_diffs = []
    for tag in tags:
        head, diffs = build(tag)
        all_diffs += diffs
        print(f"{head}   diff条目={len(diffs)}")
    (GEN / "diffs_all.json").write_text(json.dumps(all_diffs, ensure_ascii=False, indent=1),
                                        encoding="utf-8")
    print("total diff rows:", len(all_diffs))


if __name__ == "__main__":
    main()
