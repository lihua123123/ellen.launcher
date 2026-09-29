# -*- coding: utf-8 -*-
"""把逐张 OCR 文本解析成档案条目，并输出「异常/待复核」清单（第二版）。

首版两个 bug 已修：
  1) norm_token 把 IMM 的 I 归一成 1 → 变成 1MM，免疫标记被当普通文字丢掉；
  2) 技能只在「名称-标签」同一行时才能切分，而实际常常分行或把下一个技能的
     「名称-标签」塞在上一句描述的末尾 → 改为在全文上做正则切分。

抗性策略：按位置取 8 项；IMM 槽位 = IMM；纯数字槽位取该数字（OCR 常把 10 读成 0，
按 10% 处理）；出现成对/多值（多状态）或异常值则标记待复核，不硬猜。
"""
import json
import re
from pathlib import Path

OCR = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\_miyoushe\_ocr")
OUT = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\_miyoushe\_gen2")
OUT.mkdir(parents=True, exist_ok=True)

# 卡片上的元素顺序（已用 22 张元素生命卡的 IMM 位置验证：风在 3、草在 5）
ELEMENTS = ["pyro", "hydro", "anemo", "electro", "dendro", "cryo", "geo", "physical"]
CN_TO_KEY = {"火": "pyro", "水": "hydro", "草": "dendro", "雷": "electro",
             "风": "anemo", "冰": "cryo", "岩": "geo", "物": "physical"}
LETTER_TO_KEY = {"P": "pyro", "H": "hydro", "D": "dendro", "E": "electro",
                 "A": "anemo", "C": "cryo", "G": "geo"}

TAGS = ["输出", "亡语", "天赋", "控制", "远程", "召唤", "妨害", "回复", "强化",
        "充能", "猛攻", "突袭", "定位", "防护", "碾压", "辅助", "追踪", "跌落"]
TAG_ALT = "|".join(sorted(set(TAGS), key=len, reverse=True))

IMM_PAT = re.compile(r"(?i)IMM|\bMM\b|\bMN\b|\bIM\b")
CJK = r"\u4e00-\u9fff"

report = []


def is_imm(t: str) -> bool:
    u = t.upper().strip()
    return bool(IMM_PAT.search(u)) or u in {"S", "?", "MM", "MN"}


def digits_only(t: str) -> str:
    return (t.replace("O", "0").replace("o", "0")
             .replace("l", "1").replace("I", "1").replace("|", "1"))


def parse_resistance_block(lines):
    start = None
    for i, ln in enumerate(lines):
        if "抗性数据" in ln:
            start = i + 1
            break
    if start is None:
        return None, []
    tokens, notes = [], []
    for ln in lines[start:]:
        if ln.startswith(("全技能", "元素能量")) or "制图@" in ln:
            break
        t = ln.strip()
        if not t:
            continue
        if is_imm(t):
            # 含 IMM 又含数字 = 多个值被糊在一行（如 10101010 MM）→ 需复核
            kind = "mixed" if re.search(r"\d", t) else "imm"
            tokens.append({"kind": kind, "raw": t})
            continue
        n = digits_only(t)
        nums = re.findall(r"-?\d+", n)
        has_cjk = bool(re.search(f"[{CJK}]", n))
        if has_cjk and len(nums) >= 2:
            # 数字+中文且多值（如 35 10 没有时）→ 可疑，标记复核
            tokens.append({"kind": "mixed", "raw": t})
        elif has_cjk:
            # 数字+中文且单值 = 状态说明（如 电量：2雷盾…）→ 记成说明文字，不算抗性值
            notes.append(t)
        elif len(nums) >= 2:
            # 纯数字多值（如 75 50）→ 成对，多状态抗性
            tokens.append({"kind": "multi", "raw": t})
        elif len(nums) == 1:
            tokens.append({"kind": "num", "raw": t, "value": n})
        else:
            notes.append(t)
    return tokens, notes


def build_resistances(tokens):
    """8 项以内且无多值的，按位置解析；否则标待复核。"""
    anomalies = []
    if not tokens:
        return {}, ["resist:block-missing"]
    if len(tokens) != 8 or any(t["kind"] == "mixed" for t in tokens):
        return {}, ["resist:multi-state-or-mixed"]
    values = {}
    for i, t in enumerate(tokens):
        key = ELEMENTS[i]
        if t["kind"] == "imm":
            values[key] = "IMM"
            continue
        nums = [int(x) for x in re.findall(r"-?\d+", t.get("value", ""))]
        if len(nums) != 1:
            return {}, ["resist:multi-state-or-mixed"]
        v = nums[0]
        # OCR 常把 10 读成 0（丢失前导 1）→ 按 10% 处理
        if v == 0:
            v = 10
        values[key] = f"{v}%"
        if v != 10:
            anomalies.append("resist:unusual-value")
    return values, anomalies


