# -*- coding: utf-8 -*-
"""03 篇（骗骗花）42 张抗性表复核：卡片行 × 8 项 vs 元数据 8 项。"""
import json
import re
from pathlib import Path

HERE = Path(__file__).parent
META = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher\Resource"
            r"\Metadata\Genshin\CHS\Monster.json")
STATES = json.loads((HERE / "_rescue" / "03_states.json").read_text(encoding="utf-8"))

ORDER = [("pyro", "火", "FireSubHurt"), ("hydro", "水", "WaterSubHurt"),
         ("anemo", "风", "WindSubHurt"), ("electro", "雷", "ElecSubHurt"),
         ("dendro", "草", "GrassSubHurt"), ("cryo", "冰", "IceSubHurt"),
         ("geo", "岩", "RockSubHurt"), ("physical", "物", "PhysicalSubHurt")]
CN = [c for _, c, _ in ORDER]

meta_by_name = {}
for m in json.loads(META.read_text(encoding="utf-8")):
    if m.get("Name"):
        meta_by_name.setdefault(m["Name"], m)


def lookup(name):
    """精确匹配失败时按「共同后缀 + 幼前缀对齐」做模糊解析（OCR 常丢首字）。"""
    if name in meta_by_name:
        return name

    def score(n):
        if "·" in n or "武士" in n or "历经百战" in n:
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


def fmt(v):
    return v if v == "IMM" else (f"{v}%" if v and not v.endswith("%") else (v or "-"))


STATE_CHARS = set("正常眩晕带盾")


def pick_name(lines):
    """名称在首行的前导中文串；骗骗花卡首行是状态标题（正常/眩晕/带盾），故跳过。"""
    for ln in lines[:3]:
        m = re.match(r"^([\u4e00-\u9fff·]+)", ln.strip())
        if not m:
            continue
        n = m.group(1)
        if not n or all(c in STATE_CHARS for c in n):
            continue
        return re.sub(r"[，,。\s]+$", "", n)
    return re.sub(r"[，,。\s]+$", "", lines[0])


out = []
stat = {"diff": 0, "same": 0, "miss": 0}
for item in sorted(STATES, key=lambda r: r["src"]):
    tag, idx = item["src"].split("/")
    txt = (HERE / "_ocr" / tag / f"{idx}.txt").read_text(encoding="utf-8")
    lines = [ln.strip() for ln in txt.splitlines() if ln.strip()]
    name = pick_name(lines)

    meta = meta_by_name.get(lookup(name) or "")
    meta_vals = []
    if meta:
        bv = meta.get("BaseValue") or {}
        meta_vals = [round((bv.get(f) or 0) * 100) for _, _, f in ORDER]
    else:
        stat["miss"] += 1

    alias = "" if lookup(name) in (None, name) else f"（→ {lookup(name)}）"
    out.append(f"== {idx} {name}{alias}" + ("" if meta else "   ⚠ 元数据缺失"))
    for row in item.get("rows", []):
        vals = row["values"]
        out.append(f"   [{row['label'] or '常态'}] " +
                   " ".join(f"{CN[i]}={fmt(v)}" for i, v in enumerate(vals)))
        if meta_vals:
            same = sum(1 for i, v in enumerate(vals) if fmt(v) == f"{meta_vals[i]}%")
            verdict = "全同" if same == 8 else f"差异 {8 - same}/8"
            if same != 8:
                stat["diff"] += 1
            else:
                stat["same"] += 1
            out.append(f"        元数据 " + " ".join(f"{CN[i]}={meta_vals[i]}%" for i in range(8))
                       + f"   → {verdict}")
    if item.get("note_text"):
        out.append(f"   note: {item['note_text']}")

head = (f"03 骗骗花 共 {len(STATES)} 张；状态行与元数据对比：全同 {stat['same']}、有差异 {stat['diff']}、"
        f"元数据缺失 {stat['miss']}")
(HERE / "_gen2" / "table_03.txt").write_text(head + "\n\n" + "\n".join(out), encoding="utf-8")
print(head)
