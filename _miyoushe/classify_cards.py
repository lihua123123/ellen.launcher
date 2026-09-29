# -*- coding: utf-8 -*-
"""对 600 张卡做「抗性表形态」聚类统计，并按篇给出分布；顺带统计与元数据的 diff 条数。

形态定义：
  missing      没有抗性数据块
  merged       OCR 把数值与中文糊在一行（如 10101010 MM / 35 10 没有时）
  multi-state  一行里有 2 个数值（成对）→ 两套状态抗性
  shape!=8     数值条目不是 8 个（块被切错或含额外行）
  unusual      8 项标准形态，但存在非 10% 的数值（如 -20）
  std-immune   8 项标准形态，恰有 1 个 IMM
  std-plain    8 项标准形态，无 IMM
"""
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))
import parse_cards2 as P  # noqa: E402

META = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher\Resource"
            r"\Metadata\Genshin\CHS\Monster.json")


def shape(tokens):
    if tokens is None:
        return "missing"
    if any(t["kind"] == "mixed" for t in tokens):
        return "merged"
    if any(t["kind"] == "multi" for t in tokens):
        return "multi-state"
    if len(tokens) != 8:
        return "shape!=8"
    for t in tokens:
        if t["kind"] == "num":
            nums = [int(x) for x in re.findall(r"-?\d+", t["value"])]
            v = nums[0] if len(nums) == 1 else 99
            if v == 0:
                v = 10
            if v != 10:
                return "unusual"
    imm = sum(1 for t in tokens if t["kind"] == "imm")
    return "std-immune" if imm == 1 else "std-plain"


def main():
    overall = Counter()
    per_article = defaultdict(Counter)
    names_by_article = defaultdict(list)
    energy_cards = 0

    for folder in sorted(d for d in P.OCR.iterdir() if d.is_dir()):
        tag = folder.name
        for txt in sorted(folder.glob("*.txt")):
            lines = [ln.strip() for ln in txt.read_text(encoding="utf-8").splitlines() if ln.strip()]
            if not lines:
                continue
            tokens, _ = P.parse_resistance_block(lines)
            s = shape(tokens)
            overall[s] += 1
            per_article[tag][s] += 1
            name = re.split(r"[A-Za-z0-9(（:：]", lines[0])[0].strip(" ·「」")
            names_by_article[tag].append(name)
            if "元素能量" in "\n".join(lines):
                energy_cards += 1

    print("=== 抗性形态总览（600 张）===")
    for k, v in overall.most_common():
        print(f"  {k:<12} {v:>4}  ({v / 6:.1f}%)")
    print(f"\n含「元素能量」行的卡片: {energy_cards}")
    print(f"有 IMM（免疫某一元素）的卡片: {overall['std-immune']}")

    print("\n=== 按篇分布 ===")
    print(f"{'篇':<34} {'总数':>4} {'std-immune':>10} {'std-plain':>9} {'multi':>6} "
          f"{'merged':>6} {'unusual':>7} {'shape':>5} {'missing':>7}")
    for tag in sorted(per_article):
        c = per_article[tag]
        total = sum(c.values())
        print(f"{tag:<34} {total:>4} {c['std-immune']:>10} {c['std-plain']:>9} "
              f"{c['multi-state']:>6} {c['merged']:>6} {c['unusual']:>7} "
              f"{c['shape!=8']:>5} {c['missing']:>7}")


if __name__ == "__main__":
    main()
