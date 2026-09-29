# -*- coding: utf-8 -*-
r"""固化「低置信行 → 卡上行」的行身份映射（**可复现**，不再依赖一次性 snippet）。

## 为什么必须固化

`gap_adopted.json` / `gap_mapped.json` 原本是**一次性 snippet** 跑出来的，生成它的代码
只存在于当时的对话记录里。后果有三条，都是硬的：

1. **不能重算**。换台机器 / 对话丢了，已经入库那 97 行 `evidence` 就无法重新推导
   （只能从 `_gen2/gap_adopted.json` 反推，而 `_gen2/` 是 gitignore 的）。
2. **② 13 篇做不了可信比对**。13 篇本地 label 本身是垃圾 OCR，只能退到行号；
   而 13 篇本地行号**恰恰不可信**（本地漏行会整体错位）。没有可复用的身份映射，
   比对结果无法解释。
3. **③ 的 evidence 分级不可追溯**。只能声称「对当前这批数据有效」。

## 输入（都很小，已快照进 `_provenance/`，随仓库一起版本控制）

    _provenance/lowconf_worklist.json    低置信工单（取 scope == "gap" 的 99 条）
    _provenance/vlm/<tag>_<idx>.json     VLM **整表几何**读数（317 个文件合计 0.09 MB）
    _provenance/vlm/<tag>_<idx>_r<K>.json VLM **横条几何**读数（判定两几何是否分歧）
    _provenance/rescue/*_states.json     本地 OCR 行（只用来取 n_local）
    _provenance/cal_local.json           校准批 13 张卡的本地读数（供 derive_evidence 判 cal-gold）

> 快照之所以可行：**贵的部分（VLM 调用）已经付过了，输出本身极小**（< 0.5 MB）。
> 不重建快照就只能重跑 VLM，那是要花钱且不可复现的。

## 判定规则（与原始 snippet 逐条对应，未作任何改动）

    n_local == n_card 且 row 有值   → mode = index(local==card)     行号即身份
        其中若横条几何与整表几何逐格不一致 → mode = index但两几何分歧（需目视）
    否则（行号错位或 row 为空）      → mode = label(...)              以 label 为准
        用 difflib.SequenceMatcher 在整表各行 label 上找最相似：
          相似度 >= 0.60 → label(label-sim(0.xx))
          否则           → label(fallback)（回退首行，另见 OVERRIDES 说明）

同分裁决用 −|行号距离|（离工单行号越近越优先），与原实现一致。

## ⚠️ OVERRIDES：人工覆盖必须显式登记

`14/014` 是唯一「行号对齐但两几何分歧」的行：整表几何把 `9.9k` 误读成 `10`，横条几何正确
→ 目视确认为 `[9900, 9900, 9900, 9900, 9950, 9900, 9900, 9900]`。
覆盖写在 `OVERRIDES` 里，并**在下标命中时断言确实被用到**——否则条目失效时会静默留在代码里。

## 用法

    python label_map.py --build-snapshot   # 把当前 _gen2 / _rescue 的输入快照进 _provenance/
    python label_map.py                    # 重算并与 _gen2/gap_adopted.json 逐字段比对
    python label_map.py --write            # 落盘 _provenance/label_map.json + _gen2/gap_adopted.json

⚠️ `--write` 重生成 `gap_adopted.json` 时会**保留已有的 `evidence`**（按 take/row/label 对齐），
所以完整链路是幂等的：`label_map.py --write` → `derive_evidence.py --write` → `finalize.py`。
"""
import argparse
import difflib
import json
import re
import shutil
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).parent
GEN = HERE / "_gen2"
RES = HERE / "_rescue"
PROV = HERE / "_provenance"

# 与原实现完全一致的归一化：去掉空白与中英标点
NORM_RE = re.compile(r"[\s\u3000·・、,，.。:：()（）\[\]【】]+")
SIM_MIN = 0.60

# 人工覆盖：{(take, row): {values, mode, note}}
OVERRIDES = {
    ("14/014", 2): {
        "values": [9900, 9900, 9900, 9900, 9950, 9900, 9900, 9900],
        "mode": "eyeball(目视确认 9.9k)",
        "note": "整表几何把 9.9k 误读为 10；横条几何正确，已目视确认",
    },
}

# 统一字段顺序（与原产物一致；note / evidence 可选）
FIELDS = ("take", "row", "label", "meta", "values", "mode", "n_local", "n_card")
OPTIONAL = ("note", "evidence")


def norm(s):
    return NORM_RE.sub("", str(s or ""))


