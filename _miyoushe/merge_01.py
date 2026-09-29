# -*- coding: utf-8 -*-
"""合辑一入库：抗性取自裁图补偿结果，技能/机制/产球取自文本解析。

合并策略：
  - 已手工逐字核过的 5 条（火史莱姆/水史莱姆/雷史莱姆/大型火史莱姆/火飘浮灵）**保留原样**（质量更高）；
  - 其余 17 条由流水线生成并新增；
  - 文本解析字段的可信度独立于抗性，故另出一份待核对清单（模型里没有 confidence 字段）。
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))
import parse_cards2 as P  # noqa: E402

ARCHIVE = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher"
               r"\Resource\EnemyArchives\EnemyArchives.json")
RESCUE = json.loads((HERE / "_rescue" / "01.json").read_text(encoding="utf-8"))

ORDER = [("pyro", "火"), ("hydro", "水"), ("anemo", "风"), ("electro", "雷"),
         ("dendro", "草"), ("cryo", "冰"), ("geo", "岩"), ("physical", "物")]

HAND_MADE = {"火史莱姆", "水史莱姆", "雷史莱姆", "大型火史莱姆", "火飘浮灵"}


def build_from_ocr(item):
    tag, idx = item["src"].split("/")
    txt = (HERE / "_ocr" / tag / f"{idx}.txt").read_text(encoding="utf-8")
    lines = [ln.strip() for ln in txt.splitlines() if ln.strip()]
    name = re.split(r"[A-Za-z0-9(（:：]", lines[0])[0].strip(" ·「」")
    if len(lines) > 1 and lines[1].startswith("·"):
        name = name + re.split(r"[A-Za-z0-9(（:：]", lines[1])[0].strip(" ·")

    values = item.get("values", [])
    resist = {}
    for i, key_cn in enumerate(ORDER):
        v = values[i] if i < len(values) else None
        resist[key_cn[0]] = "IMM" if v == "IMM" else (f"{v}%" if v else "10%")

    _, notes = P.parse_resistance_block(lines)
    energy, _ = P.parse_energy("\n".join(lines), name)
    skills = P.parse_skills("\n".join(lines))

    entry = {
        "name": name,
        "aliases": [name],
        "type": next((k for k in ("普通", "精英", "首领", "地方传奇") if k in "\n".join(lines[:6])), "普通"),
        "resistances": {"values": resist, "states": []},
        "mechanics": [{"title": "", "text": " ".join(notes)}] if notes else [],
        "skills": [{"name": s["name"], "tag": s["tag"], "description": s["description"]} for s in skills],
    }
    if energy:
        entry["energy"] = energy
    return entry


def main():
    existing = json.loads(ARCHIVE.read_text(encoding="utf-8"))
    by_name = {e.get("name"): e for e in existing}

    added, kept, review = [], [], []
    for item in sorted(RESCUE, key=lambda r: r["src"]):
        entry = build_from_ocr(item)
        name = entry["name"]
        if name in HAND_MADE and name in by_name:
            kept.append(name)
            continue
        by_name[name] = entry
        added.append((name, len(entry["skills"]), len(entry["mechanics"]),
                      "energy" if "energy" in entry else "-"))
        if entry["skills"]:
            review.append(f"{name}: 技能 {len(entry['skills'])} 条（文本解析，未人工核对）")

    merged = list(by_name.values())
    ARCHIVE.write_text(json.dumps(merged, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    out = [f"入库后共 {len(merged)} 条；本次保留手工条 {len(kept)} 条、新增 {len(added)} 条", "",
           "新增条目（名称 / 技能数 / 机制数 / 产球）:"]
    out += [f"  {n:<14} {s:<3} {m:<2} {e}" for n, s, m, e in added]
    (HERE / "_gen2" / "merge_01.txt").write_text("\n".join(out + ["", "=== 待核对清单（文本解析通道）==="] + review),
                                                encoding="utf-8")
    print(f"merged={len(merged)} kept={len(kept)} added={len(added)}")
    print("added names:", ", ".join(n for n, *_ in added))


if __name__ == "__main__":
    main()
