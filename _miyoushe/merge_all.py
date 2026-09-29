# -*- coding: utf-8 -*-
"""通用入库：把 table_all.py 产出的 diff 清单落成档案条目。

规则（与 01/03 一致）：
  - 只收「与元数据有差异」的抗性行（diff 门槛）；
  - 名称用**元数据名**（保证能被应用匹配到），OCR 名进 aliases；
  - 一卡多行 → values 取常态行，其余进 resistances.states；
  - 抗性来自裁图高精度通道；技能/机制/产球来自文本解析 → 标注「待核对」；
  - 已在档案里的名字直接跳过（保留既有质量更高的版本）。
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))
import parse_cards2 as P  # noqa: E402
import table_all as T  # noqa: E402

ARCH = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher"
            r"\Resource\EnemyArchives\EnemyArchives.json")
GEN = HERE / "_gen2"

CN2KEY = {"火": "pyro", "水": "hydro", "风": "anemo", "雷": "electro",
          "草": "dendro", "冰": "cryo", "岩": "geo", "物": "physical"}
ORDER = ["火", "水", "风", "雷", "草", "冰", "岩", "物"]
DEFAULT_LABELS = ("正常", "常态", "")
CJK = re.compile(r"[\u4e00-\u9fff]")
ENERGY_IN_TEXT = re.compile(r"[A-Za-z]{1,2}[PpOo0]\s*\[[0-9,\s]+\]")
ENERGY_ANY = re.compile(r"[A-Za-z]\s*[PpOo]\s*\[[0-9,\s]+\]")
# 不纳入入库的篇（13 篇本地 OCR 确定性失败，见 README / 仓库记忆）
EXCLUDE_TAGS = {"13"}
# 已人工验证过的篇：其低置信行也收（01 篇曾逐格核对、03 篇与旧流程零差异）
VERIFIED_TAGS = {"01", "03"}
# 卡面实测合法区间：负值到 -100、高值到 370+（N5 态）；仍要求 5 的倍数以拦 `-5050`/`4`/`99` 这类误读
# 取值合理区间（5 的倍数另判）。
# ⚠️ 26/9/30 由 [-100,500] 放宽：目视已证实卡面存在 -140（11/017 核心行）
# 与 9.9k = 9900/9950（14/014 辅助行，卡面用 k 简写）。旧区间会把它们当垃圾丢掉。
# 仍能拦住真正的垃圾：如 05/030 的 -5050（粘连值）与 11/013 的 4（非 5 倍数）。
LOW, HIGH = -200, 10000


def plausible(vals):
    """所有值必须完整、且是 5 的倍数且在允许区间（否则宁可丢也不入库）。"""
    if len(vals) != 8 or any(not v for v in vals):
        return False
    for v in vals:
        if v == "IMM":
            continue
        try:
            n = int(v)
        except (TypeError, ValueError):
            return False
        if n % 5 or not LOW <= n <= HIGH:
            return False
    return True


def sanitize(desc):
    """去掉描述里误吞的元素能量记法（如 `WP[75,50,25]`）。"""
    d = ENERGY_IN_TEXT.sub("", desc)
    d = re.sub(r"\s{2,}", " ", d).strip(" ，,。-")
    return d


def valid_energy(e):
    if not isinstance(e, dict) or not e.get("rows"):
        return False
    for r in e["rows"]:
        if not isinstance(r.get("hp"), int) or not 0 <= r["hp"] <= 100:
            return False
        if r.get("particle") not in (0, 1, 2, 3) or r.get("orb") not in (0, 1, 2, 3):
            return False
    return True


def to_values(vals):
    return {CN2KEY[c]: ("IMM" if v == "IMM" else f"{v}%") for c, v in zip(ORDER, vals)}


def text_fields(tag, idx):
    """从整卡 OCR 取技能/机制/元素能量（文本通道，待核对）。"""
    txt = (T.OCR / tag / f"{idx}.txt").read_text(encoding="utf-8")
    lines = [ln.strip() for ln in txt.splitlines() if ln.strip()]
    joined = "\n".join(lines)
    skills = []
    try:
        for s in P.parse_skills(joined):
            nm = (s.get("name") or "").strip()
            desc = sanitize(s.get("description") or "")
            if len(CJK.findall(nm)) >= 2 and len(desc) >= 4:
                item = {"name": nm, "description": desc}
                if s.get("tag"):
                    item["tag"] = s["tag"]
                skills.append(item)
    except Exception:
        skills = []
    mechanics = []
    try:
        _, notes = P.parse_resistance_block(lines)
        notes = [n for n in (sanitize(x) for x in notes) if len(n) >= 4]
        if notes:
            mechanics = [{"title": "", "text": " ".join(notes)}]
    except Exception:
        mechanics = []
    energy, energy_raw = None, ""
    try:
        energy, _ = P.parse_energy(joined, "")
    except Exception:
        energy = None
    if not valid_energy(energy):
        m = ENERGY_ANY.search(joined)
        energy_raw = m.group(0) if m else ""
        energy = None
    return skills, mechanics, energy, energy_raw


def main():
    diffs = json.loads((GEN / "diffs_all.json").read_text(encoding="utf-8"))
    archive = json.loads(ARCH.read_text(encoding="utf-8"))
    by_name = {e.get("name"): e for e in archive}

    # 按「元数据名」分组：同一怪物的多状态可能被拆到多张卡，跨卡合并而不是丢弃
    grouped = {}
    dropped = []
    for d in diffs:
        key = d.get("meta") or ""
        if not key:
            continue
        tag = d["src"].split("/")[0]
        if tag in EXCLUDE_TAGS:
            dropped.append(f"{d['src']} 排除篇 {tag}")
            continue
        if not d.get("confident") and tag not in VERIFIED_TAGS:
            dropped.append(f"{d['src']} 低置信行（横条非 8 独立块，可能有错列）")
            continue
        if not plausible(d["values"]):
            dropped.append(f"{d['src']} 取值不合理 {d['values']}")
            continue
        grouped.setdefault(key, []).append(d)

    added, skipped, review, no_meta = [], [], [], []
    for meta_name, rows in sorted(grouped.items()):
        if meta_name in by_name:
            skipped.append(meta_name)
            continue
        srcs = sorted({r["src"] for r in rows})
        if len(srcs) > 1:
            review.append(f"⚠ {meta_name}: {len(srcs)} 张卡归到同一元数据名（{', '.join(srcs)}），已合并")

        # 常态行优先；多卡时取第一张卡的常态行
        base = next((r for r in rows if r["label"] in ("常态", "")), None) \
            or next((r for r in rows if r["label"] == "正常"), None) or rows[0]
        # 状态行必须同样合理，否则丢弃该状态
        states = [r for r in rows if r is not base and plausible(r["values"])
                  and (r.get("confident") or tag in VERIFIED_TAGS)]
        if not plausible(base["values"]):
            dropped.append(f"{base['src']} 常态行不合理")
            continue

        note = ""
        for src in srcs:
            tag, idx = src.split("/")
            item = json.loads((HERE / "_rescue" / f"{tag}_states.json").read_text(encoding="utf-8"))
            note = next((x.get("note_text", "") for x in item if x["src"] == src), "") or note

        tag, idx = base["src"].split("/")
        skills, mechanics, energy, energy_raw = text_fields(tag, idx)
        entry = {"name": meta_name, "aliases": sorted({meta_name} | {r["name"] for r in rows}),
                 "type": "普通",
                 "resistances": {"values": to_values(base["values"]),
                                 "states": [{"name": s["label"] or "状态",
                                             "condition": note,
                                             "values": to_values(s["values"])} for s in states]}}
        if note:
            entry["resistances"]["defaultNote"] = note
        if mechanics:
            entry["mechanics"] = mechanics
        if skills:
            entry["skills"] = skills
        if energy:
            entry["energy"] = energy

        by_name[meta_name] = entry
        added.append((base["src"], meta_name, len(states), len(skills)))
        review.append(f"{base['src']} {meta_name}: skills={len(skills)} mechanics={len(mechanics)} "
                      f"energy={'y' if energy else 'n'}（文本解析，未人工核对）")
        if energy_raw:
            review.append(f"{base['src']} {meta_name}: ⚠ 能量未解析，原文 {energy_raw}")

    ARCH.write_text(json.dumps(list(by_name.values()), ensure_ascii=False, indent=2) + "\n",
                    encoding="utf-8")
    report = [f"新增 {len(added)} 条；跳过（已存在）{len(skipped)}；无元数据名 {len(no_meta)}",
              f"档案总数 {len(by_name)}", "", "新增："]
    report += [f"  {s:<10} {n:<16} states={st} skills={sk}" for s, n, st, sk in added]
    report += ["", "=== 待核对（文本通道）==="] + review
    if no_meta:
        report += ["", "无元数据名（未入库）: " + ", ".join(no_meta)]
    if dropped:
        report += ["", f"=== 被守卫丢弃（{len(dropped)}）==="] + dropped
    (GEN / "merge_all.txt").write_text("\n".join(report), encoding="utf-8")
    print(f"added={len(added)} skipped={len(skipped)} no_meta={len(no_meta)} total={len(by_name)}")


if __name__ == "__main__":
    main()
