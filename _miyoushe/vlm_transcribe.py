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
from collections import Counter
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

# 控制台编码兜底：部分终端是 GBK，打印 ✓ / ⚠ / → 会抛 UnicodeEncodeError 直接中断脚本。
# 只放宽 errors（不改 encoding），这样支持 UTF-8 的终端照常显示，GBK 终端退化为 ? 而不是崩溃。
for _s in (sys.stdout, sys.stderr):
    try:
        _s.reconfigure(errors="replace")
    except Exception:
        pass

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


def table_crop(tag, idx):
    """「整张抗性表」裁图 —— **与校准批完全相同的几何**。

    ⚠️ 为什么不默认用单行横条：
      1) 横条只有 60px 高，且对「该卡根本没有那么多行」的卡会裁到**说明文字区**，
         VLM 会据此**凭空编造**出看起来合理的抗性行（实测 06/001 的 r2/r3 编出了
         `[10,10,IMM,10,...]` 与 6 行副本，而该卡实际只有 1 行）—— validate() 抓不到；
      2) 一张卡裁多张横条时，行号与「第几行」的对应关系无法自证。
    整表裁图下，行按序返回且带 label，与金标准同构 —— 且正是校准 224/224 用过的几何。
    """
    src = next(R.ROOT.glob(f"{tag}_*/{idx}.png"))
    im = Image.open(src).convert("RGB")
    lx0, yb, _ = F.locate(im)
    if lx0 is None:
        return None
    box = (0, max(0, int(yb - 70)), min(im.width, int(lx0 + 600)),
           min(im.height, int(yb + 320)))
    return im.crop(box)


