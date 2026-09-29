# -*- coding: utf-8 -*-
"""13 篇（`--scope excluded-13`）只读比对报告 —— **不修改任何文件**。

用法
    1) 先跑读数（需要 key，~26 张）：
         python vlm_transcribe.py --worklist --scope excluded-13
    2) 再出报告：
         python check_13.py

为什么要先看数字再决定
    「13 篇整篇排除」是当初的**刻意决定**（本地 OCR 在 13 篇是确定性失败）。
    反悔需要证据，而「通道看起来没问题」不是证据。本脚本只输出数字，不改数据。

本脚本回答四个问题
    1) 13 篇的行数是否也系统性 > 本地（本地漏行在其余篇已有先例）
    2) VLM 相对本地**多找回**了哪些行（本地完全没有的对标物）
    3) 值域/5 倍数/几何边界是否有新情况（13 篇是 2048×1080、表在卡片顶部，几何独特）
    4) 逐格与本地的一致率（本地在 13 篇很差，一致率低是**预期**，不代表 VLM 错）

退出码：0 = 有输出且无硬性问题；1 = 没跑读数或发现越界/非 5 倍数
"""
import json
import re
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).parent
GEN = HERE / "_gen2"
RES = HERE / "_rescue"
V = GEN / "vlm"
LOW, HIGH = -200, 10000

norm = lambda s: re.sub(r"[\s\u3000·・、,，.。:：()（）\[\]【】]+", "", str(s or ""))


def main():
    a = lambda s: str(s).encode("ascii", "replace").decode()
    files = sorted(V.glob("13_*.json"))
    if not files:
        print("未找到 _gen2/vlm/13_*.json —— 请先跑：")
        print("  python vlm_transcribe.py --worklist --scope excluded-13")
        return 1

    loc = {}
    p13 = RES / "13_states.json"
    if p13.exists():
        for c in json.loads(p13.read_text(encoding="utf-8")):
            loc[c["src"]] = c.get("rows", [])

    print("=" * 74)
    n_all = len(loc) or 26
    print("13 篇只读比对（%d / %d 张卡有 VLM 输出）" % (len(files), n_all))
    if len(files) < n_all:
        print("  \u26a0 \u8fd8\u672a\u8dd1\u5168\uff1a\u8bf7\u5148 `python vlm_transcribe.py --worklist --scope excluded-13`")
    print("=" * 74)

    hard = []
    n_vlm_rows = n_loc_rows = n_recovered = 0
    agree_hist = Counter()
    ranges = []

    for p in files:
        src = p.stem.replace("_", "/", 1)
        rows = json.loads(p.read_text(encoding="utf-8"))["rows"]
        lrows = loc.get(src, [])
        n_vlm_rows += len(rows)
        n_loc_rows += len(lrows)
        print("\n%s   VLM %d 行 / 本地 %d 行" % (a(src), len(rows), len(lrows)))

        lbl_loc = [norm(r.get("label")) for r in lrows]
        for i, r in enumerate(rows, 1):
            vals = r["values"]
            lab = norm(r.get("label"))
            nums = [v for v in vals if v != "IMM"] if isinstance(vals, list) else []
            bad = [v for v in nums if not isinstance(v, int) or v % 5 or not (LOW <= v <= HIGH)]
            if bad:
                hard.append((src, i, bad))
            ranges += [v for v in nums if isinstance(v, int)]
            # 与本地同 label 行比对（本地可能没有 → 视为 VLM 找回）
            hit = next((lr for lr in lrows if norm(lr.get("label")) == lab and lab), None)
            if hit is None:
                n_recovered += 1
                # ⚠️ 这只是「本地没有同 label 的行」，**不等于**本地漏了行：
                # 13 篇本地 label 本身就是垃圾 OCR，所以本指标会系统性高估。
                # 可靠的漏行度量看「行数差」。
                tag = "label无匹配"
            else:
                lv = hit["values"]
                s = sum(1 for c in range(8) if c < len(lv) and
                        ((vals[c] == "IMM" and lv[c] == "IMM") or
                         (vals[c] != "IMM" and str(vals[c]) == str(lv[c]))))
                agree_hist[s] += 1
                tag = "与本地同 label 行 相同=%d/8" % s
            print("   r%d %-12s %s   %s"
                  % (i, a(lab or "-"), a(str(vals)), tag))

    print("\n" + "=" * 74)
    print("汇总")
    print("  VLM 总行数 %d / 本地总行数 %d  → 行数差 %+d（这是可靠的漏行度量）"
          % (n_vlm_rows, n_loc_rows, n_vlm_rows - n_loc_rows))
    print("  label 无匹配的行 %d 个（\u26a0 本地 label 是垃圾 OCR，此数会高估，仅供参考）"
          % n_recovered)
    print("  与本地同 label 行的一致率分布（相同格数:行数）: %s"
          % a(str(dict(sorted(agree_hist.items())))))
    if ranges:
        print("  值域 %d ~ %d ；5 倍数外的值: %s"
              % (min(ranges), max(ranges),
                 sorted({v for v in ranges if v % 5}) or "(无)"))
    if hard:
        print("\n  ⚠ 硬性问题 %d 处（越界 / 非 5 倍数）:" % len(hard))
        for src, i, bad in hard[:15]:
            print("    %-10s r%d  %s" % (a(src), i, a(str(bad))))
    else:
        print("  ✓ 无越界 / 无 5 倍数外")
    print("\n  ⚠ 提醒：本地通道在 13 篇是确定性失败，一致率低属**预期**，")
    print("    不能据此判 VLM 错。要判对错得看卡面（逐张目视或放大裁图）。")
    print("=" * 74)
    return 1 if hard else 0


if __name__ == "__main__":
    sys.exit(main())
