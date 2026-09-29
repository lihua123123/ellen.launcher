# -*- coding: utf-8 -*-
"""收尾：回退档案到基线 → 出复核表 → 按「高置信 + diff + 取值合理」入库 → 汇总。

用法：python finalize.py
"""
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).parent
ROOT = Path(r"D:\艾莲的数据库\workspace\ellen-launcher")
ARCH = ROOT / "src/Ellen.Launcher/Ellen.Launcher/Resource/EnemyArchives/EnemyArchives.json"
GEN = HERE / "_gen2"
PY = sys.executable


def restore_baseline():
    """把上一轮 merge 加进去的条目摘掉，恢复到基线（避免重复累计）。"""
    names = set(json.loads((GEN / "added_names.json").read_text(encoding="utf-8")))
    arch = json.loads(ARCH.read_text(encoding="utf-8"))
    kept = [e for e in arch if e.get("name") not in names]
    removed = len(arch) - len(kept)
    ARCH.write_text(json.dumps(kept, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return len(arch), len(kept), removed


def run(script, *args):
    r = subprocess.run([PY, "-u", "-X", "utf8", str(HERE / script), *args],
                       capture_output=True, text=True, errors="replace",
                       cwd=str(HERE), timeout=1800)
    return r.returncode, r.stdout, r.stderr


def main():
    a = lambda s: str(s).encode("ascii", "replace").decode()
    before, kept, removed = restore_baseline()
    print(f"档案回退: {before} -> {kept} (移除 {removed})")

    tags = [f"{i:02d}" for i in range(1, 20) if i != "13"]
    rc, out, err = run("table_all.py", *tags)
    print("table_all exit", rc, a(err[-150:]).strip())

    # ⚠️ 必须在 table_all 之后：table_all 会**重建** diffs_all.json，
    # 先注入会被覆盖。gap_override 用 VLM 补读值覆盖低置信行。
    rc_o, out_o, err_o = run("gap_override.py")
    print("gap_override exit", rc_o, a(out_o.strip()).replace("\n", " | "))
    # ⚠️ gap_override 非零退出 = 有采纳行**没能注入**（工单行与 diffs 行对不上）。
    # 此时那几行会带着**本地未验证值**进入档案 —— 这是数据错误，必须停，不能继续。
    # 实测触发场景：改了名字解析器（`table_all.find_name_in_text` 换成 name_resolve 后
    # 卡片的 `meta` 会变，而 gap_override 按老键找不到行）→ 2026-09-30 实测 exit 1、
    # 5 行未注入、evidence 99→94。当时是**回退档案**收场的，见 BACKLOG.md。
    if rc_o != 0:
        print()
        print("!! 中止：gap_override 失败（%d）。继续跑 merge_all 会产出带"
              "「本地未验证值」的档案。" % rc_o)
        print("!! 见 _miyoushe/BACKLOG.md 的「名字解析迁移」一条。")
        return 1

    rc2, out2, _ = run("health.py", *tags)
    print("health exit", rc2)

    diffs = json.loads((GEN / "diffs_all.json").read_text(encoding="utf-8"))
    conf = [d for d in diffs if d.get("confident")]
    print(f"diff 行 {len(diffs)}，其中高置信 {len(conf)}，低置信 {len(diffs) - len(conf)}")

    rc3, out3, err3 = run("merge_all.py")
    print("merge_all exit", rc3, a(out3[-200:]).strip(), a(err3[-200:]).strip())

    report = (GEN / "merge_all.txt").read_text(encoding="utf-8").splitlines()
    review = next((i for i, l in enumerate(report) if l.startswith("=== 被守卫丢弃")), None)
    n_drop = len(report) - review - 1 if review is not None else 0
    print("被守卫丢弃行数:", n_drop)
    if review is not None:
        for l in report[review + 1:]:
            print("   " + a(l))


if __name__ == "__main__":
    # 必须把返回值变成退出码：否则 main() 里的 `return 1`（gap_override 护栏）
    # 不会体现在 EXIT 上，调用方看不见失败。
    sys.exit(main())
