# -*- coding: utf-8 -*-
"""通用抗性表提取器：自动标定列中心，适配单横条/多状态表格、多尺寸卡面。

实测几何（19 篇同模板）：
  「抗性数据」标签块 → lx0 / yb
  状态行中心恒在 yb + 107 与 yb + 167（行距 60）
  列中心 ≈ lx0 + [31, 90, 150, 210, 269, 330, 390, 450]（不同卡面尺寸下略缩放到实测列距）
  行标题在 lx0 左侧；注释行（中文散文）需排除

列中心自标定：从本卡 dict 块里挑「窄数值块」（宽 ≤46），按间距中位数拟合 8 项等差数列；
   拟合失败则回落到模板相对偏移。随后**逐格裁切 + 4 倍放大 + 大间隔拼图 → 单次 OCR**，
   避免相邻格粘连（`10101010`）与格内装饰污染。
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

ELEMENTS = R.ELEMENTS
CN = ["火", "水", "风", "雷", "草", "冰", "岩", "物"]
TEMPLATE_REL = [31, 90, 150, 210, 269, 330, 390, 450]
ROW_DY = (107, 167)          # 状态行中心相对标签底部
ROW_BAND = (80, 240)         # 搜索带（相对标签底部）
PITCH_RANGE = (45, 80)
NARROW_W = 48
MATCH_TOL = 13
GAP = 36
SCALE = 4
CELL_DY = (-21, 17)
TOKEN = re.compile(r"^-?\d{1,4}$")
CJK = re.compile(r"[\u4e00-\u9fff]")
bad_cards: list[str] = []


def norm_val(t):
    t = str(t).strip()
    if "MM" in t.upper():
        return "IMM"
    # ⚠️ 只能用 [0-9]：`str.isdigit()` 对 `²` 等上标返回 True，但 `int('²')` 会抛 ValueError
    n = re.sub(r"[^0-9]", "", t)
    if not n:
        return t
    return ("-" if t.startswith("-") else "") + str(int(n))


def expand_values(toks):
    """把 token 列表规整为值列表：一个 token 里塞了多个值（如 `-3030`）时拆开。"""
    vals = []
    for t in toks:
        nv = norm_val(t)
        if TOKEN.match(nv) or nv == "IMM":
            vals.append(nv)
            continue
        groups = re.findall(r"-?\d+", t.replace("O", "0").replace("o", "0"))
        if len(groups) >= 2:
            vals.extend(norm_val(g) for g in groups)
        else:
            vals.append(nv)
    return vals


def tokens_of(text):
    out = []
    for raw in text.replace("\n", " ").split():
        t = raw.strip().strip("%,.")
        if t:
            out.append(t.replace("O", "0").replace("o", "0"))
    return out


def blocks_in_band(blocks, yb, lx0):
    lo, hi = yb + ROW_BAND[0], yb + ROW_BAND[1]
    picked = []
    for b in blocks:
        pts = b["box"]
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        if max(xs) > lx0 + 640:
            continue
        cy = (min(ys) + max(ys)) / 2
        if lo <= cy <= hi:
            picked.append({"cy": cy, "x0": min(xs), "x1": max(xs),
                           "t": str(b.get("text", "")).replace("\n", "").strip()})
    picked.sort(key=lambda d: d["cy"])
    rows = []
    for d in picked:
        if rows and d["cy"] - rows[-1]["cy"] <= 28:
            rows[-1]["items"].append(d)
        else:
            rows.append({"cy": d["cy"], "items": [d]})
    return rows


def row_stats(row, lx0):
    """返回（数值格计数, 数值块横向跨度, 行标题）。

    数值格计数按**数字组**算：粘连块（如 `170130130130130130130130` = 8 个值）算 8 个，
    否则整行会被误判成注释行而丢掉。含中文的块视为散文（注释），不计入。
    """
    n = 0
    xs = []
    for d in row["items"]:
        if d["x1"] <= lx0 - 6:
            continue
        t = d["t"]
        if CJK.search(t):
            continue
        if "MM" in t.upper():
            n += 1
            xs += [d["x0"], d["x1"]]
            continue
        toks = re.findall(r"-?\d+", t.replace("O", "0").replace("o", "0"))
        if toks:
            n += len(toks)
            xs += [d["x0"], d["x1"]]
    span = (max(xs) - min(xs)) if xs else 0
    title = "".join(d["t"] for d in row["items"] if d["x1"] <= lx0 - 6)
    return n, span, title


def is_state_row(row, lx0):
    """状态行：数值块窄且横跨整个表格；注释行是宽散文或零散窄块（如「6倍。」）。"""
    n, span, title = row_stats(row, lx0)
    if n >= 2 and span >= 250:
        return True
    return bool(title) and n >= 1


def observations(rows, lx0):
    """取窄数值块的中心，作为列标定的观测量。"""
    obs = []
    for row in rows:
        for d in row["items"]:
            if d["x1"] > lx0 - 6 and (d["x1"] - d["x0"]) <= NARROW_W:
                obs.append((d["x0"] + d["x1"]) / 2)
    return sorted(obs)


def score(cand, obs):
    return sum(1 for c in cand if any(abs(o - c) <= MATCH_TOL for o in obs))


def fit_centers(obs):
    """拟合 8 项等差数列（仅在模板匹配不佳时使用）。"""
    if len(obs) < 5:
        return None
    deltas = [b - a for a, b in zip(obs, obs[1:]) if PITCH_RANGE[0] <= b - a <= PITCH_RANGE[1]]
    if len(deltas) < 3:
        return None
    deltas.sort()
    pitch = deltas[len(deltas) // 2]
    best = None
    for base in obs:
        cand = [base + k * pitch for k in range(8)]
        if not (40 <= pitch <= 90):
            continue
        s = score(cand, obs)
        if best is None or s > best[1]:
            best = (cand, s)
    return best if best and best[1] >= 5 else None


def choose_centers(rows, lx0):
    """列中心标定：观测点足够时优先拟合（最准）；否则粘连块跨度；最后模板。

    ⚠️ 模板相对偏移在多数篇成立，但 04 这类实际列起点会整体偏 ~12px
    （模板 93..512 vs 实测 81..494）→ 若只用模板会把整行读错列
    （实测 04/003 把 `370,310,…,330` 读成 `310,310,370,…,310`）。
    """
    obs = observations(rows, lx0)
    if len(obs) >= 6:
        fit = fit_centers(obs)
        if fit:
            return fit[0], "fit"
    spans = []
    for row in rows:
        for d in row["items"]:
            if d["x1"] <= lx0 - 6 or CJK.search(d["t"]):
                continue
            t = (d["t"].replace("O", "0").replace("o", "0").replace("l", "1")
                 .replace("I", "1").replace("|", "1"))
            digits = re.sub(r"[^0-9]", "", t)
            # 整行可能被读成无分隔的纯数字串（如 `200200120020020020`）→ 按数字个数判断
            if len(digits) >= 8 and (d["x1"] - d["x0"]) >= 200:
                spans.append((d["x0"], d["x1"]))
    if spans and len(obs) < 6:
        x0 = min(s[0] for s in spans)
        x1 = max(s[1] for s in spans)
        pitch = (x1 - x0) / 8
        return [x0 + (i + 0.5) * pitch for i in range(8)], "span"

    template = [lx0 + r for r in TEMPLATE_REL]
    if not obs:
        return template, "template"
    t_score = score(template, obs)
    # 轻微缩放搜索（个别卡面列距略宽/窄）
    best_t, best_ts = template, t_score
    for s in (0.96, 0.98, 1.02, 1.04, 1.06, 1.08):
        cand = [lx0 + r * s for r in TEMPLATE_REL]
        sc = score(cand, obs)
        if sc > best_ts:
            best_t, best_ts = cand, sc
    fit = fit_centers(obs)
    if fit and fit[1] > best_ts and fit[1] >= 5:
        return fit[0], "fit"
    return best_t, "template"


def cell_crops(im, centers, cy, lx0):
    y0, y1 = int(cy) + CELL_DY[0], int(cy) + CELL_DY[1]
    # 格宽用满整格：紧排版卡（13 篇）三位数会被 0.86 收缩切掉尾位（`200`→`20`）
    half = int((centers[1] - centers[0]) / 2 * 0.98) if len(centers) > 1 else 26
    tiles = [im.crop((max(0, int(lx0 - 60)), y0, max(1, int(lx0 - 6)), y1))]
    for c in centers:
        tiles.append(im.crop((int(c - half), y0, int(c + half), y1)))
    return [t.resize((t.width * SCALE, t.height * SCALE), Image.LANCZOS) for t in tiles]


def safe_ocr(img_bytes, fmt="text"):
    """空图 / 无文字时返回空串，不抛异常。"""
    try:
        return R.ocr(img_bytes, fmt)
    except RuntimeError:
        return ""


def stitch(tiles, path=None):
    w = sum(t.width for t in tiles) + GAP * (len(tiles) - 1)
    h = max(t.height for t in tiles)
    canvas = Image.new("RGB", (w, h), (16, 16, 24))
    x = 0
    for t in tiles:
        canvas.paste(t, (x, (h - t.height) // 2))
        x += t.width + GAP
    if path:
        canvas.save(path)
    buf = io.BytesIO()
    canvas.save(buf, format="PNG")
    return safe_ocr(buf.getvalue())


def crop_ocr(im, centers, cy, lx0, path):
    """拼接识别；token 数不足 8 时逐格兜底，按格位落值。"""
    tiles = cell_crops(im, centers, cy, lx0)
    toks = tokens_of(stitch(tiles, path))
    if len(toks) == 8:
        return toks, "stitch"
    per = []
    for t in tiles:
        buf = io.BytesIO()
        t.save(buf, format="PNG")
        ts = tokens_of(safe_ocr(buf.getvalue()))
        per.append(ts[0] if ts else "")
    return per, "per-cell"


def extract(tag, idx, keep_crops=False):
    src = next(R.ROOT.glob(f"{tag}_*/{idx}.png"))
    blocks = R.ocr(src.read_bytes(), "dict")
    lab = next((b for b in blocks if "抗性数据" in str(b.get("text", ""))), None)
    if lab is None:
        bad_cards.append(f"{tag}/{idx}/no-label")
        return {"src": f"{tag}/{idx}", "error": "label-not-found"}
    xs = [p[0] for p in lab["box"]]
    ys = [p[1] for p in lab["box"]]
    lx0, yb = min(xs), max(ys)

    im = Image.open(src)
    rows = blocks_in_band(blocks, yb, lx0)
    if not rows:
        bad_cards.append(f"{tag}/{idx}/no-rows")
        return {"src": f"{tag}/{idx}", "error": "no-rows"}

    # 兜底：一张卡一个状态行都没识别出时，取最靠近 yb+107 且有数值块的那一行
    if not any(is_state_row(r, lx0) for r in rows):
        cand = [r for r in rows if row_stats(r, lx0)[0] >= 1]
        if cand:
            best = min(cand, key=lambda r: abs(r["cy"] - (yb + ROW_DY[0])))
            best["items"] = best["items"] + [{"cy": best["cy"], "x0": 0, "x1": lx0 - 10,
                                              "t": "常态"}]
            bad_cards.append(f"{tag}/{idx}/fallback-row")

    centers, calib = choose_centers(rows, lx0)

    outdir = R.RESCUE / tag
    outdir.mkdir(parents=True, exist_ok=True)

    states, notes = [], []
    for i, row in enumerate(rows, 1):
        n, span, title = row_stats(row, lx0)
        if not is_state_row(row, lx0):
            notes.append("".join(d["t"] for d in row["items"]))
            continue
        path = outdir / f"{idx}_r{i}.png"
        toks, mode = crop_ocr(im, centers, row["cy"], lx0, path)
        if mode == "per-cell":
            # 按格位落值：toks[0] 是行标题格，toks[1..8] 是 8 个数值格
            label = toks[0] if toks else title
            vals = [norm_val(t) for t in toks[1:9]]
            while len(vals) < 8:
                vals.append("")
        else:
            label, vals = "", []
            for t in toks:
                # 标题只可能是含汉字的串（避免把 `IMM` 这类无标题卡的首个值当标题）
                if not vals and CJK.search(t) and not TOKEN.match(norm_val(t)):
                    label += t
                else:
                    vals.append(norm_val(t))
        entry = {"label": label or title, "values": vals, "calib": calib, "mode": mode,
                 "raw": " ".join(toks)}
        if keep_crops:
            entry["crop"] = str(path)
        else:
            path.unlink(missing_ok=True)
        empty = sum(1 for v in vals if not v)
        if len(vals) != 8 or empty:
            bad_cards.append(f"{tag}/{idx}/r{i}/n={len(vals)}/empty={empty}")
            entry["warn"] = f"n={len(vals)}/empty={empty}"
        states.append(entry)

    note_txt = ""
    ny0, ny1 = int(yb) + 195, int(yb) + 290
    strip = im.crop((max(0, int(lx0 - 60)), ny0, int(lx0 + 560), ny1))
    strip = strip.resize((strip.width * 4, strip.height * 4), Image.LANCZOS)
    nbuf = io.BytesIO()
    strip.save(nbuf, format="PNG")
    note_txt = safe_ocr(nbuf.getvalue()).replace("\n", " ").strip()

    if len(states) == 1 and ("该状态下" in note_txt or "击破" in note_txt):
        bad_cards.append(f"{tag}/{idx}/missing-2nd-row")

    return {"src": f"{tag}/{idx}", "size": im.size, "calib": calib,
            "rows": states, "notes": " ".join(notes), "note_text": note_txt}


def main():
    tags = sys.argv[1:] or ["01", "02", "03", "04", "05", "06", "07", "08", "09", "10",
                            "11", "12", "13", "14", "15", "16", "17", "18", "19"]
    for tag in tags:
        d = R.OCR_DIR / tag
        if not d.is_dir():
            continue
        results = []
        for txt in sorted(d.glob("*.txt")):
            try:
                results.append(extract(tag, txt.stem))
            except Exception as e:                      # 单卡失败不影响整批
                bad_cards.append(f"{tag}/{txt.stem}/EXC:{type(e).__name__}")
                results.append({"src": f"{tag}/{txt.stem}", "error": str(e)[:120]})
            if len(results) % 10 == 0:
                print(f"  {tag}: {len(results)} cards done", flush=True)
        out = R.RESCUE / f"{tag}_states.json"
        out.write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding="utf-8")
        shapes = [len(r["values"]) for x in results for r in x.get("rows", [])]
        ok = sum(1 for s in shapes if s == 8)
        print(f"{tag}: cards={len(results)} rows={len(shapes)} ok8={ok} "
              f"bad={len(shapes) - ok} multi={sum(1 for x in results if len(x.get('rows', [])) > 1)}")
    print("bad_cards:", bad_cards)


if __name__ == "__main__":
    main()
