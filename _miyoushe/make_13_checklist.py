# -*- coding: utf-8 -*-
"""生成 13 篇的人工核对表（配合 `crop13/` 里的修正裁图使用）。

为什么需要它
    13 篇的本地 OCR 是**确定性失败**的（数字读成 `OO2Q2O2OOO2OO2` 之类），
    所以只能人工/视觉模型读图。人工读图时最怕两件事：
      1. 不知道自己该填几行  -> 表里列出 worklist 的行数
      2. 分不清列的顺序      -> 表头固定写出「火水风雷草冰岩物」并给出该顺序的确认依据
    另外把「本地 OCR 的坏读数」与「注释区原文」一并列出，前者用于确认确实坏了，
    后者常常直接写着各状态的抗性，是很强的交叉校验线索。

⚠️ 裁图几何
    旧几何是 `(0, yb-70) ~ (lx0+600, yb+320)`，对 **≥4 行**的卡会**切掉最后 1~2 行**
    （实测 13/003 有 5 行，旧几何只框到第 3 行）。本脚本用修正后的 `yb+430`，
    能容纳最多 5 行且不把下方注释文字框进来（只留一个「困难 -」碎片）。

用法
    python make_13_checklist.py            # 生成 _gen2/crop13/_核对表.md
    python make_13_checklist.py --crops     # 顺手重裁 18 张图（依赖 Umi-OCR 在线跑 locate）
"""
import argparse
import collections
import json
import sys
from pathlib import Path

HERE = Path(__file__).parent
GEN = HERE / "_gen2"
OUT = GEN / "crop13"
RES = HERE / "_rescue"
CAL = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\_design\enemy-archive\cal")
SRC = HERE / "miyoushe_3572896"

CROP_BOTTOM = 430   # yb + 430：容 5 行且不含注释段
CROP_LEFT = 600     # lx0 + 600


def make_crops(takes):
    """用修正几何重裁。需要 Umi-OCR 在线（`locate()` 依赖它）。"""
    import vlm_transcribe as V  # 延迟导入：不裁图时无需 OCR 相关依赖
    from PIL import Image
    OUT.mkdir(parents=True, exist_ok=True)
    for p in OUT.glob("*.png"):
        p.unlink()
    made = []
    for take in takes:
        tag, idx = take.split("/")
        src = next(SRC.glob(f"{tag}_*/{idx}.png"))
        im = Image.open(src).convert("RGB")
        lx0, yb, *_ = V.F.locate(im)
        box = (0, max(0, int(yb - 70)), min(im.width, int(lx0 + CROP_LEFT)),
               min(im.height, int(yb + CROP_BOTTOM)))
        crop = im.crop(box)
        up = crop.resize((int(crop.width * 1.8), int(crop.height * 1.8)), Image.LANCZOS)
        out = OUT / f"{tag}_{idx}.png"
        up.save(out)
        made.append((take, up.size))
    return made


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--crops", action="store_true", help="顺便重裁 18 张图")
    args = ap.parse_args()
    sys.stdout.reconfigure(errors="replace")
    a_ = lambda s: str(s).encode("ascii", "replace").decode()

    wl = json.loads((GEN / "lowconf_worklist.json").read_text(encoding="utf-8"))
    bycard = collections.defaultdict(list)
    for r in wl:
        if r["scope"] == "excluded-13":
            bycard[r["take"]].append(r)
    local = {c["src"]: c for c in json.loads((RES / "13_states.json").read_text(encoding="utf-8"))}
    gold = json.loads((CAL / "cal_gold.json").read_text(encoding="utf-8"))
    takes = sorted(bycard)

    if args.crops:
        made = make_crops(takes)
        print("重裁 %d 张：" % len(made))
        for take, size in made:
            print("  %-9s %dx%d" % (a_(take), size[0], size[1]))

    lines = [
        "# 13 篇人工核对表",
        "",
        "列顺序固定为 **火 水 风 雷 草 冰 岩 物**。",
        "确认依据：`13/003` 的「凝滞」行是 `30,30,30,30,30,-30,30,30`，",
        "而同卡注释写「凝滞**冰**-40%」→ 第 6 位正是冰 ✓",
        "",
        f"每张卡的裁图：`crop13/<tag>_<idx>.png`（几何已修正为 `yb+{CROP_BOTTOM}`，可容纳最多 5 行）。",
        "⚠️ 旧几何 `yb+320` 会切掉第 4/5 行 —— 那是本轮发现的 bug，",
        "校准批那 3 张卡恰好都 ≤3 行，所以从未暴露。",
        "",
    ]
    for take in takes:
        tag, idx = take.split("/")
        card = local.get(take, {})
        lines.append(f"## `{take}` — {len(bycard[take])} 行待填")
        lines.append(f"- 裁图：`{tag}_{idx}.png`")
        if take in gold:
            lines.append(f"- ✅ 校准批已目视核过（金标准 {len(gold[take])} 行），可用来检查裁图几何：")
            for lab, vals in gold[take]:
                lines.append(f"  - label=`{lab}` values=`{vals}`")
        rows = card.get("rows") or []
        lines.append("- 本地 OCR 读到的（**已知是坏的**，仅作对照）：")
        for r in rows:
            lines.append(f"  - `{r.get('values')}`")
        note = (card.get("note_text") or "").strip()
        if note:
            lines.append("- 注释区原文（交叉校验线索，往往直接写着各状态抗性）：")
            lines.append(f"  > {note}")
        lines.append("- worklist 行号 `%s`，OCR label `%s`"
                     % ([r["row"] for r in bycard[take]], [r["label"] for r in bycard[take]]))
        lines.append("- 待填：")
        lines.append("  | 行标签 | 火 | 水 | 风 | 雷 | 草 | 冰 | 岩 | 物 |")
        lines.append("  |---|---|---|---|---|---|---|---|---|")
        for r in bycard[take]:
            lines.append("  | %s | | | | | | | | |" % (r["label"] or "?"))
        lines.append("")

    OUT.mkdir(parents=True, exist_ok=True)
    p = OUT / "_核对表.md"
    p.write_text("\n".join(lines), encoding="utf-8")
    print("已生成 %s（%d bytes）" % (a_(p.name), p.stat().st_size))
    print("待填行合计 %d，即 %d 个格子" % (sum(len(v) for v in bycard.values()),
                                          sum(len(v) for v in bycard.values()) * 8))
    return 0


if __name__ == "__main__":
    sys.exit(main())
