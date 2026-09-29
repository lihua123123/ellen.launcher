# -*- coding: utf-8 -*-
"""03 篇（骗骗花）抗性表提取器 v2 —— 按列拼图识别。

布局实测（1920×1080，同模板）：
  「抗性数据(单位%)」标签块 x=66..308，y_bottom≈763~766
  表格 = 左标题列(x 0..60) + 8 个数值列（中心恒为 97/156/216/276/335/396/456/516，
        跨 7 张卡实测漂移 ≤2px）
  状态行 1~2 行（有的卡只有一行；行标题可能缺失，如 004）
  ⚠️ 格内下方可能有装饰图形被读成多余数字（001 第 5 格 → "135"）
  ⚠️ 相邻格直接 OCR 会粘连（002 → "3535"）

做法：每格单独裁切（列中心 ±26，行中心 -20..+16）→ 拼成一张大间隔图 → 单次 OCR
      → 首个非数字 token 即行标题，其余为 8 项值（保留负号）。
"""
import io
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))
import rescue_resist as R  # noqa: E402
from PIL import Image  # noqa: E402

TAG = "03"
ELEMENTS = R.ELEMENTS
CN = ["火", "水", "风", "雷", "草", "冰", "岩", "物"]

COL_CENTERS_REL = [31, 90, 150, 210, 269, 330, 390, 450]   # 相对标签 x0（实测两套模板一致）
COL_HALF = 26
LABEL_REL = (-60, -6)   # 行标题格：相对标签 x0
TABLE_REL = (-60, 520)  # 表格横向范围（含行标题列）：相对标签 x0
ROW_Y = (-20, 16)
GAP = 36
SCALE = 4
BAND_OFFSET = (40, 330)
NOTE_Y = (200, 290)    # 注释行相对标签底部的裁剪范围
ROW_GAP = 26
MIN_NUMERIC = 5        # 纯数字块个数下限（无行标题时）
MIN_NUMERIC_WITH_LABEL = 3   # 有行标题时放宽（OCR 可能漏认部分单元格）

TOKEN = re.compile(r"^-?\d{1,4}$")
bad_rows: list[str] = []


def group_rows(blocks, yb, lx0):
    lo, hi = yb + BAND_OFFSET[0], yb + BAND_OFFSET[1]
    picked = []
    for b in blocks:
        pts = b["box"]
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        if min(xs) >= lx0 + TABLE_REL[1] or max(xs) <= lx0 + TABLE_REL[0]:
            continue                        # 表格以外的文字（含左/右面板）
        cy = (min(ys) + max(ys)) / 2
        if lo <= cy <= hi:
            picked.append((cy, min(xs), max(xs), str(b.get("text", "")).replace("\n", "").strip()))
    picked.sort()
    rows = []
    for cy, x0, x1, text in picked:
        if rows and cy - rows[-1]["cy"] <= ROW_GAP:
            rows[-1]["items"].append((x0, x1, text))
            rows[-1]["cy"] = (rows[-1]["cy"] + cy) / 2
        else:
            rows.append({"cy": cy, "items": [(x0, x1, text)]})
    return rows


def row_parts(row, lx0):
    """返回（行标题文本, 数值块文本按 x 排序）。"""
    label = "".join(t for x0, x1, t in row["items"] if x1 <= lx0 + LABEL_REL[1])
    vals = [t for x0, x1, t in sorted(row["items"]) if x0 > lx0 + LABEL_REL[1]]
    return label, vals


def numeric_count(row):
    """数值块里「纯数字」的个数。状态行 ≈ 8；注释行 ≈ 0（注释是中文散文）。"""
    n = 0
    for x0, x1, t in row["items"]:
        if "MM" in t.upper() or TOKEN.match(norm_val(t)):
            n += 1
    return n


