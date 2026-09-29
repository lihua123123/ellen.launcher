# -*- coding: utf-8 -*-
"""合辑一 22 张的复核对照表：图号 / 名称 / IMM 位置 / IMM 元素 / 元数据同项 / 是否 diff。

抗性取「裁图补偿」的结果（_rescue/01.json），名称与元数据做匹配后取对应字段。
输出 UTF-8 文本，供扫一眼批量确认。
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))
import parse_cards2 as P  # noqa: E402

META = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher\Resource"
            r"\Metadata\Genshin\CHS\Monster.json")
RESCUE = json.loads((HERE / "_rescue" / "01.json").read_text(encoding="utf-8"))

# 卡片顺序：火水风雷草冰岩物
ORDER = [("pyro", "火", "FireSubHurt"), ("hydro", "水", "WaterSubHurt"),
         ("anemo", "风", "WindSubHurt"), ("electro", "雷", "ElecSubHurt"),
         ("dendro", "草", "GrassSubHurt"), ("cryo", "冰", "IceSubHurt"),
         ("geo", "岩", "RockSubHurt"), ("physical", "物", "PhysicalSubHurt")]

meta_by_name = {}
for m in json.loads(META.read_text(encoding="utf-8")):
    if m.get("Name"):
        meta_by_name.setdefault(m["Name"], m)

rows = []
diffs = 0
for item in sorted(RESCUE, key=lambda r: r["src"]):
    tag, idx = item["src"].split("/")
    txt = (HERE / "_ocr" / tag / f"{idx}.txt").read_text(encoding="utf-8")
    lines = [ln.strip() for ln in txt.splitlines() if ln.strip()]
    name = re.split(r"[A-Za-z0-9(（:：]", lines[0])[0].strip(" ·「」")

    values = item.get("values", [])
    imm_pos = next((i + 1 for i, v in enumerate(values) if v == "IMM"), None)
    key, cn, meta_field = ORDER[imm_pos - 1] if imm_pos else (None, "?", None)

    meta = meta_by_name.get(name)
    if meta is None:
        meta_txt = "元数据缺失"
        is_diff = "diff(缺)"
    else:
        mv = (meta.get("BaseValue") or {}).get(meta_field)
        meta_txt = f"{round(mv * 100)}%" if isinstance(mv, (int, float)) else "-"
        is_diff = "diff" if meta_txt != "IMM" else "same"
    if is_diff != "same":
        diffs += 1
    rows.append((idx, name, imm_pos, cn, meta_txt, is_diff))

out = [f"合辑一 22 张抗性对照（卡片顺序：火水风雷草冰岩物）  有 diff 的共 {diffs}/22", ""]
out.append(f"{'图号':<5}{'名称':<14}{'IMM位':<6}{'IMM元素':<8}{'元数据同项':<12}{'结论'}")
for r in rows:
    out.append(f"{r[0]:<5}{r[1]:<14}{r[2]:<6}{r[3]:<8}{r[4]:<12}{r[5]}")

(HERE / "_gen2" / "table_01.txt").write_text("\n".join(out), encoding="utf-8")
print(f"rows={len(rows)} diffs={diffs}")
print("\n".join(out))