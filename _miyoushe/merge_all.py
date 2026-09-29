# -*- coding: utf-8 -*-
r"""通用入库：把 table_all.py 产出的 diff 清单落成档案条目。

规则（与 01/03 一致）：
  - 只收「与元数据有差异」的抗性行（diff 门槛）；
  - 名称用**元数据名**（保证能被应用匹配到），OCR 名进 aliases；
  - 一卡多行 → values 取常态行，其余进 resistances.states；
  - 抗性来自裁图高精度通道；技能/机制/产球来自文本解析 → 标注「待核对」；
  - 已在档案里的名字直接跳过（保留既有质量更高的版本）。

⚠️ `condition` 为什么在生成期就要清洗
    状态行的 `condition` 取的是本地 OCR 抓的注释区文本，其中绝大多数是**表格碎片**
    （形如 `10-40 70 20 10-40 ...`、`士气 70 70 70 ...`）。`EnemyArchiveResistanceState.Condition`
    在全仓**没有任何消费者**（死字段），但把 OCR 碎片当成「触发条件」写进档案是错的。
    清洗规则见 `split_condition()`，判据与 `clean_conditions.py` 一致：
    命中「`\d-\d` 粘连」**或**「数字 ≥ 6 个」→ `condition` 置 `null`，原文转入 `condition_ocr_raw`。
    它**必须在这里**做：`finalize.py` 每轮都会重建这些条目，做成一次性补丁会被冲掉
    （踩过：`clean_conditions.py` 手工跑完，重跑 finalize 后 74 条 condition 全部变回 OCR 原文）。
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


# evidence 强度序（越大越强），去重时保留更强的一档
EV_STRENGTH = {"cal-gold": 3, "2geo": 2, "eyeball": 1, "single-read": 0}


def dedupe_states(states, src, review):
    """同名 + 同 8 值 → 只留一条；同名不同值 → 全部保留并告警。返回去重后的列表。

    ⚠️ 为什么需要：worklist / diffs_all 里存在**重复行**（实测 `04/030 r1` 的「带甲」出现两次），
    而本函数上游是「一行建一个状态」→ 同一个抗性行被写成多个**完全相同**的状态
    （实测 `魔像督军` 出现 3 个一模一样的「带甲」，全部 70%×8）。
    重复行在界面上的代价是多一个无法区分的 Tab（换 Segmented 后尤其明显）。

    ⚠️ 为什么**不能**按名字一刀切去重：`古岩龙蜥` 的「起身」×2、`深海龙蜥·原种` 的「幼体」×3
    都是**合法的同名不同值**（卡面确实同名，但数值不同），必须保留。

    保留更强的一档 evidence：同值行若一个走了 VLM 通道、另一个没走，应留下有等级的那个。
    """
    kept, index, dropped = [], {}, 0
    for r in states:
        key = (r["label"] or "状态", tuple(str(v) for v in r["values"]))
        if key in index:
            prev = kept[index[key]]
            if EV_STRENGTH.get(r.get("evidence"), -1) > EV_STRENGTH.get(prev.get("evidence"), -1):
                prev["evidence"] = r.get("evidence")
            dropped += 1
            continue
        index[key] = len(kept)
        kept.append(r)

    if dropped:
        review.append("%s 同名同值状态去重 %d 条（重复行）" % (src, dropped))

    byname = {}
    for r in kept:
        byname.setdefault(r["label"] or "状态", []).append(r)
    for name, group in byname.items():
        if len(group) > 1:
            vals = [" ".join(str(v) for v in r["values"]) for r in group]
            review.append("⚠ %s 状态名「%s」出现 %d 次且数值不同（合法重名，已保留）：%s"
                          % (src, name, len(group), vals))
    return kept


def split_condition(note):
    """把注释区串切成 (可用条件, OCR 原文)；判据同 clean_conditions.py。

    返回 (None, note) 表示这是 OCR 碎片，只能进 condition_ocr_raw；
    返回 (note, None) 表示这是干净的人话，可以直接做 condition。
    """
    if not note:
        return None, None
    s = str(note)
    if re.search(r"\d+\s*-\s*\d+", s) or len(re.findall(r"\d", s)) >= 6:
        return None, note
    return note, None


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
        # 同名同值去重（重复行）；同名不同值保留并告警
        states = dedupe_states(states, base["src"], review)
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
        condition, condition_raw = split_condition(note)
        # evidence：每一行（常态 + 每个状态）各自的读值证据等级。
        # 由 derive_evidence.py 推导、gap_override.py 携带；缺省不写（如 01/03 篇的
        # 豁免行、以及档案里 136 条来源不明的基线行）。
        state_items = []
        for s in states:
            item = {"name": s["label"] or "状态",
                    "condition": condition,
                    "values": to_values(s["values"])}
            if condition_raw:
                item["condition_ocr_raw"] = condition_raw
            if s.get("evidence"):
                item["evidence"] = s["evidence"]
            state_items.append(item)
        entry = {"name": meta_name, "aliases": sorted({meta_name} | {r["name"] for r in rows}),
                 "type": "普通",
                 "resistances": {"values": to_values(base["values"]),
                                 "states": state_items}}
        if base.get("evidence"):
            entry["resistances"]["evidence"] = base["evidence"]
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