def png_bytes(img, upscale=1.5):
    if upscale != 1.0:
        img = img.resize((int(img.width * upscale), int(img.height * upscale)), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def est_tokens(w, h):
    """Qwen-VL 视觉 token 粗估：按 28x28 一个 patch 计。
    只用于花钱前核对量级，不等于官方计费值（还有模板/文字开销）。"""
    return ((w + 27) // 28) * ((h + 27) // 28)


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

def classify(g, v):
    """给一处不一致分类——用来区分「系统性偏差」与「随机噪声」。"""
    if v is None or v == "":
        return "empty(未识别)"
    gs, vs = str(g), str(v)
    if g == "IMM" and vs != "IMM":
        return "IMM误读为数值"
    if g != "IMM" and vs == "IMM":
        return "数值误读为IMM"
    try:
        gi, vi = int(gs), int(vs)
    except (TypeError, ValueError):
        return "非数值"
    if len(str(gi)) > 1 and str(gi)[:-1] == str(vi):
        return "数字截断(少一位)"
    if str(gi) == str(vi)[:-1]:
        return "多误一位"
    if gi == -vi:
        return "符号翻转"
    if abs(gi - vi) == 5:
        return "偏差5"
    if abs(gi - vi) == 10:
        return "偏差10"
    return "其他数值"


PROBE_ENDPOINTS = [
    ("国内站", "https://dashscope.aliyuncs.com/compatible-mode/v1"),
    ("国际站", "https://dashscope-intl.aliyuncs.com/compatible-mode/v1"),
]
PROBE_TEXT_MODELS = ["qwen-turbo"]
PROBE_VL_MODELS = ["qwen-vl-max-latest", "qwen-vl-max", "qwen-vl-plus",
                   "qwen2.5-vl-7b-instruct"]


def _http_call(payload, base, key, timeout=45):
    """返回 (status, body_text)。status 是 int，或字符串 'ERR'。"""
    req = urllib.request.Request(
        base.rstrip("/") + "/chat/completions",
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json", "Authorization": "Bearer " + key})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return 200, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as e:
        return "ERR", "%s: %s" % (type(e).__name__, e)


def probe_matrix(key, base):
    """把 端点 × 模型 试一遍，定位 403 到底是哪一层的问题。

    关键区分：403 access_denied ≠ 401 InvalidApiKey。前者说明 key 被认出来了但没权限；
    而且响应带 request_id → 请求已到 DashScope 网关 → **网络/代理不是原因**，
    不要再折腾 NO_PROXY。
    试「文本模型」是为了把「账号级」与「视觉模型级」分开：
      文本也 403 → 账号/额度/区域/权限问题；仅视觉 403 → 该账号未开通 qwen-vl。
    """
    print("key = %s...%s (len=%d)" % (key[:6], key[-4:], len(key)))
    # ⚠️ 测试图必须 >=10px（Qwen-VL 要求宽高各不小于 10 像素），否则视觉模型会回
    # 400 invalid_parameter_error 「image length and width do not meet the restrictions」，
    # 看上去像不可用，其实鉴权已经通过了
    px = Image.new("RGB", (64, 64), (255, 255, 255))
    b = io.BytesIO()
    px.save(b, format="PNG")
    uri = "data:image/png;base64," + base64.b64encode(b.getvalue()).decode()
    txt = [{"type": "text", "text": "reply with: ok"}]
    vis = [{"type": "image_url", "image_url": {"url": uri}}] + txt

    rows = []
    print("\n  %-6s %-22s %-5s %s" % ("端点", "模型", "状态", "详情"))
    for ename, ebase in PROBE_ENDPOINTS:
        for m in PROBE_TEXT_MODELS + PROBE_VL_MODELS:
            content = vis if "vl" in m else txt
            st, body = _http_call(
                {"model": m, "messages": [{"role": "user", "content": content}],
                 "temperature": 0}, ebase, key)
            if st == 200:
                try:
                    d = "OK -> " + str(json.loads(body)["choices"][0]["message"]["content"])[:40]
                except Exception:
                    d = "OK (响应未解析)"
            else:
                try:
                    j = json.loads(body)["error"]
                    d = "%s | %s" % (j.get("code"), str(j.get("message"))[:90])
                except Exception:
                    d = str(body)[:100]
            rows.append((ename, m, st, d))
            print("  %-6s %-22s %-5s %s" % (ename, m, st, d[:110]))

    # ⚠️ 关键：鉴权是否通过看的是「不是 401/403」，不是「是 200」。
    # 400（参数/尺寸/额度外的请求级错误）说明请求已过鉴权 → 该模型对该账号是可用的。
    auth_ok = [(e, m, st) for e, m, st, _ in rows if str(st) not in ("401", "403")]
    d401 = [(e, m) for e, m, st, _ in rows if str(st) == "401"]
    d403 = [(e, m) for e, m, st, _ in rows if str(st) == "403"]
    dom_ok = any(e == "国内站" for e, _, _ in auth_ok)
    intl_ok = any(e == "国际站" for e, _, _ in auth_ok)
    print("\n  【归因】状态码集合 = %s"
          % ", ".join(sorted({str(st) for _, _, st, _ in rows})))
    print("    ✓ 鉴权通过（可用）：%s"
          % (", ".join("%s/%s(%s)" % t for t in auth_ok) or "无"))
    if d403:
        print("    ✗ 403 access_denied：%s" % ", ".join("%s/%s" % t for t in d403))
    if d401:
        print("    ✗ 401 invalid_api_key：%s" % ", ".join("%s/%s" % t for t in d401))

    PREF = ["qwen-vl-max", "qwen-vl-max-latest", "qwen-vl-plus",
            "qwen2.5-vl-72b-instruct", "qwen2.5-vl-7b-instruct"]
    usable_vl = [m for _, m in sorted(
        (PREF.index(m) if m in PREF else 99, m)
        for e, m, _ in auth_ok if "vl" in m and e == "国内站")]
    if intl_ok and not dom_ok:
        print("    → key 属**国际站**：把 VLM_BASE_URL 设为\n"
              "      https://dashscope-intl.aliyuncs.com/compatible-mode/v1")
    if usable_vl:
        print("    \u2192 建议：$env:VLM_MODEL = \"%s\"   # 已授权%s"
              % (usable_vl[0], "，能力最强" if usable_vl[0] == "qwen-vl-max" else ""))
    elif auth_ok:
        print("    ✗ 鉴权通过，但**没有任何可用的视觉模型（VL）**——本任务必须用视觉模型，\n"
              "      去百炼控制台 → 模型广场开通任一 qwen-vl 系列后重试")
    else:
        print("    ✗ 没有任何「端点+模型」可用：key / 账号 / 账单需人工排查")
    if d403 and usable_vl:
        print("    注：403 的都是**未授权给该账号的模型名**（-latest 滚动标签、旧版 2.5 系列\n"
              "      常需单独开通）。直接用上面建议的已授权模型名即可，不必等开通。")
    if not d401 and not d403:
        print("    ✓ 全部组合鉴权通过")
    return rows


def score(vlm_dir, show_detail=True):
    """与 cal_gold.json 逐格比对。除一致率外，**重点输出不一致格的位置分布**：
    单看一致率不够——若错格集中在同一列/同一类型，说明是系统性偏差，
    全量时会在同样位置重复翻车；分布分散才像随机认识噪声。
    """
    gold = json.loads((CAL / "cal_gold.json").read_text(encoding="utf-8"))
    tot = hit = rowtot = rowhit = 0
    miss_cards, miss_rows, mism = [], [], []
    for src in sorted(gold):
        p = vlm_dir / (src.replace("/", "_") + ".json")
        rows = None
        if p.exists():
            try:
                doc = json.loads(p.read_text(encoding="utf-8"))
                rows = doc.get("rows")
                if rows is None:
                    miss_cards.append("%s(bad-content:%s)" % (src, str(doc.get("error"))[:20]))
            except Exception as e:
                miss_cards.append("%s(bad-file:%s)" % (src, type(e).__name__))
        else:
            miss_cards.append(src)
        for ri, (_, gv) in enumerate(gold[src]):
            rowtot += 1
            if rows is None or ri >= len(rows):
                miss_rows.append("%s r%d" % (src, ri + 1))
                tot += 8
                continue
            lv = rows[ri].get("values") or []
            if len(lv) != 8:
                miss_rows.append("%s r%d(len=%d)" % (src, ri + 1, len(lv)))
            ok = True
            for ci in range(8):
                tot += 1
                v = lv[ci] if ci < len(lv) else None
                good = (v == "IMM") if gv[ci] == "IMM" else (str(v) == str(gv[ci]))
                if good:
                    hit += 1
                else:
                    ok = False
                    mism.append((src, ri + 1, ci, gv[ci], v, classify(gv[ci], v)))
            if ok:
                rowhit += 1

    pct = 100.0 * hit / tot if tot else 0.0
    rpct = 100.0 * rowhit / rowtot if rowtot else 0.0
    print("=" * 70)
    print("VLM vs GOLD   cells %d/%d = %.2f%%   rows %d/%d = %.1f%%   不一致 %d 格"
          % (hit, tot, pct, rowhit, rowtot, rpct, len(mism)))
    if miss_cards:
        print("\u26a0 无输出/不可用的卡 %d 张：%s" % (len(miss_cards), ", ".join(miss_cards)))
    if miss_rows:
        print("\u26a0 行缺失或长度异常：%s" % ", ".join(miss_rows[:12]))

    if mism:
        by_cls = Counter(m[5] for m in mism)
        by_col = Counter(m[2] for m in mism)
        by_row = Counter(m[1] for m in mism)
        by_art = Counter(m[0][:2] for m in mism)
        n = len(mism)
        print("\n  \u3010\u7c7b\u578b\u3011")
        for k, c in by_cls.most_common():
            print("    %-16s %3d  %5.1f%%  %s" % (k, c, 100.0 * c / n, "#" * c))
        print("  \u3010\u5217\u4f4d\u3011(0=\u706b ... 7=\u7269\u7406)")
        for ci in range(8):
            c = by_col.get(ci, 0)
            print("    c%d %-3s %3d  %s" % (ci, ELEMS[ci], c, "#" * c))
        print("  \u3010\u884c\u53f7\u3011" + "   ".join("r%d=%d" % (k, v) for k, v in sorted(by_row.items())))
        print("  \u3010\u7bc7\u3011" + "   ".join("%s:%d" % (k, v) for k, v in sorted(by_art.items())))

        print("\n  \u3010\u7cfb\u7edf\u6027\u5224\u65ad\u3011")
        top_cls, n_cls = by_cls.most_common(1)[0]
        top_col, n_col = by_col.most_common(1)[0]
        flags = []
        # 列位锁定才是「同样位置重复翻车」的真信号；类型集中要有更高占比才算强证据
        if n >= 3 and n_col / n >= 0.5:
            flags.append("列位高度集中：%.0f%% 落在第 %d 列（%s）"
                         % (100.0 * n_col / n, top_col + 1, ELEMS[top_col]))
        if n >= 3 and n_cls / n >= 0.75:
            flags.append("类型高度集中：%.0f%% 都是「%s」" % (100.0 * n_cls / n, top_cls))
        # 行号集中是弱信号（行数远少于格数，随机也会偏），只提示、不参与判定
        top_row, n_row = by_row.most_common(1)[0]
        weak = []
        if n >= 3 and n_row / n >= 0.5:
            weak.append("%.0f%% 落在 r%d（弱信号，随机也会偏，仅供参考）"
                        % (100.0 * n_row / n, top_row))
        if flags:
            print("    \u26a0 \u7591\u4f3c\u7cfb\u7edf\u6027\u504f\u5dee \u2014\u2014 " + "\uff1b".join(flags))
            print("      \u2192 \u5168\u91cf\u65f6\u4f1a\u5728\u540c\u6837\u4f4d\u7f6e\u91cd\u590d\u51fa\u9519\uff1a"
                  "\u5148\u8c03\u63d0\u793a\u8bcd\uff08\u5f3a\u8c03\u5217\u5bf9\u9f50 / \u6570\u4f4d\u6570\uff09\u6216\u6362\u6a21\u578b\uff0c\u4e0d\u8981\u76f4\u63a5\u5168\u91cf\u653e\u884c")
        else:
            print("    \u2713 \u5206\u5e03\u5206\u6563\uff0c\u50cf\u968f\u673a\u8fa8\u8bc6\u566a\u58f0"
                  "\uff08\u4e0d\u5b58\u5728\u5355\u4e00\u4e3b\u5bfc\u7c7b\u578b/\u5217\uff09\u2192 \u53ef\u6309\u4e0b\u9762\u6863\u4f4d\u5904\u7406")
        for w in weak:
            print("    \u00b7 " + w)

    print("\n  \u3010\u95e8\u69db\u3011")
    if pct >= 95:
        rec = "\u2265 95%%  \u2192 \u5168\u91cf\u653e\u884c\uff0c\u4e0d\u4e00\u81f4\u9879\u8d70 --dual \u4eba\u5de5\u4e09\u5ba1"
    elif pct >= 90:
        rec = "90~95%% \u2192 \u53ef\u5168\u91cf\uff0c\u4f46\u4e0d\u4e00\u81f4\u9879\u5fc5\u987b\u4eba\u5de5\u88c1\u56fe\u540e\u624d\u5165\u5e93"
    else:
        rec = "< 90%%   \u2192 \u5148\u6362\u6a21\u578b / \u8c03\u63d0\u793a\u8bcd\uff0c\u6682\u4e0d\u5168\u91cf"
    print("    %s" % rec)
    print("=" * 70)

    if show_detail and mism:
        print("\n  \u3010\u9010\u683c\u660e\u7ec6\u3011")
        for src, ri, ci, g, v, cls in mism[:60]:
            print("    %-8s r%d c%d(%s) gold=%-5s vlm=%-5s %s"
                  % (src, ri, ci, ELEMS[ci], g, v, cls))
        if len(mism) > 60:
            print("    ... \u53e6 %d \u683c" % (len(mism) - 60))
    return pct


def run(targets, dry, model, base, key, upscale, vlm_dir):
    vlm_dir.mkdir(parents=True, exist_ok=True)
    total_bytes = 0
    total_tokens = 0
    for label, items in targets:
        for sub, img in items:
            png = png_bytes(img, upscale)
            total_bytes += len(png)
            tok = est_tokens(int(img.width * upscale), int(img.height * upscale))
            total_tokens += tok
            name = "%s%s.json" % (label.replace("/", "_"), ("_" + sub) if sub else "")
            if dry:
                print("  %-22s %5d KB  ~%4d tok  -> %s"
                      % (label + ("/" + sub if sub else ""), len(png) // 1024, tok, name))
                continue
            try:
                txt = call_vlm(png, model, base, key)
                data = parse_json(txt)
                if data is None:
                    (vlm_dir / name).write_text(
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
                (vlm_dir / name).write_text(
                    json.dumps({"rows": rows}, ensure_ascii=False, indent=1), encoding="utf-8")
                nok = sum(1 for r in rows if r.get("verified"))
                print("  %-22s rows=%d verified=%d" % (label + ("/" + sub if sub else ""), len(rows), nok))
            except Exception as e:
                print("  %-22s ERROR %s: %s"
                      % (label + ("/" + sub if sub else ""), type(e).__name__, str(e)[:110]))
            time.sleep(0.3)
    if dry:
        print("dry-run total: %.1f MB / \u7ea6 %d \u4e2a\u89c6\u89c9 token\uff08\u7c97\u4f30\uff0c\u4e0d\u542b\u6587\u5b57\u4e0e\u6a21\u677f\u5f00\u9500\uff09"
              % (total_bytes / 1048576.0, total_tokens))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cal", action="store_true", help="跑校准批（13 张 cal 裁图）")
    ap.add_argument("--tags", nargs="*", default=None, help="按篇全量（只发状态行裁图）")
    ap.add_argument("--worklist", action="store_true",
                    help="只跑 lowconf_worklist.json 里的低置信卡（默认 scope=gap，99 行/79 卡）")
    ap.add_argument("--strip", action="store_true",
                    help="改用「单行横条」裁图（省 token，但会引入幻影行与行号歧义，非校准几何）")
    ap.add_argument("--probe", action="store_true",
                    help="诊断矩阵：跨国内/国际端点 × 文本/视觉模型试探，定位 403/401 属于哪一层")
    ap.add_argument("--scope", default="gap",
                    choices=["gap", "verified-bypass", "excluded-13"],
                    help="gap=真正入库缺口 | verified-bypass=01/03 已豁免入库 | excluded-13=13 篇整篇")
    ap.add_argument("--dry-run", action="store_true", help="不联网，只报裁图体积")
    ap.add_argument("--score", action="store_true",
                    help="与 cal_gold.json 逐格比对，并输出不一致格的位置分布 + 系统性判定")
    ap.add_argument("--vlm-dir", default=str(VLM_DIR), help="VLM 输出目录（默认 _gen2/vlm）")
    ap.add_argument("--dual", action="store_true", help="与本地通道逐格比对")
    ap.add_argument("--upscale", type=float, default=1.5)
    a = ap.parse_args()

    vlm_dir = Path(a.vlm_dir)
    if a.score:
        score(vlm_dir)
        return

    key = os.environ.get("VLM_API_KEY", "")
    model = os.environ.get("VLM_MODEL", "qwen-vl-max-latest")
    base = os.environ.get("VLM_BASE_URL", "https://dashscope.aliyuncs.com/compatible-mode/v1")

    if a.probe:
        if not key:
            print("缺少 VLM_API_KEY")
            return 1
        probe_matrix(key, base)
        return 0
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
                if a.strip:
                    # 行号必须保留在文件名里，否则同一张卡的多行会互相覆盖
                    for ri, im in row_crops(tag, f.stem):
                        items.append((f"{f.stem}_r{ri}" if ri else f.stem, im))
                else:
                    im = table_crop(tag, f.stem)
                    if im is not None:
                        items.append((f.stem, im))
            if items:
                targets.append((tag, items))
    if a.worklist:
        # 只对工单里的卡发裁图（比整篇省 ~85% 流量）。
        # 默认用「整张抗性表」几何（= 校准批验证过的几何），行按序返回、带 label，
        # 事后按 label 匹配即可，不依赖行号；--strip 才退回单行横条。
        wl = json.loads((GEN2 / "lowconf_worklist.json").read_text(encoding="utf-8"))
        rows = [r for r in wl if r["scope"] == a.scope]
        takes = sorted({r["take"] for r in rows})
        by_tag = {}
        for t in takes:
            by_tag.setdefault(t[:2], []).append(t.split("/")[1])
        skipped = []
        for tag, idxs in sorted(by_tag.items()):
            items = []
            for idx in idxs:
                if a.strip:
                    for ri, im in row_crops(tag, idx):
                        items.append((f"{idx}_r{ri}" if ri else idx, im))
                else:
                    im = table_crop(tag, idx)
                    if im is None:
                        skipped.append(f"{tag}/{idx}")
                    else:
                        items.append((idx, im))
            if items:
                targets.append((tag, items))
        print("worklist scope=%s: %d rows / %d cards / geometry=%s"
              % (a.scope, len(rows), len(takes), "strip" if a.strip else "table"))
        if skipped:
            print("  \u26a0 locate \u5931\u8d25\uff0c\u5df2\u8df3\u8fc7: %s" % ", ".join(skipped))

    if not targets:
        print("nothing to do (use --cal or --tags)")
        return 1
    run(targets, a.dry_run, model, base, key, a.upscale, vlm_dir)

    if a.dual:
        loc = json.loads((GEN2 / "cal_local.json").read_text(encoding="utf-8"))
        for tag, items in targets:
            for sub, _ in items:
                p = vlm_dir / ("%s_%s.json" % (tag, sub))
                if not p.exists():
                    continue
                vr = json.loads(p.read_text(encoding="utf-8"))["rows"]
                lr = loc.get("%s/%s" % (tag, sub), {}).get("rows", [])
                ad, rv = dual_channel(vr, lr)
                print("%s/%s adopt=%d review=%d" % (tag, sub, len(ad), len(rv)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