def pick(rows, want, k):
    """整表各行里挑最像 want 的一行；同分用行号距离裁决。

    返回 (row, how)，how ∈ {label-sim(0.xx), fallback}。
    """
    w = norm(want)
    scored = []
    for i, r in enumerate(rows):
        lab = norm(r.get("label"))
        ratio = difflib.SequenceMatcher(None, w, lab).ratio() if (w and lab) else 0.0
        scored.append((ratio, -abs((i + 1) - (k or 1)), i, r))
    best = max(scored, key=lambda x: (x[0], x[1]))
    if best[0] >= SIM_MIN:
        return best[3], "label-sim(%.2f)" % best[0]
    return rows[0], "fallback"


def agree(a, b):
    """逐格一致数；不可比时 None。注意本地值为 str、VLM 值为 int，故统一 str() 比。"""
    if not (a and b and len(a) == 8 and len(b) == 8):
        return None
    return sum(1 for c in range(8)
               if (a[c] == "IMM" and b[c] == "IMM")
               or (a[c] != "IMM" and str(a[c]) == str(b[c])))


def resolve_inputs():
    """优先用 `_provenance/` 快照；否则回落到 live 目录。要求成套，避免半新半旧。"""
    wl_p, vlm_d, res_d = PROV / "lowconf_worklist.json", PROV / "vlm", PROV / "rescue"
    have_prov = [wl_p.exists(), vlm_d.is_dir(), res_d.is_dir()]
    if any(have_prov) and not all(have_prov):
        raise SystemExit("❌ `_provenance/` 不完整（%s）。请先 `--build-snapshot`，或删掉该目录。"
                         % have_prov)
    if all(have_prov):
        return wl_p, vlm_d, res_d, "provenance"
    return GEN / "lowconf_worklist.json", GEN / "vlm", RES, "live"


def load_local_rows(res_d):
    out = {}
    for p in sorted(res_d.glob("*_states.json")):
        for c in json.loads(p.read_text(encoding="utf-8")):
            out[c["src"]] = c.get("rows", [])
    return out


def build(wl_p, vlm_d, res_d):
    """重算行身份映射，返回 (adopted, agreed_map, overrides_hit)。"""
    wl = json.loads(wl_p.read_text(encoding="utf-8"))
    gap = [r for r in wl if r["scope"] == "gap"]
    local = load_local_rows(res_d)

    adopted, agreed_map = [], {}
    hit = Counter()
    for e in gap:
        tag, idx = e["take"].split("/")
        k = e["row"]
        rows = json.loads((vlm_d / f"{tag}_{idx}.json").read_text(encoding="utf-8"))["rows"]
        n_card, n_loc = len(rows), len(local.get(e["take"], []))
        strip_p = vlm_d / f"{tag}_{idx}_r{k}.json" if k else None
        strip = (json.loads(strip_p.read_text(encoding="utf-8"))["rows"][0]["values"]
                 if (strip_p and strip_p.exists()) else None)

        if n_loc == n_card and k and k <= n_card:
            row, mode = rows[k - 1], "index(local==card)"
            if agree(row["values"], strip) not in (None, 8):
                mode = "index但两几何分歧"
        else:
            row, how = pick(rows, e["label"], k)
            mode = "label(" + how + ")"

        item = {"take": e["take"], "row": k, "label": e["label"], "meta": e.get("meta"),
                "values": row["values"], "mode": mode, "n_local": n_loc, "n_card": n_card}

        ov = OVERRIDES.get((e["take"], k))
        if ov:
            hit[(e["take"], k)] += 1
            item["values"] = ov["values"]
            item["mode"] = ov["mode"]
            item["note"] = ov["note"]

        # 身份自证：VLM 行与**本地同序号行**的逐格一致数（供 ② 复用；不进档案）
        fixed = k if isinstance(k, int) else None
        lv = local.get(e["take"], [])
        agreed_map[f'{e["take"]}#{e["row"]}'] = (
            agree(item["values"], lv[fixed - 1]["values"])
            if (fixed and fixed <= len(lv)) else None)

        adopted.append(item)
    return adopted, agreed_map, hit


