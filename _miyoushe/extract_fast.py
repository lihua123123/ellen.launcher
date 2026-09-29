# -*- coding: utf-8 -*-
"""快速版抗性提取 v2：窄条定位 + 小横条 dict + 多实例端口池 + 线程池。

实测依据：
  - 整图 dict（旧流程用来定位）1.25~4.0s，占旧流程 85% 时间；
  - 左侧窄条 (0~700)@0.5× 定位只要 ~0.5s，标签仍可识别
    （但小字号数值会丢，所以只用它定位，不用它取数）；
  - **只对状态行所在的小横条 (0~960 × 52px) 做 dict**：0.13~0.25s，直接拿到每格的
    位置与文字 —— 位置可用于列标定，也能解开全图时粘连的 `10101010`；
  - 单实例内部串行（同端口并发 4 次无加速），故用多实例端口池 + 卡片级线程池。

输出格式与 extract_all.py 完全一致，下游 table_all / health / merge_all 无需改动。
"""
import io
import json
import re
import sys
import threading
from pathlib import Path

HERE = Path(__file__).parent
sys.path.insert(0, str(HERE))
import ocr_pool  # noqa: E402
import extract_all as E  # noqa: E402
import rescue_resist as R  # noqa: E402
from PIL import Image  # noqa: E402

# 每个工作线程固定绑定一个实例端口（避免共享轮询器把请求堆到同一实例）
_TLS = threading.local()
E.safe_ocr = lambda b, fmt="text": ocr_pool.ocr(
    b, fmt, port=getattr(_TLS, "port", None))

LOCATE_WINDOWS = [((0, 700), 0.5), ((340, 1040), 0.5), ((0, 960), 1.0)]
BAND_X1 = 960
BAND_HALF = 26
ROW_STEP = 60
MAX_ROWS = 3
NOTE_Y = (145, 300)
bad_cards: list[str] = []


def to_items(blocks):
    items = []
    for b in blocks:
        xs = [p[0] for p in b["box"]]
        ys = [p[1] for p in b["box"]]
        items.append({"cy": (min(ys) + max(ys)) / 2, "x0": min(xs), "x1": max(xs),
                      "t": str(b.get("text", "")).replace("\n", "").strip()})
    return items


def locate(im):
    """定位「抗性数据」标签 → (lx0, yb, blocks)。只识别窄条，比整图快 5 倍以上。"""
    w, h = im.size
    for (x0, x1), scale in LOCATE_WINDOWS:
        c = im.crop((x0, 0, min(x1, w), h))
        if scale != 1.0:
            c = c.resize((max(1, int(c.width * scale)), max(1, int(c.height * scale))),
                         Image.LANCZOS)
        buf = io.BytesIO()
        c.save(buf, format="PNG")
        blocks = E.safe_ocr(buf.getvalue(), "dict")
        if not isinstance(blocks, list):
            continue
        fixed = [{"box": [[x0 + p[0] / scale, p[1] / scale] for p in b["box"]],
                  "text": str(b.get("text", ""))} for b in blocks]
        labs = [b for b in fixed if "抗性数据" in b["text"]]
        if not labs:
            continue
        lab = min(labs, key=lambda b: min(p[0] for p in b["box"]))
        lx0 = min(p[0] for p in lab["box"])
        yb = max(p[1] for p in lab["box"])
        # 13 篇（2048×1080）的抗性表在卡片顶部，故下界不能按比例卡太死
        if not (0 <= lx0 <= 1200 and 0.03 * h <= yb <= 0.97 * h):
            continue
        return lx0, yb, fixed
    return None, None, None


def band_dict(im, cy):
    """对一张小横条做 dict：位置与文字一次拿到（远比整图便宜）。越界则返回空。"""
    top = max(0, int(cy - BAND_HALF))
    bottom = min(im.height, int(cy + BAND_HALF))
    if bottom - top < 8:                    # 该行完全落在图片外
        return []
    c = im.crop((0, top, min(BAND_X1, im.width), bottom))
    buf = io.BytesIO()
    c.save(buf, format="PNG")
    blocks = E.safe_ocr(buf.getvalue(), "dict")
    return to_items(blocks) if isinstance(blocks, list) else []


