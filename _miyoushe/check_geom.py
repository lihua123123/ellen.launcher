# -*- coding: utf-8 -*-
"""裁图几何自证：`max_rows_in()` 到底对不对。

为什么需要这个脚本
------------------
`possibly_truncated` 的判据是 `rows_observed >= rows_fit`，而 `rows_fit` 由
`max_rows_in(裁图高, 篇)` 算出。这个函数**曾经错过**：

  - 公式写成 `(span - dy + HALF) // step + 1`（`+HALF`），等价于允许「行中心比裁图
    下沿低半行」，把容量**多算一行**。实测反例：`12/013` 算出 3（真值 2）、
    13 篇算出 6（真值 5）。
  - `ROW_HALF = 16` 是**拍的**，实测行墨迹带高 56px（半高 28）—— 连量都没量过。
  - 最要命的是：当时也有一张「自证 6/6」的表，但那张表的**期望值是从公式输出抄的**
    → 它验证的是公式自己，不是事实。所以它 6/6 通过、公式同时是错的。

本脚本的原则：**真值必须来自独立路径**。
  - 行网格与行数都从**原图**量：逐扫描线统计「亮于本行中位数 +45」的像素数，
    取连续墨迹带 → 得到每行的墨迹带上沿/下沿（相对 yb）。与 `max_rows_in` 无涉。
  - 行数的**期望值**来自人眼逐张数过的清单（见 KNOWN），不是来自几何公式。

三层校验
--------
  A. 网格常量：量出来的行带是否与 `ROW_GRID_*` 一致（上沿对得上 ±3px、带高 46~66）
  B. 已知行数：量出来的行数是否等于 KNOWN 里人眼数出的行数（25 张卡，含全部 18 张 13 篇）
  C. 结构性质：`max_rows_in` 单调不减、且每 +60px 恰好 +1 行；再对每个「行带刚好
     跨过裁图下沿」的边界点，检查容量是否恰好在那一刻 +1

输出 `_gen2/geom_report.md`（UTF-8），因为控制台可能是 GBK 会显示成乱码。

用法：
    python check_geom.py                 # 量图 + 校验（约 25 张卡，需 Umi-OCR 在跑）
    python check_geom.py --structure-only  # 只做 C 层，秒级、不调 OCR
"""
import argparse
import sys
from pathlib import Path

HERE = Path(__file__).parent
ROOT = HERE.parent
sys.path.insert(0, str(HERE))

from PIL import Image  # noqa: E402

import extract_fast as F  # noqa: E402
import rescue_resist as R  # noqa: E402
from vlm_transcribe import (  # noqa: E402
    CROP_BOTTOM_13,
    CROP_BOTTOM_DEFAULT,
    ROW_GRID_13,
    ROW_GRID_DEFAULT,
    max_rows_in,
)

GEN2 = HERE / "_gen2"

# ---------------------------------------------------------------- 人眼真值
# 来源（都写清楚，将来可复查）：
#   - 13 篇 18 张：26/9/30 逐张看图数出（`_gen2/crop13/_核对表.md` 同批）
#   - 非 13 篇 7 张：26/9/30 审计 `n_card=4` 的卡时逐张看图数出
#     （其中 06/041 是 5 行 —— 就是被 yb+320 切掉一行的那张）
# ⚠️ 这些是**人眼**数出来的，不是几何公式推出来的。改这张表必须重新看图。
KNOWN = {
    ("06", "041"): 5,   # 唯一确认被切的卡（第 5 行「不破机制惩罚 80x8」）
    ("04", "024"): 4,
    ("04", "029"): 4,
    ("04", "030"): 4,
    ("05", "030"): 4,
    ("06", "043"): 4,
    ("11", "027"): 4,
    ("13", "001"): 3, ("13", "002"): 3, ("13", "003"): 5,
    ("13", "006"): 3, ("13", "007"): 1, ("13", "008"): 4,
    ("13", "009"): 5, ("13", "010"): 3, ("13", "013"): 2,
    ("13", "015"): 1, ("13", "016"): 2, ("13", "017"): 4,
    ("13", "018"): 5, ("13", "019"): 5, ("13", "020"): 2,
    ("13", "023"): 4, ("13", "024"): 4, ("13", "025"): 4,
}

