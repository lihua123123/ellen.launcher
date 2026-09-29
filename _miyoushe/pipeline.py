# -*- coding: utf-8 -*-
"""扫描完成后的收尾流水线：出复核表 → 健康度检查 → 汇总（不入库）。

入库单独走 merge_all.py，便于人工过一眼表再合并。
"""
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).parent
PY = sys.executable
GEN = HERE / "_gen2"


def run(script, *args):
    r = subprocess.run([PY, "-u", "-X", "utf8", str(HERE / script), *args],
                       capture_output=True, text=True, errors="replace", cwd=str(HERE), timeout=1800)
    ok = r.returncode == 0
    print(f"[{script}] exit={r.returncode}")
    if not ok:
        print("  stderr:", str(r.stderr)[-300:].encode("ascii", "replace").decode())
    return r.stdout


def main():
    tags = sys.argv[1:]
    if not tags:
        tags = sorted(p.stem.replace("_states", "")
                      for p in (HERE / "_rescue").glob("*_states.json"))
    print("articles:", len(tags))

    out = run("table_all.py", *tags)
    rows = [l for l in out.splitlines() if "cards=" in l]
    print("  " + "\n  ".join(str(l).encode("ascii", "replace").decode() for l in rows))

    run("health.py", *tags)
    health = (GEN / "health.txt").read_text(encoding="utf-8").splitlines()
    print("  health.txt:", len(health), "lines")
    for l in health:
        if "需查" in l:
            print("  " + str(l).encode("ascii", "replace").decode())

    diffs = json.loads((GEN / "diffs_all.json").read_text(encoding="utf-8"))
    by_art = {}
    for d in diffs:
        by_art.setdefault(d["src"].split("/")[0], 0)
        by_art[d["src"].split("/")[0]] += 1
    print("diff rows total:", len(diffs))
    print("per-article:", json.dumps(by_art, sort_keys=True))
    names = {d["meta"] for d in diffs if d["meta"]}
    print("distinct meta names to merge:", len(names))


if __name__ == "__main__":
    main()
