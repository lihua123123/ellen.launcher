# -*- coding: utf-8 -*-
"""为 VLM 补读的 gap 行**推导** `evidence` 等级（可复现，不手工标注）。

等级（证据强度降序）：

  cal-gold     该行所属的卡在**校准批次**内（`_gen2/cal_local.json` 的 13 张，
               其中 10 张来自 gap 工作单）。校准裁图切的是整张表的区域，
               所以这 10 张卡的全部行都经过「人眼读图」复核。
  eyeball      两套裁图几何对同一行给出不同结果，由目视裁决（当前仅 14/014）。
  2geo         同一行被两套裁图几何分别读出，且 8 格逐格一致。
  single-read  只有一套几何读到该行（**当前为 0 行**）。

推导规则（严格按上述定义，不掺主观判断）：

  1. take ∈ 校准批次  →  cal-gold
  2. mode 以 eyeball 开头  →  eyeball
  3. mode 以 index 开头（本地行数 == 卡上行数，行号可信）
     → 取该卡 `_r{k}.json` 横条与整表第 k 行逐格比较：8/8 → 2geo，否则 single-read
  4. 其余（行号错位，靠 label 相似度选行）
     → 用**该卡全部横条**去撞采纳值：恰有 1 条 8/8 命中 → 2geo（同时反证了行身份）；
       多条命中 → 2geo（数值被多重佐证，但**身份不可区分**，会单独列出）；
       无命中 → single-read

⚠️ 边界（写给未来的自己，别淡化它）：

  - `2geo` 是**同一模型、同一张原图、不同裁图方式**的两次读出。两次解码的
    token 流不同，所以一致有信息量；但它们**共享同一个视觉编码器与同一次源图**，
    失败模式相关。它是「佐证更强」，**不是独立复现，更不是地面真值**。
  - `cal-gold` 之所以更强，是因为换了读者（人眼而不是同一个模型）。
    但那个「人眼」也是 AI 助手的目视判断——所以它仍然**不等于**卡片真值。
  - 本脚本只处理 gap 采纳行。档案里那 136 条基线行**没有** evidence
    （来源是更早的、未审计的流程），**不要**用本脚本的等级去覆盖它们。

用法：
    python derive_evidence.py            # 只报告，不落盘
    python derive_evidence.py --write    # 把 evidence 写进 _gen2/gap_adopted.json
"""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).parent
GEN = HERE / "_gen2"
VLM = GEN / "vlm"

LEVELS = ("cal-gold", "2geo", "eyeball", "single-read")


def agree(a, b):
    """两行 8 格逐格一致的格数；长度不对时返回 None（不可比）。"""
    if not (a and b and len(a) == 8 and len(b) == 8):
        return None
    return sum(1 for c in range(8)
               if (a[c] == "IMM" and b[c] == "IMM")
               or (a[c] != "IMM" and str(a[c]) == str(b[c])))


def strip_values(tag, idx, k):
    p = VLM / f"{tag}_{idx}_r{k}.json"
    if not p.exists():
        return None
    rows = json.loads(p.read_text(encoding="utf-8"))["rows"]
    return rows[0]["values"] if rows else None


def strip_hits(take, values):
    """该卡全部横条里，与 values 8/8 一致的横条行号。"""
    tag, idx = take.split("/")
    hits = []
    for p in sorted(VLM.glob(f"{tag}_{idx}_r*.json")):
        vals = json.loads(p.read_text(encoding="utf-8"))["rows"][0]["values"]
        if agree(values, vals) == 8:
            hits.append(int(p.stem.rsplit("_r", 1)[1]))
    return hits


def derive(adopted, cal_takes):
    out, notes = [], []
    for r in adopted:
        take, mode = r["take"], r["mode"]

        if take in cal_takes:
            lvl = "cal-gold"
        elif mode.startswith("eyeball"):
            lvl = "eyeball"
        elif mode.startswith("index"):
            k = r["row"]
            tag, idx = take.split("/")
            tbl = json.loads((VLM / f"{tag}_{idx}.json").read_text(encoding="utf-8"))["rows"]
            tab = tbl[k - 1]["values"] if k and k <= len(tbl) else None
            lvl = "2geo" if agree(tab, strip_values(tag, idx, k)) == 8 else "single-read"
        else:
            hits = strip_hits(take, r["values"])
            lvl = "2geo" if hits else "single-read"
            if len(hits) > 1:
                notes.append("身份不可区分：%s label=%s 与 %d 条横条同值（%s）"
                             % (take, r["label"], len(hits), hits))

        item = dict(r)
        item["evidence"] = lvl
        out.append(item)

    # 数值完整性：evidence 是附加信息，不该改变任何值
    for a, b in zip(adopted, out):
        if a["values"] != b["values"]:
            raise SystemExit("❌ evidence 推导改动了 values，绝不允许：%s" % a["take"])
    return out, notes


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true", help="写回 _gen2/gap_adopted.json")
    args = ap.parse_args()

    sys.stdout.reconfigure(errors="replace")
    a_ = lambda s: str(s).encode("ascii", "replace").decode()

    adopted = json.loads((GEN / "gap_adopted.json").read_text(encoding="utf-8"))
    cal_takes = set(json.loads((GEN / "cal_local.json").read_text(encoding="utf-8")).keys())
    out, notes = derive(adopted, cal_takes)

    tally = Counter(x["evidence"] for x in out)
    print("gap 采纳行 %d 行的 evidence 划分：" % len(out))
    for lv in LEVELS:
        print("  %-12s %d" % (lv, tally.get(lv, 0)))
    extra = {k: v for k, v in tally.items() if k not in LEVELS}
    if extra:
        print("  ⚠ 出现规格外的等级：%s" % a_(str(extra)))
    print()
    print("校准批次卡（%d 张）：%s" % (len(cal_takes), a_(str(sorted(cal_takes)))))
    print("其中落在 gap 工作单的：" , a_(str(sorted(cal_takes & {r["take"] for r in adopted}))))
    print()
    by_take = Counter(x["take"] for x in out if x["evidence"] == "cal-gold")
    print("cal-gold 行来自 %d 张卡：%s" % (len(by_take), a_(str(sorted(by_take)))))
    for n in notes:
        print("  ⚠ " + a_(n))
    if not notes:
        print("  （无身份不可区分的行）")

    if args.write:
        p = GEN / "gap_adopted.json"
        p.write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
        print("\n已写回 %s（新增字段 evidence）" % p.name)
    else:
        print("\n（试运行，未落盘；加 --write 生效）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