def parse_energy(text, name):
    i = text.find("元素能量")
    seg = text[i:i + 40] if i >= 0 else text
    m = re.search(r"([A-Za-z][A-Za-z0-9]?)\[([0-9][0-9,\s]*)\]", seg)
    if not m:
        return None, ["energy:line-missing"]
    letters = m.group(1)
    elem = LETTER_TO_KEY.get(letters[0].upper())
    if elem is None:
        elem = next((CN_TO_KEY[c] for c in name if c in CN_TO_KEY), None)
    if elem is None:
        return None, ["energy:unknown-element"]
    k = letters[1].upper() if len(letters) > 1 else "P"
    if k not in ("P", "O"):
        k = "O" if letters[1] == "0" else "P"
    hps = [int(x) for x in m.group(2).replace(" ", "").split(",") if x.isdigit()]
    if not hps:
        return None, ["energy:no-thresholds"]
    rows = [{"hp": h, "particle": 1 if k == "O" else 0, "orb": 1 if k == "P" else 0} for h in hps]
    rows.sort(key=lambda r: -r["hp"])
    return {"raw": m.group(0), "rows": rows}, []


SKILL_PAT = re.compile(
    rf"(?P<name>[{CJK}A-Za-z0-9·]{{2,14}})[^{CJK}A-Za-z0-9]{{0,2}}[-—–]\s*(?P<tag>{TAG_ALT})"
)


def parse_skills(text):
    matches = list(SKILL_PAT.finditer(text))
    skills = []
    for i, m in enumerate(matches):
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        desc = text[m.end():end]
        desc = re.sub(r"\s+", "", desc)
        desc = desc.strip("　 ·,，。;；")
        skills.append({
            "name": m.group("name").strip(),
            "tag": m.group("tag"),
            "description": desc,
        })
    return skills


def parse_card(txt: str):
    lines = [ln.strip() for ln in txt.splitlines() if ln.strip()]
    if not lines:
        return None, ["empty"]
    anomalies = []

    name = re.split(r"[A-Za-z0-9(（:：]", lines[0])[0].strip(" ·「」")
    if len(lines) > 1 and lines[1].startswith("·"):
        name = name + re.split(r"[A-Za-z0-9(（:：]", lines[1])[0].strip(" ·")
    if not name:
        anomalies.append("name:empty")

    head = "\n".join(lines[:6])
    kind = next((k for k in ("普通", "精英", "首领", "地方传奇") if k in head), None)
    lv = re.search(r"Lv\.?\s*(\d+)", head)

    tokens, notes = parse_resistance_block(lines)
    values, res_anom = build_resistances(tokens) if tokens is not None else ({}, ["resist:block-missing"])
    anomalies += res_anom

    energy, en_anom = parse_energy("\n".join(lines), name)
    # 没有「元素能量」行的卡片很常见（不是异常），只有解析到一半失败才算
    anomalies += [a for a in en_anom if a != "energy:line-missing"]

    skills = parse_skills("\n".join(lines))
    if not skills:
        anomalies.append("skills:none")

    entry = {
        "name": name,
        "aliases": [name],
        "type": kind or "普通",
    }
    if lv:
        entry["type"] += f" Lv.{lv.group(1)}"
    if energy:
        entry["energy"] = energy
    entry["resistances"] = {"values": values, "states": []}
    entry["mechanics"] = [{"title": "", "text": " ".join(notes)}] if notes else []
    entry["skills"] = skills
    return entry, anomalies


def main():
    entries = []
    for folder in sorted(d for d in OCR.iterdir() if d.is_dir()):
        for txt in sorted(folder.glob("*.txt")):
            content = txt.read_text(encoding="utf-8")
            entry, anomalies = parse_card(content)
            if entry is None:
                report.append(f"[{folder.name}/{txt.stem}] 解析失败")
                continue
            entry["_source"] = f"{folder.name}/{txt.stem}"
            entries.append(entry)
            if anomalies:
                block = "\n".join(ln for ln in content.splitlines() if ln.strip())[:700]
                report.append(f"[{folder.name}/{txt.stem}] {entry['name']} :: {','.join(anomalies)}\n"
                              f"    --- 原文 ---\n{block}\n")

    (OUT / "entries.json").write_text(json.dumps(entries, ensure_ascii=False, indent=1), encoding="utf-8")
    (OUT / "report.txt").write_text(
        f"卡片总数: {len(entries)}\n标记待复核: {len(report)}\n\n" + "\n".join(report), encoding="utf-8")
    print(f"entries={len(entries)} flagged={len(report)}")


if __name__ == "__main__":
    main()