def extract(tag, idx, keep_crops=False):
    src = next(R.ROOT.glob(f"{tag}_*/{idx}.png"))
    im = Image.open(src)
    lx0, yb, _ = locate(im)
    if lx0 is None:
        bad_cards.append(f"{tag}/{idx}/no-label")
        return {"src": f"{tag}/{idx}", "error": "label-not-found"}

    bands = []
    for k in range(MAX_ROWS):
        items = band_dict(im, yb + E.ROW_DY[0] + ROW_STEP * k)
        if items:
            bands.append({"cy": yb + E.ROW_DY[0] + ROW_STEP * k, "items": items})

    rows = [b for b in bands if E.is_state_row(b, lx0)]
    if not rows and bands:
        cand = [b for b in bands if E.row_stats(b, lx0)[0] >= 1]
        if cand:
            best = min(cand, key=lambda r: abs(r["cy"] - (yb + E.ROW_DY[0])))
            best["items"].append({"cy": best["cy"], "x0": 0, "x1": lx0 - 10, "t": "常态"})
            rows = [best]
            bad_cards.append(f"{tag}/{idx}/fallback-row")
    if not rows:
        bad_cards.append(f"{tag}/{idx}/no-rows")
        return {"src": f"{tag}/{idx}", "error": "no-rows"}

    centers, calib = E.choose_centers(rows, lx0)
    outdir = R.RESCUE / tag
    outdir.mkdir(parents=True, exist_ok=True)

    states = []
    for i, row in enumerate(rows, 1):
        _, _, title = E.row_stats(row, lx0)
        nums = [d for d in sorted(row["items"], key=lambda d: d["x0"])
                if d["x0"] > lx0 - 6
                and ("MM" in d["t"].upper() or E.TOKEN.match(E.norm_val(d["t"])))]
        path = outdir / f"{idx}_r{i}.png"
        if len(nums) == 8:
            label, mode = title, "band-dict"
            vals = [E.norm_val(d["t"]) for d in nums]
        else:
            # 粘连行（如 `1101101101`）→ 按列裁格重识别
            toks, mode = E.crop_ocr(im, centers, row["cy"], lx0, path)
            if mode == "per-cell":
                label = toks[0] if toks else title
                vals = [E.norm_val(t) for t in toks[1:9]]
                while len(vals) < 8:
                    vals.append("")
            else:
                label, raw_vals = "", []
                for t in toks:
                    if not raw_vals and E.CJK.search(t) and not E.TOKEN.match(E.norm_val(t)):
                        label += t
                    else:
                        raw_vals.append(t)
                vals = E.expand_values(raw_vals)   # 拆开 `-3030` 这类单 token 多值
            label = label or title
        if not keep_crops:
            path.unlink(missing_ok=True)
        empty = sum(1 for v in vals if not v)
        if len(vals) != 8 or empty:
            bad_cards.append(f"{tag}/{idx}/r{i}/n={len(vals)}/empty={empty}")
            entry_warn = f"n={len(vals)}/empty={empty}"
        else:
            entry_warn = None
        # 置信度：只有横条 dict 直接给出「8 个独立数值块」才算高置信。
        # 实测（04/003 行1）粘连串被截断时，逐格裁切会整行错列且值都像模像样，
        # 数字预算之类的交叉校验也无法识别 → 这类行一律标记为不确定，不参与入库。
        confident = mode == "band-dict" and len(nums) == 8
        # 空值过多 = 该横条不是真正的抗性行（如抓到 Data 水印/说明行）→ 丢弃
        if empty >= 4 or (len(vals) == 8 and empty and empty >= len(vals) / 2):
            continue
        entry = {"label": label, "values": vals, "calib": calib, "mode": mode,
                 "confident": confident, "raw": " ".join(d["t"] for d in nums)}
        if entry_warn:
            entry["warn"] = entry_warn
        states.append(entry)

    note_parts = ["".join(d["t"] for d in b["items"]) for b in bands if b not in rows]
    note_band = " ".join(p for p in note_parts if p)
    # 注释优先用横条里已经识别到的文字（免费）；不足时才单独裁图重识别
    # （单独裁图 620×155@3× ≈ 0.87MP，是横条 0.05MP 的 17 倍，很贵）
    note_txt = note_band if len(E.CJK.findall(note_band)) >= 6 else ""
    if not note_txt:
        ny0 = max(0, int(yb + NOTE_Y[0]))
        ny1 = min(im.height, int(yb + NOTE_Y[1]))
        if ny1 - ny0 >= 8:
            strip = im.crop((max(0, int(lx0 - 60)), ny0, min(im.width, int(lx0 + 560)), ny1))
            strip = strip.resize((strip.width * 2, strip.height * 2), Image.LANCZOS)
            nbuf = io.BytesIO()
            strip.save(nbuf, format="PNG")
            note_txt = E.safe_ocr(nbuf.getvalue()).replace("\n", " ").strip()
    if len(states) == 1 and ("该状态下" in note_txt or "击破" in note_txt):
        bad_cards.append(f"{tag}/{idx}/missing-2nd-row")

    return {"src": f"{tag}/{idx}", "size": im.size, "calib": calib,
            "rows": states, "notes": " ".join(note_parts), "note_text": note_txt}


def _one(tag, idx):
    try:
        return extract(tag, idx)
    except Exception as e:
        bad_cards.append(f"{tag}/{idx}/EXC:{type(e).__name__}")
        return {"src": f"{tag}/{idx}", "error": f"{type(e).__name__}: {str(e)[:100]}"}


def main():
    ports = ocr_pool.refresh()
    tags = sys.argv[1:] or [f"{i:02d}" for i in range(1, 20)]
    print(f"ports={ports} workers={len(ports)}", flush=True)
    for tag in tags:
        d = R.OCR_DIR / tag
        if not d.is_dir():
            continue
        cards = [p.stem for p in sorted(d.glob("*.txt"))]
        buckets = [cards[i::len(ports)] for i in range(len(ports))]
        results: dict[str, dict] = {}
        lock = threading.Lock()

        def worker(bucket, port):
            _TLS.port = port
            for c in bucket:
                r = _one(tag, c)
                with lock:
                    results[c] = r

        threads = [threading.Thread(target=worker, args=(b, p)) for b, p in zip(buckets, ports)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()
        ordered = [results[c] for c in cards]
        (R.RESCUE / f"{tag}_states.json").write_text(
            json.dumps(ordered, ensure_ascii=False, indent=1), encoding="utf-8")
        shapes = [len(r["values"]) for x in ordered for r in x.get("rows", [])]
        ok = sum(1 for s in shapes if s == 8)
        print(f"{tag}: cards={len(ordered)} rows={len(shapes)} ok8={ok} "
              f"bad={len(shapes) - ok} multi={sum(1 for x in ordered if len(x.get('rows', [])) > 1)}",
              flush=True)
    print("bad_cards:", bad_cards, flush=True)


if __name__ == "__main__":
    main()