def compare(computed, existing, base_only=True):
    """逐字段比对两批采纳行；返回不匹配清单。"""
    key = lambda r: (r["take"], r["row"])
    cm, em = {key(r): r for r in computed}, {key(r): r for r in existing}
    diffs = []
    for k in sorted(set(cm) | set(em), key=lambda x: (str(x[0]), str(x[1]) or "")):
        a, b = cm.get(k), em.get(k)
        if a is None or b is None:
            diffs.append((k, "只在一侧存在", a is None, b is None))
            continue
        for f in FIELDS + OPTIONAL:
            if base_only and f in ("note", "evidence"):
                continue          # note 由 OVERRIDES 定义，evidence 由 derive_evidence 追加
            if a.get(f) != b.get(f):
                diffs.append((k, f, a.get(f), b.get(f)))
    return diffs, cm, em


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--build-snapshot", action="store_true",
                    help="把 live 输入快照进 _provenance/（随仓库版本控制）")
    ap.add_argument("--write", action="store_true",
                    help="写 _provenance/label_map.json 与 _gen2/gap_adopted.json")
    args = ap.parse_args()

    sys.stdout.reconfigure(errors="replace")
    a_ = lambda s: str(s).encode("ascii", "replace").decode()

    if args.build_snapshot:
        PROV.mkdir(parents=True, exist_ok=True)
        (PROV / "vlm").mkdir(exist_ok=True)
        (PROV / "rescue").mkdir(exist_ok=True)
        shutil.copy2(GEN / "lowconf_worklist.json", PROV / "lowconf_worklist.json")
        shutil.copy2(GEN / "cal_local.json", PROV / "cal_local.json")
        n_v = n_r = 0
        for p in sorted((GEN / "vlm").glob("*.json")):
            shutil.copy2(p, PROV / "vlm" / p.name)
            n_v += 1
        for p in sorted(RES.glob("*_states.json")):
            shutil.copy2(p, PROV / "rescue" / p.name)
            n_r += 1
        total = sum(f.stat().st_size for f in PROV.rglob("*") if f.is_file())
        print("快照完成：vlm %d 个 / rescue %d 个，合计 %.2f MB -> %s"
              % (n_v, n_r, total / 1e6, a_(str(PROV.relative_to(HERE.parent)))))
        return 0

    wl_p, vlm_d, res_d, origin = resolve_inputs()
    print("输入来源: %s（%s）" % (origin, a_(str(vlm_d.relative_to(HERE.parent)))))
    adopted, agreed_map, hits = build(wl_p, vlm_d, res_d)

    print("\n重算采纳行 %d 条，mode 分布：" % len(adopted))
    for k, n in Counter(r["mode"] for r in adopted).most_common():
        print("  %-30s %d" % (a_(k), n))

    print("\nOVERRIDES 命中：")
    for k, need in ((k, 1) for k in OVERRIDES):
        got = hits.get(k, 0)
        print("  %-10s row=%s  %s" % (a_(k[0]), k[1], "OK x%d" % got if got == need
                                      else "!! 期望 %d 次，实际 %d 次 —— 覆盖条目可能已失效" % (need, got)))

    not8 = [f'{r["take"]}#{r["row"]}' for r in adopted
            if not (r["values"] and len(r["values"]) == 8)]
    print("\n值不完整（非 8 项）的行: %d %s" % (len(not8), a_(str(not8[:10]))))

    ag = Counter(agreed_map.values())
    print("身份自证（VLM 采纳值 vs 本地同序号行，逐格一致数）: %s" % a_(str(dict(sorted(
        (("None" if k is None else str(k)), v) for k, v in ag.items())))))

    # 与现有产物比对
    existing_p = GEN / "gap_adopted.json"
    if existing_p.exists():
        existing = json.loads(existing_p.read_text(encoding="utf-8"))
        diffs, _, _ = compare(adopted, existing)
        print("\n与 _gen2/gap_adopted.json 逐字段比对：%d 条 | 不匹配 %d 处"
              % (len(existing), len(diffs)))
        for d in diffs[:20]:
            print("  %-12s %-10s %s -> %s"
                  % (a_(str(d[0])), a_(str(d[1])), a_(str(d[2])), a_(str(d[3]))))
        if not diffs:
            print("  >>> 身份映射**完全复现**（原产物可由本脚本重算）")
    else:
        print("\n（_gen2/gap_adopted.json 不存在，跳过比对）")

    if args.write:
        GEN.mkdir(parents=True, exist_ok=True)   # 冷启动时 _gen2/ 可能还不存在
        (PROV / "label_map.json").write_text(
            json.dumps({"adopted": adopted, "agreed_with_local": agreed_map},
                       ensure_ascii=False, indent=1), encoding="utf-8")
        # 保留已有 evidence，保持链路幂等
        ev = {}
        if existing_p.exists():
            for r in json.loads(existing_p.read_text(encoding="utf-8")):
                if r.get("evidence"):
                    ev[(r["take"], r["row"], r["label"])] = r["evidence"]
        carried = 0
        for r in adopted:
            e = ev.get((r["take"], r["row"], r["label"]))
            if e:
                r["evidence"] = e
                carried += 1
        existing_p.write_text(json.dumps(adopted, ensure_ascii=False, indent=1), encoding="utf-8")
        print("\n已落盘 _provenance/label_map.json 与 _gen2/gap_adopted.json"
              "（携带回已有 evidence %d 条）" % carried)
    return 0


if __name__ == "__main__":
    sys.exit(main())