BAND_MIN, BAND_MAX = 46, 66    # 数据行墨迹带高的接受范围（实测 56）
ALIGN_TOL = 3                  # 筛墨迹带时与网格的对齐容差 —— **必须紧**：
                               #   13/007 的说明文字带上沿 +245 与网格 +240 差 5px，
                               #   放宽到 5 就会把它当成一行（实测）。
GRID_TOL = 6                   # 网格常量本身的容差 —— 卡间本就有抖动：
                               #   非 13 篇上沿 75/79/81，13 篇 116/120。
BRIGHT = 45                    # 「亮于本行中位数」的阈值，用于找墨迹


# ---------------------------------------------------------------- 量图
def measure(tag, idx):
    """从**原图**量出各候选墨迹带。返回 (距图底像素数, [(上沿, 下沿)] 相对 yb)。"""
    src = next(R.ROOT.glob("%s_*/%s.png" % (tag, idx)))
    im = Image.open(src).convert("RGB")
    lx0, yb, _ = F.locate(im)
    x0 = int(lx0 + 18)
    x1 = min(im.width, int(lx0 + 490))
    y0 = max(0, int(yb + 40))
    y1 = min(im.height, int(yb + 480))
    px = im.load()
    raw, cur = [], None
    for y in range(y0, y1):
        vals = sorted(0.299 * px[x, y][0] + 0.587 * px[x, y][1] + 0.114 * px[x, y][2]
                      for x in range(x0, x1, 2))
        med = vals[len(vals) // 2]
        ink = sum(1 for v in vals if v > med + BRIGHT)
        if ink >= 3:
            cur = [y, y] if cur is None else [cur[0], y]
        elif cur:
            raw.append(cur)
            cur = None
    if cur:
        raw.append(cur)
    return (im.height - yb), [(b[0] - yb, b[1] - yb) for b in raw]


def data_rows(raw_bands):
    """从原始带里挑出数据行：丢掉第 1 条（表头），再按「带高 + 网格对齐」筛。

    两条筛选缺一不可（都踩过）：
      - 只按带高筛：13 篇的**表头**也是 56px 高，会被当成一行 → 所以先丢 raw[0]
      - 只按网格筛：紧随表后的说明文字有时会**恰好**落在网格上（13/007 的带
        上沿 +245 与网格 +240 差 5px）→ 所以 ALIGN_TOL 收紧到 3
    """
    body = raw_bands[1:] if raw_bands else []
    body = [b for b in body if BAND_MIN <= (b[1] - b[0] + 1) <= BAND_MAX]
    if not body:
        return []
    base = body[0][0]
    return [b for b in body
            if (b[0] - base) % 60 <= ALIGN_TOL or (b[0] - base) % 60 >= 60 - ALIGN_TOL]


def crop_height_of(tag, idx, img_span):
    """该卡在真实裁图下的 `crop_height`（含 `min(im.height, yb+bottom)` 夹取）。"""
    bottom = CROP_BOTTOM_13 if tag == "13" else CROP_BOTTOM_DEFAULT
    return min(bottom, int(img_span)) + 70


# ---------------------------------------------------------------- 校验
def check_structure(log):
    """C 层：单调性 + 每 60px 恰好 +1 + 边界点 + 对网格抖动的鲁棒性。不调 OCR。"""
    bad = []
    for tag in ("06", "13"):
        top, step, ink = ROW_GRID_13 if tag == "13" else ROW_GRID_DEFAULT
        # ⚠️ 容量增加的条件是 `span - top - ink` 跨过 step 的整数倍，其中
        #    `span = crop_height - 70`。曾写成 `(crop_h - 1 - top - ink) % step`
        #    （漏了 70，差 69）→ 假报 23 项失败。
        #    另需排除 `span - top - ink < 0` 的区段：那里 `max(0, ...)` 把容量钳在 0，
        #    即使跨过 step 倍数也不会 +1（曾假报 5 项失败）。
        def expect_up(h):
            d = h - 70 - top - ink
            return d >= 0 and d % step == 0
        for h in range(71, 900):
            v, p = max_rows_in(h, tag), max_rows_in(h - 1, tag)
            if v < p:
                bad.append("非单调 tag=%s h=%d %d->%d" % (tag, h, p, v))
            elif (v == p + 1) != expect_up(h):
                bad.append("步进异常 tag=%s h=%d %d->%d" % (tag, h, p, v))
        for k in range(0, 9):
            edge = top + ink + step * k          # 第 k 行带下沿（相对 yb）
            y = edge + 70                        # 对应的 crop_height
            if max_rows_in(y, tag) != max_rows_in(y - 1, tag) + 1:
                bad.append("边界点失败 tag=%s k=%d crop_h=%d %d != %d+1"
                           % (tag, k, y, max_rows_in(y, tag), max_rows_in(y - 1, tag)))

    # 鲁棒性放到 check_images 里做（要用**量出来**的上沿范围，不能用拍的 ±N）
    log.append("### C 层：结构性质")
    log.append("")
    if bad:
        log.append("**失败 %d 项**：" % len(bad))
        log.extend("  - " + b for b in bad[:20])
    else:
        log.append("- `max_rows_in` 单调不减；容量只在「第 k 行带下沿 == 裁图下沿」处 +1")
        log.append("  （k=0..8 全部通过；`span - top - ink < 0` 的钳位区段也已排除）。")
    log.append("")
    return not bad


def check_images(log):
    """A/B 层：量图 → 校验网格常量与已知行数 → 算过报率。"""
    log.append("### A/B 层：量图（与 `max_rows_in` 独立的路径）")
    log.append("")
    rows = []
    n_top_bad = n_cnt_bad = 0
    for (tag, idx), want in sorted(KNOWN.items()):
        try:
            img_span, raw = measure(tag, idx)
        except Exception as e:
            rows.append((tag, idx, want, None, None, "量图失败 %s" % type(e).__name__))
            continue
        bands = data_rows(raw)
        base = bands[0][0] if bands else None
        gtop, gstep, gink = ROW_GRID_13 if tag == "13" else ROW_GRID_DEFAULT
        top_bad = ""
        if base is not None and abs(base - gtop) > GRID_TOL:
            top_bad = "上沿 %d vs 网格 %d" % (base, gtop)
            n_top_bad += 1
        cnt_bad = ""
        if len(bands) != want:
            cnt_bad = "量出 %d 行 vs 人眼 %d 行" % (len(bands), want)
            n_cnt_bad += 1
        rows.append((tag, idx, want, base, len(bands), top_bad or cnt_bad))

    bad_rows = [r for r in rows if r[5]]
    log.append("| 卡 | 人眼行数 | 量出行数 | 网格上沿 | 问题 |")
    log.append("|---|---|---|---|---|")
    for tag, idx, want, base, got, bad in rows:
        log.append("| `%s/%s` | %s | %s | %s | %s |"
                   % (tag, idx, want, got if got is not None else "—",
                      base if base is not None else "—", bad or "—"))
    log.append("")
    log.append("- 网格上沿不符：**%d** 张（容差 ±%d px）；行数不符：**%d** 张（共 %d 张）"
               % (n_top_bad, GRID_TOL, n_cnt_bad, len(rows)))
    log.append("  ⚠️ 行数才是关键判据：**%d/%d 全中** = 行网格与 `KNOWN` 人眼表互相印证。"
               % (len(rows) - n_cnt_bad, len(rows)))
    log.append("- 网格常量：非 13 篇 `ROW_GRID_DEFAULT=(%d, %d, %d)`、13 篇 `ROW_GRID_13=(%d, %d, %d)`"
               % (ROW_GRID_DEFAULT + ROW_GRID_13))

    # 鲁棒性：上沿在**实测**范围内抖动时，两个真实几何的容量必须不变。
    # 用实测范围而不是拍的 ±N：拍的容差要么过宽（假通过）要么过窄（假失败）—— 都踩过。
    log.append("")
    log.append("### D 层：对网格抖动的鲁棒性")
    log.append("")
    drift_bad = []
    # ⚠️ 分组要按「是不是 13 篇」，不能按 `t == "06"`：同一套网格的篇有
    #    04/05/06/11…，只挑 "06" 会把其余篇的上沿丢掉（曾因此把实测范围
    #    报成 79~79 而不是 75~81）。
    for is13, bottom in ((True, CROP_BOTTOM_13), (False, CROP_BOTTOM_DEFAULT)):
        bases = [b for t, i, w, b, g, e in rows if (t == "13") == is13 and b is not None]
        if not bases:
            continue
        lo, hi = int(min(bases)), int(max(bases))
        step, ink = (ROW_GRID_13 if is13 else ROW_GRID_DEFAULT)[1:]
        vals = set(max(0, (bottom - d - ink) // step + 1) for d in range(lo, hi + 1))
        tag_name = "13 篇" if is13 else "非 13 篇"
        if len(vals) == 1:
            log.append("- %s：实测上沿 %d~%d（%d 张），`yb+%d` 的容量恒为 **%d** ✓"
                       % (tag_name, lo, hi, len(bases), bottom, vals.pop()))
        else:
            drift_bad.append("%s 实测上沿 %d~%d 下容量取到 %s" % (tag_name, lo, hi, sorted(vals)))
            log.append("- %s：实测上沿 %d~%d → 容量取到 %s ✗" % (tag_name, lo, hi, sorted(vals)))
    log.append("")
    if drift_bad:
        bad_rows.extend(drift_bad)
    log.append("")

    # 过报率：按各卡的真实裁图几何算
    log.append("### 过报率（按各卡真实裁图几何）")
    log.append("")
    log.append("`flagged` = `rows_observed >= rows_fit` 会亮；"
               "`真被切` = 该卡存在一行的墨迹带**下沿超出**裁图下沿。")
    log.append("")
    log.append("| 卡 | 裁图高 | 人眼行数 | rows_fit | flagged | 真被切 |")
    log.append("|---|---|---|---|---|---|")
    tp = fp = fn = tn = 0
    for (tag, idx), want in sorted(KNOWN.items()):
        try:
            img_span, raw = measure(tag, idx)
        except Exception:
            continue
        ch = crop_height_of(tag, idx, img_span)
        span = ch - 70
        bands = data_rows(raw)
        fit = max_rows_in(ch, tag)
        flag = want >= fit
        cut = any(b > span for _, b in bands)
        if flag and cut:
            tp += 1
        elif flag:
            fp += 1
        elif cut:
            fn += 1
        else:
            tn += 1
        log.append("| `%s/%s` | %d | %d | %d | %s | %s |"
                   % (tag, idx, ch, want, fit, "是" if flag else "",
                      "**是**" if cut else ""))
    log.append("")
    log.append("- 命中 %d / 过报 %d / 漏报 %d / 正确放过 %d" % (tp, fp, fn, tn))
    log.append("- ⚠️ 过报（`flagged` 但没被切）是**设计使然**：标记的语义是"
               "「读数顶到了裁图天花板，**可能**不完整」，\n"
               "  不是「读数错了」。`rows_observed == rows_fit` = 需要看一眼；"
               "`rows_observed < rows_fit` = 明确没顶到，可直接放过。")
    if fn:
        log.append("- ⚠️⚠️ **漏报 %d**：被切了却没亮标记 —— 这比过报严重，要查。" % fn)
    log.append("")
    return not bad_rows and not fn


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--structure-only", action="store_true",
                    help="只做 C 层结构自检（不调 OCR）")
    a = ap.parse_args()

    log = ["# 裁图几何自证报告", "",
           "由 `_miyoushe/check_geom.py` 生成。真值来自原图墨迹带 + 人眼行数，"
           "**不来自 `max_rows_in` 自己**。", ""]
    ok = check_structure(log)
    if not a.structure_only:
        ok = check_images(log) and ok

    GEN2.mkdir(exist_ok=True)
    out = GEN2 / "geom_report.md"
    out.write_text("\n".join(log) + "\n", encoding="utf-8")
    print("OK" if ok else "FAILED")
    print("report -> %s" % out)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
