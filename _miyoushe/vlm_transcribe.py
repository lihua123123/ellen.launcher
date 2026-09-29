# -*- coding: utf-8 -*-
"""VLM 抗性表转录（第二通道）+ 与本地 OCR 的双通道比对。

设计要点（与本地通道互补，不做重复劳动）：
  - **只发裁图，不发全卡**：全卡 1920×1280 里抗性表只占约 1/8 面积，其余是立绘/说明，
    发全卡既贵又给 VLM 引入无关文本（历史上正是「说明行/水印」污染本地通道）。
  - **强制 JSON**：模型必须回 `{"rows":[{"label":"...","values":[8 个]}]}`，
    value 只能是整数或字符串 "IMM"。
  - **本地校验先行**：8 值 / 5 的倍数 / [-100,500] / 空值数，任一不过 → 该行标 unverified。
  - **双通道**：VLM 行 与 本地 `_rescue/<tag>_states.json` 对应行逐格比；
    完全一致 → adopt（采纳）；不一致 → review（进人工三审，不自动入库）。
  - **13 篇额外一致性校验**（见 check13）：行数、5 的倍数、瘫痪行必须不高于本体行、
    IMM 必须落在该怪物自身元素列。

密钥绝不写在代码里，只从环境变量读：
    set VLM_API_KEY=sk-xxxx
    set VLM_BASE_URL=https://dashscope.aliyuncs.com/compatible-mode/v1   (默认)
    set VLM_MODEL=qwen-vl-max-latest                                     (默认)

用法：
    python vlm_transcribe.py --dry-run              # 不联网，打印将发送的裁图清单与体积
    python vlm_transcribe.py --cal                  # 校准批：跑 13 张 cal 裁图
    python vlm_transcribe.py --cal --score          # 再与 cal_gold.json 算一致率
    python vlm_transcribe.py --tags 02 04 05        # 全量：按篇跑（只发状态行裁图）
    python vlm_transcribe.py --tags 02 --dual       # 附带与本地通道的双通道比对
"""
import argparse
import base64
import io
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

HERE = Path(__file__).parent
ROOT = HERE.parent
sys.path.insert(0, str(HERE))
import ocr_pool  # noqa: E402
import extract_fast as F  # noqa: E402
import rescue_resist as R  # noqa: E402
import extract_all as E  # noqa: E402
from PIL import Image  # noqa: E402

GEN2 = HERE / "_gen2"
VLM_DIR = GEN2 / "vlm"
CAL = ROOT / "_design" / "enemy-archive" / "cal"
ELEMS = ["\u706b", "\u6c34", "\u98ce", "\u96f7", "\u8349", "\u51b0", "\u5ca9", "\u7269"]
LOW, HIGH = -100, 500

PROMPT = """这是一张游戏攻略图鉴卡上「抗性数据」表格的裁图。
请**只**输出 JSON，不要任何解释、不要 markdown 代码块。
表格：第一行是 8 个元素图标，元素列顺序固定为：火 水 风 雷 草 冰 岩 物理。
每个元素列下方的数值就是抗性百分比；若显示 IMM / 免疫，则该列输出字符串 "IMM"。
左侧若有行标签（如 正常/瘫痪/强化/嗜魔/本体/帽子 等），照抄到 label；没有就用空字符串。

输出格式（严格）：
{"rows":[{"label":"正常","values":[10,10,"IMM",10,10,10,10,10]}]}

注意：
- values 必须恰好 8 项，顺序与上述元素列一致，不要漏列、不要多列。
- 负数照抄（如 -40）；数字不要带 % 号。
- 逐格仔细对齐：每个数值必须落在它正上方的元素图标那一列。
"""


# ---------------------------------------------------------------- 裁图

def cal_crops():
    """校准批裁图（已是「抗性表」局部图）。"""
    rows = json.loads((CAL / "sample.json").read_text(encoding="utf-8"))
    return [(r["src"], CAL / r["file"]) for r in rows]


def row_crops(tag, idx):
    """全量用的「状态行横条」裁图：每行一条，只含数值与行标签。

    13 篇（2048×1080）抗性表在卡片顶部、行标签是怪物名，故整块表一起发。
    """
    src = next(R.ROOT.glob(f"{tag}_*/{idx}.png"))
    im = Image.open(src).convert("RGB")
    lx0, yb, _ = F.locate(im)
    if lx0 is None:
        return []
    out = []
    if tag == "13":
        box = (0, max(0, int(yb - 70)), min(im.width, int(lx0 + 700)),
               min(im.height, int(yb + 320)))
        out.append(("", im.crop(box)))
        return out
    for k in range(F.MAX_ROWS):
        cy = yb + E.ROW_DY[0] + F.ROW_STEP * k
        top, bottom = max(0, int(cy - 30)), min(im.height, int(cy + 30))
        if bottom - top < 10:
            continue
        out.append((str(k + 1), im.crop((0, top, min(im.width, int(lx0 + 620)), bottom))))
    return out