def compose(im, cy, lx0):
    """左标题格 + 8 个数值格，各自放大后按大间隔拼接。"""
    y0, y1 = int(cy) + ROW_Y[0], int(cy) + ROW_Y[1]
    x_lo, x_hi = lx0 + LABEL_REL[0], lx0 + LABEL_REL[1]
    tiles = [im.crop((x_lo, y0, x_hi, y1))]
    for r in COL_CENTERS_REL:
        cx = lx0 + r
        tiles.append(im.crop((cx - COL_HALF, y0, cx + COL_HALF, y1)))
    tiles = [t.resize((t.width * SCALE, t.height * SCALE), Image.LANCZOS) for t in tiles]
    w = sum(t.width for t in tiles) + GAP * (len(tiles) - 1)
    h = max(t.height for t in tiles)
    canvas = Image.new("RGB", (w, h), (16, 16, 24))
    x = 0
    for t in tiles:
        canvas.paste(t, (x, (h - t.height) // 2))
        x += t.width + GAP
    return canvas


def tokens_of(text):
    out = []
    for raw in text.replace("\n", " ").split():
        t = raw.strip().strip("%,.")
        if not t:
            continue
        if "MM" in t.upper():
            out.append("IMM")
        else:
            t2 = t.replace("O", "0").replace("o", "0")
            out.append(t2)
    return out


def norm_val(t):
    n = "".join(c for c in t if c.isdigit())
    if not n:
        return t
    sign = "-" if t.startswith("-") else ""
    return f"{sign}{int(n)}"


def extract(idx):
    src = next(R.ROOT.glob(f"{TAG}_*/{idx}.png"))
    blocks = R.ocr(src.read_bytes(), "dict")
    yb, lx0_raw, _ = R.find_label_bottom(blocks)
    if yb is None:
        return {"src": f"{TAG}/{idx}", "error": "label-not-found"}
    im = Image.open(src)
    outdir = R.RESCUE / TAG
    outdir.mkdir(parents=True, exist_ok=True)
    lx0 = int(lx0_raw)

    states, notes = [], []
    for i, row in enumerate(group_rows(blocks, yb, lx0), 1):
        dict_label, dict_vals = row_parts(row, lx0)
        if not (numeric_count(row) >= MIN_NUMERIC
                or (dict_label and numeric_count(row) >= MIN_NUMERIC_WITH_LABEL)):
            notes.append("".join(dict_vals))
            continue
        canvas = compose(im, row["cy"], lx0)
        path = outdir / f"{idx}_r{i}.png"
        canvas.save(path)
        buf = io.BytesIO()
        canvas.save(buf, format="PNG")
        toks = tokens_of(R.ocr(buf.getvalue(), "text"))

        label = ""
        vals = []
        for t in toks:
            if not vals and not TOKEN.match(t):
                label += t
            else:
                vals.append(norm_val(t))

        entry = {"label": label or dict_label, "values": vals, "source": "cols",
                 "crop": str(path), "raw": " ".join(toks)}
        if len(vals) != 8:
            entry["warn"] = f"token-count={len(vals)}"
            bad_rows.append(f"{idx}/r{i}")
        states.append(entry)
    # 注释行（状态含义、护甲说明）单独高清识别
    ny0 = int(yb) + NOTE_Y[0]
    note_strip = im.crop((lx0 + LABEL_REL[0], ny0, lx0 + TABLE_REL[1], int(yb) + NOTE_Y[1]))
    note_strip = note_strip.resize((note_strip.width * 4, note_strip.height * 4), Image.LANCZOS)
    note_path = outdir / f"{idx}_note.png"
    note_strip.save(note_path)
    nbuf = io.BytesIO()
    note_strip.save(nbuf, format="PNG")
    try:
        note_text = R.ocr(nbuf.getvalue(), "text").replace("\n", " ").strip()
    except RuntimeError:          # code=101 空图（该卡无注释行）
        note_text = ""

    res = {"src": f"{TAG}/{idx}", "label_bottom": yb, "rows": states,
           "notes": " ".join(notes), "note_text": note_text, "note_crop": str(note_path)}
    # 稽查：注释提到「该状态下/击破」说明应有第二个状态行
    if len(states) == 1 and ("该状态下" in note_text or "击破" in note_text):
        res["warn"] = "possible-missing-state-row"
        bad_rows.append(f"{idx}/missing-2nd-row")
    return res


def main():
    bad_rows.clear()
    idxs = sys.argv[1:] or [p.stem for p in sorted((R.OCR_DIR / TAG).glob("*.txt"))]
    results, bad = [], []
    for idx in idxs:
        r = extract(idx)
        results.append(r)
        shapes = [len(row["values"]) for row in r.get("rows", [])]
        if not shapes or any(s != 8 for s in shapes):
            bad.append((idx, shapes))
        print(f"{idx}: nrows={len(shapes)} shapes={shapes} "
              f"labels={[row['label'] for row in r.get('rows', [])]}")
    out = R.RESCUE / f"{TAG}_states.json"
    out.write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding="utf-8")
    print("bad:", bad)
    print("bad_rows:", bad_rows)
    print("written", out.name, len(results))


if __name__ == "__main__":
    main()
