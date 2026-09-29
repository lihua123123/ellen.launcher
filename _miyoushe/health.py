# -*- coding: utf-8 -*-
"""抗性提取健康度检查：用「与元数据全同率」当误读探测指标。

原理：卡片上的抗性绝大多数与官方元数据一致（01/03 实测 41/42 一致）。
      若某篇全同率骤降，多半是 OCR 误读而非真的「档案 ≠ 元数据」。
      同时白名单过滤不可能的取值（如 170%、130%）。
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))
import table_all as T  # noqa: E402

# 卡面实测合法区间：负值到 -100、高值到 370+（N5 态）
# 仍要求 5 的倍数：`4`/`99`/`-5050` 这类误读会被拦下
LOW, HIGH = -100, 500


def check(tag):
    states = json.loads((HERE / "_rescue" / f"{tag}_states.json").read_text(encoding="utf-8"))
    cards = len(states)
    rows = [r for x in states for r in x.get("rows", [])]
    ok8 = sum(1 for r in rows if len(r["values"]) == 8)
    same = diff = miss = 0
    suspects, odd = [], []
    for item in states:
        _, idx = item["src"].split("/")
        txt = (T.OCR / tag / f"{idx}.txt").read_text(encoding="utf-8")
        lines = [ln.strip() for ln in txt.splitlines() if ln.strip()]
        name, mid = T.resolve_name(lines)
        mv = T.meta_vals(mid)
        if not mid:
            miss += 1
        for row in item.get("rows", []):
            vals = row["values"]
            for v in vals:
                if v == "IMM":
                    continue
                try:
                    n = int(v)
                except (TypeError, ValueError):
                    odd.append((item["src"], row["label"], v))
                    continue
                if n % 5 or not LOW <= n <= HIGH:
                    odd.append((item["src"], row["label"], f"{n}%"))
            if mv:
                s = sum(1 for i, v in enumerate(vals) if T.fmt(v) == f"{mv[i]}%")
                if s == 8:
                    same += 1
                else:
                    diff += 1
                    if len(vals) == 8 and s <= 3:
                        suspects.append((item["src"], name, row["label"] or "常态",
                                         vals, [T.fmt(v) for v in vals]))
    rate = same / (same + diff) if (same + diff) else 0
    return {"tag": tag, "cards": cards, "rows": len(rows), "ok8": ok8,
            "same": same, "diff": diff, "miss": miss, "same_rate": round(rate, 3),
            "odd": odd, "suspects": suspects}


def main():
    tags = sys.argv[1:] or sorted(p.stem.replace("_states", "")
                                  for p in (HERE / "_rescue").glob("*_states.json"))
    out = []
    for tag in tags:
        r = check(tag)
        flag = "⚠ 需查" if (r["same_rate"] < 0.7 or r["odd"]) else "ok"
        out.append(f"{tag:<4} cards={r['cards']:>3} rows={r['rows']:>3} ok8={r['ok8']:>3} "
                   f"全同={r['same']:>3} 差异={r['diff']:>3} 缺元数据={r['miss']:>2} "
                   f"一致率={r['same_rate']:>5}  {flag}")
        for s in r["suspects"][:6]:
            out.append(f"       主要差异 {s[0]} {s[1]}: {s[3]}")
        for o in r["odd"][:6]:
            out.append(f"       ⚠ 异常取值 {o[0]} [{o[1]}] {o[2]}")
    (HERE / "_gen2" / "health.txt").write_text("\n".join(out), encoding="utf-8")
    print(f"written health.txt, {len(out)} lines, flags=" +
          str(sum(1 for l in out if "需查" in l)))


if __name__ == "__main__":
    main()