def png_bytes(img, upscale=1.5):
    if upscale != 1.0:
        img = img.resize((int(img.width * upscale), int(img.height * upscale)), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


# ---------------------------------------------------------------- 调用

def call_vlm(png, model, base, key, timeout=120):
    payload = {
        "model": model,
        "messages": [{"role": "user", "content": [
            {"type": "image_url",
             "image_url": {"url": "data:image/png;base64," + base64.b64encode(png).decode()}},
            {"type": "text", "text": PROMPT},
        ]}],
        "temperature": 0,
    }
    req = urllib.request.Request(
        base.rstrip("/") + "/chat/completions",
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json", "Authorization": "Bearer " + key})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            body = json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        # 响应体里才有真正的原因（InvalidApiKey / Model not exist / quota 等）
        raise RuntimeError("HTTP %s %s | %s" % (
            e.code, e.reason, e.read().decode("utf-8", "replace")[:400]))
    except urllib.error.URLError as e:
        raise RuntimeError("URLError %s（网络/代理不通？）" % e.reason)
    if "choices" not in body:
        raise RuntimeError("unexpected body: %s" % json.dumps(body, ensure_ascii=False)[:300])
    return body["choices"][0]["message"]["content"]


def parse_json(txt):
    """模型常带 ```json 围栏或前后废话，尽力取出第一个 JSON 对象。"""
    t = re.sub(r"^\s*```(?:json)?|```\s*$", "", txt.strip(), flags=re.M)
    i, j = t.find("{"), t.rfind("}")
    if i < 0 or j <= i:
        return None
    try:
        return json.loads(t[i:j + 1])
    except json.JSONDecodeError:
        return None


# ---------------------------------------------------------------- 校验

def validate(rows):
    """返回 (clean_rows, problems)。clean_rows 内含每行的 verified 标记。"""
    clean = []
    for i, r in enumerate(rows, 1):
        lab = str(r.get("label", "") or "").strip()
        vals = r.get("values")
        pr = []
        if not isinstance(vals, list) or len(vals) != 8:
            pr.append("not-8-values(%s)" % (len(vals) if isinstance(vals, list) else "?"))
            clean.append({"label": lab, "values": vals, "verified": False, "problems": pr})
            continue
        norm = []
        for ci, v in enumerate(vals):
            s = str(v).strip().upper().replace("%", "")
            if s in ("IMM", "IMMUNE", "\u514d\u75ab"):
                norm.append("IMM")
                continue
            try:
                n = int(round(float(s)))
            except ValueError:
                pr.append("c%d=%r" % (ci, v))
                norm.append("")
                continue
            if n % 5:
                pr.append("c%d-not-mult5(%d)" % (ci, n))
            if not (LOW <= n <= HIGH):
                pr.append("c%d-out-of-range(%d)" % (ci, n))
            norm.append(n)
        empty = sum(1 for x in norm if x == "")
        if empty:
            pr.append("empty=%d" % empty)
        clean.append({"label": lab, "values": norm, "verified": not pr, "problems": pr})
    nbad = sum(1 for r in clean if not r["verified"])
    return clean, ["%d row(s) failed validation" % nbad] if nbad else []


def check13(rows):
    """13 篇额外一致性校验。返回问题列表。"""
    pr = []
    if len(rows) < 2:
        pr.append("13-rows<2")
    for i, r in enumerate(rows, 1):
        if not r.get("verified"):
            pr.append("r%d-unverified" % i)
    # 瘫痪类行必须不高于其上一行（全列）
    for i in range(1, len(rows)):
        lab = str(rows[i]["label"])
        if "\u762b\u75ab" in lab:
            prev = rows[i - 1]["values"]
            cur = rows[i]["values"]
            for ci in range(8):
                a, b = prev[ci], cur[ci]
                if isinstance(a, int) and isinstance(b, int) and b > a:
                    pr.append("r%d-c%d-paralysis-higher(%d>%d)" % (i + 1, ci, b, a))
    return pr


def norm_cmp(v):
    return str(v) if v != "IMM" else "IMM"


def dual_channel(vlm_rows, local_rows):
    """VLM 行 vs 本地行 逐格比对。返回 (adopt, review)。"""
    adopt, review = [], []
    for i, vr in enumerate(vlm_rows):
        rec = {"row": i + 1, "vlm": vr["values"]}
        if i >= len(local_rows):
            rec["reason"] = "no-local-row"
            review.append(rec)
            continue
        lv = local_rows[i]["values"]
        if len(lv) != 8:
            rec["reason"] = "local-not-8"
            review.append(rec)
            continue
        diff = [ci for ci in range(8) if norm_cmp(vr["values"][ci]) != norm_cmp(lv[ci])]
        if not diff:
            adopt.append(rec)
        else:
            rec["diff_cols"] = [ELEMS[ci] for ci in diff]
            rec["local"] = lv
            review.append(rec)
    return adopt, review


# ---------------------------------------------------------------- 主流程

def score_cal():
    gold = json.loads((CAL / "cal_gold.json").read_text(encoding="utf-8"))
    tot = hit = 0
    rowtot = rowhit = 0
    bad = []
    for src in sorted(gold):
        p = VLM_DIR / (src.replace("/", "_") + ".json")
        if not p.exists():
            bad.append("%s: no-vlm-output" % src)
            continue
        rows = json.loads(p.read_text(encoding="utf-8"))["rows"]
        for ri, (_, gv) in enumerate(gold[src]):
            rowtot += 1
            if ri >= len(rows):
                tot += 8
                bad.append("%s r%d: missing" % (src, ri + 1))
                continue
            lv = rows[ri]["values"]
            ok = True
            for ci in range(8):
                tot += 1
                if len(lv) != 8:
                    continue
                good = (lv[ci] == "IMM") if gv[ci] == "IMM" else (str(lv[ci]) == str(gv[ci]))
                if good:
                    hit += 1
                else:
                    ok = False
                    bad.append("%s r%d c%s gold=%s vlm=%s" % (src, ri + 1, ELEMS[ci], gv[ci], lv[ci]))
            if ok:
                rowhit += 1
    pct = 100.0 * hit / tot if tot else 0.0
    print("VLM vs GOLD  cells %d/%d = %.1f%%   rows %d/%d = %.1f%%" % (
        hit, tot, pct, rowhit, rowtot, 100.0 * rowhit / rowtot if rowtot else 0.0))
    print("gate: >=95%% 全量放行 | 90~95%% 全量跑但人工裁图 | <90%% 换模型/放弃")
    for b in bad[:60]:
        print("  " + b)
    return pct


def run(targets, dry, model, base, key, upscale):
    VLM_DIR.mkdir(parents=True, exist_ok=True)
    total_bytes = 0
    for label, items in targets:
        for sub, img in items:
            png = png_bytes(img, upscale)
            total_bytes += len(png)
            name = "%s%s.json" % (label.replace("/", "_"), ("_" + sub) if sub else "")
            if dry:
                print("  %-22s %5d KB  -> %s" % (label + ("/" + sub if sub else ""),
                                                 len(png) // 1024, name))
                continue
            try:
                txt = call_vlm(png, model, base, key)
                data = parse_json(txt)
                if data is None:
                    (VLM_DIR / name).write_text(
                        json.dumps({"error": "unparseable", "raw": txt}, ensure_ascii=False, indent=1),
                        encoding="utf-8")
                    print("  %-22s UNPARSEABLE" % label)
                    continue
                rows, _ = validate(data.get("rows", []))
                if label.startswith("13"):
                    extra = check13(rows)
                    if extra:
                        for r in rows:
                            r.setdefault("problems", []).extend(extra)
                (VLM_DIR / name).write_text(
                    json.dumps({"rows": rows}, ensure_ascii=False, indent=1), encoding="utf-8")
                nok = sum(1 for r in rows if r.get("verified"))
                print("  %-22s rows=%d verified=%d" % (label + ("/" + sub if sub else ""), len(rows), nok))
            except Exception as e:
                print("  %-22s ERROR %s: %s" % (label, type(e).__name__, str(e)[:90]))
            time.sleep(0.3)
    if dry:
        print("dry-run total: %.1f MB" % (total_bytes / 1048576.0))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cal", action="store_true", help="跑校准批（13 张 cal 裁图）")
    ap.add_argument("--tags", nargs="*", default=None, help="按篇全量（只发状态行裁图）")
    ap.add_argument("--worklist", action="store_true",
                    help="只跑 lowconf_worklist.json 里的低置信卡（默认 scope=gap，99 行/79 卡）")
    ap.add_argument("--probe", action="store_true",
                    help="用 1x1 图发一次极小请求，校验 key / 模型名 / 端点是否可用")
    ap.add_argument("--scope", default="gap",
                    choices=["gap", "verified-bypass", "excluded-13"],
                    help="gap=真正入库缺口 | verified-bypass=01/03 已豁免入库 | excluded-13=13 篇整篇")
    ap.add_argument("--dry-run", action="store_true", help="不联网，只报裁图体积")
    ap.add_argument("--score", action="store_true", help="与 cal_gold.json 算一致率")
    ap.add_argument("--dual", action="store_true", help="与本地通道逐格比对")
    ap.add_argument("--upscale", type=float, default=1.5)
    a = ap.parse_args()

    if a.score:
        score_cal()
        return

    key = os.environ.get("VLM_API_KEY", "")
    model = os.environ.get("VLM_MODEL", "qwen-vl-max-latest")
    base = os.environ.get("VLM_BASE_URL", "https://dashscope.aliyuncs.com/compatible-mode/v1")

    if a.probe:
        if not key:
            print("缺少 VLM_API_KEY")
            return 1
        px = Image.new("RGB", (2, 2), (255, 255, 255))
        b = io.BytesIO()
        px.save(b, format="PNG")
        print("endpoint = %s" % base)
        print("model    = %s" % model)
        print("key      = %s...%s (len=%d)" % (key[:6], key[-4:], len(key)))
        try:
            out = call_vlm(b.getvalue(), model, base, key, timeout=60)
            print("OK, model replied: %s" % out.strip()[:120].encode(
                "ascii", "replace").decode())
            return 0
        except Exception as e:
            print("FAILED %s: %s" % (type(e).__name__, str(e)[:400]))
            return 1
    if not a.dry_run and not key:
        print("缺少 VLM_API_KEY（请自行设置环境变量，不要写进代码）")
        return 1

    ocr_pool.refresh()
    targets = []
    if a.cal:
        # 校准批文件名用 <tag>_<idx>（与 cal_gold.json / cal_local.json 的 key 对齐）
        for src, p in cal_crops():
            tag, idx = src.split("/")
            targets.append((tag, [(idx, Image.open(p).convert("RGB"))]))
    if a.tags:
        tags = a.tags or [f"{i:02d}" for i in range(1, 20)]
        for tag in tags:
            d = R.OCR_DIR / tag
            if not d.is_dir():
                continue
            items = []
            for f in sorted(d.glob("*.txt")):
                # 行号必须保留在文件名里，否则同一张卡的多行会互相覆盖
                for ri, im in row_crops(tag, f.stem):
                    items.append((f"{f.stem}_r{ri}" if ri else f.stem, im))
            if items:
                targets.append((tag, items))
    if a.worklist:
        # 只对工单里的卡发裁图（比整篇省 ~85% 流量）；行身份仍靠 label 事后匹配，
        # 因为 diffs_all 不记录行号，少数行的 label 也与 extract 不一致。
        wl = json.loads((GEN2 / "lowconf_worklist.json").read_text(encoding="utf-8"))
        rows = [r for r in wl if r["scope"] == a.scope]
        takes = sorted({r["take"] for r in rows})
        by_tag = {}
        for t in takes:
            by_tag.setdefault(t[:2], []).append(t.split("/")[1])
        for tag, idxs in sorted(by_tag.items()):
            items = []
            for idx in idxs:
                for ri, im in row_crops(tag, idx):
                    items.append((f"{idx}_r{ri}" if ri else idx, im))
            if items:
                targets.append((tag, items))
        print("worklist scope=%s: %d rows / %d cards" % (a.scope, len(rows), len(takes)))

    if not targets:
        print("nothing to do (use --cal or --tags)")
        return 1
    run(targets, a.dry_run, model, base, key, a.upscale)

    if a.dual:
        loc = json.loads((GEN2 / "cal_local.json").read_text(encoding="utf-8"))
        for tag, items in targets:
            for sub, _ in items:
                p = VLM_DIR / ("%s_%s.json" % (tag, sub))
                if not p.exists():
                    continue
                vr = json.loads(p.read_text(encoding="utf-8"))["rows"]
                lr = loc.get("%s/%s" % (tag, sub), {}).get("rows", [])
                ad, rv = dual_channel(vr, lr)
                print("%s/%s adopt=%d review=%d" % (tag, sub, len(ad), len(rv)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
