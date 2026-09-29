# -*- coding: utf-8 -*-
"""裁图补偿：状态文字压行导致整卡 OCR 读不全抗性表时，按坐标裁出抗性条单独放大重识别。

流程：
  1. 整卡以 data.format=dict 识别 → 找到「抗性数据」文字块的坐标；
  2. 抗性值在图标行下方（约 label 底部 +85 ~ +125），裁 x 40~720 的横条；
  3. 放大 3 倍保存到 _rescue/<篇>/<图号>_resist.png（这份也是给人复核用的图）；
  4. 对裁图重识别 → 按位置解析 8 项。
"""
import base64
import io
import json
import sys
import urllib.request
from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
OCR_DIR = HERE / "_ocr"
ROOT = HERE / "miyoushe_3572896"
RESCUE = HERE / "_rescue"
RESCUE.mkdir(exist_ok=True)

API = "http://127.0.0.1:1224/api/ocr"
OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))
ELEMENTS = ["pyro", "hydro", "anemo", "electro", "dendro", "cryo", "geo", "physical"]


def ocr(image_bytes: bytes, fmt: str):
    body = {"base64": base64.b64encode(image_bytes).decode("ascii"),
            "options": {"data.format": fmt}}
    req = urllib.request.Request(API, data=json.dumps(body).encode(),
                                headers={"Content-Type": "application/json"})
    with OPENER.open(req, timeout=300) as r:
        res = json.loads(r.read().decode("utf-8"))
    if res.get("code") != 100:
        raise RuntimeError(f"code={res.get('code')} {str(res.get('data'))[:100]}")
    return res["data"]


def find_label_bottom(blocks):
    for b in blocks:
        if "抗性数据" in str(b.get("text", "")):
            return max(p[1] for p in b["box"]), min(p[0] for p in b["box"]), max(p[0] for p in b["box"])
    return None, None, None


def norm(s: str) -> str:
    t = s.replace("O", "0").replace("o", "0").replace("l", "1").replace("I", "1").replace("|", "1")
    return t.strip()


def parse_strip(text: str):
    """把裁图识别结果里的 8 个值按位置取出来。"""
    vals = []
    for raw in text.replace("\n", " ").split():
        t = raw.strip()
        if not t:
            continue
        if "IMM" in t.upper() or t.upper() in {"MM", "MN", "S"}:
            vals.append("IMM")
        else:
            n = norm(t)
            nums = [int(x) for x in n.replace("%", "").split(",") if x.strip().lstrip("-").isdigit()]
            vals.extend(str(v) for v in nums)
    return vals


def rescue(tag: str, idx: str):
    src = next(ROOT.glob(f"{tag}_*/{idx}.png"))
    blocks = ocr(src.read_bytes(), "dict")
    y_bottom, _, _ = find_label_bottom(blocks)
    if y_bottom is None:
        return {"src": f"{tag}/{idx}", "error": "label-not-found"}

    im = Image.open(src)
    box = (40, int(y_bottom) + 85, 720, int(y_bottom) + 125)
    strip = im.crop(box)
    strip = strip.resize((strip.width * 3, strip.height * 3), Image.LANCZOS)
    outdir = RESCUE / tag
    outdir.mkdir(exist_ok=True)
    strip_path = outdir / f"{idx}_resist.png"
    strip.save(strip_path)

    buf = io.BytesIO()
    strip.save(buf, format="PNG")
    text = ocr(buf.getvalue(), "text")
    vals = parse_strip(text)
    return {"src": f"{tag}/{idx}", "label_bottom": y_bottom, "crop": str(strip_path),
            "values": vals, "raw": text.replace("\n", " ")[:200],
            "mapped": {ELEMENTS[i]: v for i, v in enumerate(vals[:8])}}


def main():
    tag = sys.argv[1] if len(sys.argv) > 1 else "01"
    indices = sys.argv[2:] or None
    results = []
    for txt in sorted((OCR_DIR / tag).glob("*.txt")):
        if indices and txt.stem not in indices:
            continue
        results.append(rescue(tag, txt.stem))
    out = RESCUE / f"{tag}.json"
    out.write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding="utf-8")
    for r in results:
        print(json.dumps(r, ensure_ascii=False))
    print(f"\nwritten {out}")


if __name__ == "__main__":
    main()
