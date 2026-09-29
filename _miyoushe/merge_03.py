# -*- coding: utf-8 -*-
"""03 篇入库：只收与元数据有差异的 7 条（diff 门槛）。

抗性来自裁图通道（高置信）；技能/机制/产球来自整卡文本解析 → 标「待核对」。
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))
import parse_cards2 as P  # noqa: E402
import table_03 as T  # noqa: E402

ARCHIVE = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher"
               r"\Resource\EnemyArchives\EnemyArchives.json")
STATES = json.loads((HERE / "_rescue" / "03_states.json").read_text(encoding="utf-8"))

CN2KEY = {"火": "pyro", "水": "hydro", "风": "anemo", "雷": "electro",
          "草": "dendro", "冰": "cryo", "岩": "geo", "物": "physical"}
ORDER = ["火", "水", "风", "雷", "草", "冰", "岩", "物"]

# 需要入库的 7 张（图号 → 状态行用途）
INCLUDE = {"001": ("常态", "眩晕"), "002": ("常态", "眩晕"), "003": ("常态", "眩晕"),
           "017": ("常态", None),
           "028": ("常态", "带盾"), "029": ("常态", "带盾"), "030": ("常态", "带盾")}


def values_to_dict(vals):
    return {CN2KEY[cn]: ("IMM" if v == "IMM" else f"{v}%") for cn, v in zip(ORDER, vals)}


def build(idx):
    item = next(x for x in STATES if x["src"] == f"03/{idx}")
    rows = {row["label"]: row["values"] for row in item["rows"]}
    _, state_label = INCLUDE[idx]
    default_vals = rows.get("正常") or rows.get("") or next(iter(rows.values()))

    entry = {"name": "", "aliases": [], "type": "普通",
             "resistances": {"values": values_to_dict(default_vals), "states": [],
                             "defaultNote": item.get("note_text", "")}}

    meta_name = T.lookup(T.pick_name([l.strip() for l in
                                      (HERE / "_ocr" / "03" / f"{idx}.txt").read_text(
                                          encoding="utf-8").splitlines() if l.strip()]))
    entry["name"] = meta_name or ""
    entry["aliases"] = [entry["name"]]

    if state_label and state_label in rows:
        entry["resistances"]["states"] = [{
            "name": state_label,
            "condition": item.get("note_text", ""),
            "values": values_to_dict(rows[state_label]),
        }]
    return entry


def main():
    out = []
    for idx in sorted(INCLUDE):
        e = build(idx)
        out.append(e)
        ns = len(e["resistances"]["states"])
        out[-1]["_src"] = f"03/{idx}"
        print(f"{idx} {e['name']} states={ns} default="
              f"{list(e['resistances']['values'].values())}")
    (HERE / "_gen2" / "merge_03.json").write_text(json.dumps(out, ensure_ascii=False, indent=1),
                                                  encoding="utf-8")
    print("written merge_03.json", len(out))


if __name__ == "__main__":
    main()
