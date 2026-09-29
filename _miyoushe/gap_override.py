# -*- coding: utf-8 -*-
"""把 VLM 补读的 gap 采纳值注入 `diffs_all.json`。

⚠️ **为什么必须是独立一步**：`finalize.py` 会重跑 `table_all.py`，而 table_all 会
**重建** `diffs_all.json`（内容来自本地 OCR 的 `_rescue/*_states.json` + 表格文件）。
所以「先手工改 diffs_all 再跑 finalize」一定会被覆盖掉——注入点必须在
`table_all` 之后、`merge_all` 之前。

数据来源：`_gen2/gap_adopted.json`（99 行，由 `vlm_transcribe.py` 的两套裁图几何
互检 + 行身份核查产生，见 README「VLM 第二通道」）。

规则：
  - 只覆盖 tag ∉ {01, 03, 13} 的**低置信**行（01/03 是已人工验证篇，13 篇整篇排除）
  - 命中则把 `values` 换成 VLM 读值、`confident=True`、记 `source="vlm-gap"`
  - 未命中会打印出来（正常应为 0，非 0 说明工单与 diff 不一致，需人工查）

用法：python gap_override.py
"""
import json
import sys
from pathlib import Path

HERE = Path(__file__).parent
GEN = HERE / "_gen2"
EXCLUDE = {"13"}
VERIFIED = {"01", "03"}


def main():
    a = lambda s: str(s).encode("ascii", "replace").decode()
    diffs_p = GEN / "diffs_all.json"
    diffs = json.loads(diffs_p.read_text(encoding="utf-8"))
    adopted = json.loads((GEN / "gap_adopted.json").read_text(encoding="utf-8"))

    amap = {}
    for r in adopted:
        amap.setdefault((r["take"], r["label"]), r["values"])

    upd, miss, dup = 0, [], 0
    for d in diffs:
        if d.get("confident"):
            continue
        tag = d["src"].split("/")[0]
        if tag in EXCLUDE or tag in VERIFIED:
            continue
        key = (d["src"], d.get("label"))
        if key not in amap:
            miss.append("%s|%s" % (d["src"], a(str(d.get("label")))))
            continue
        vals = amap[key]
        d["values"] = [("IMM" if v == "IMM" else str(v)) for v in vals]
        d["confident"] = True
        d["source"] = "vlm-gap"
        upd += 1

    # 多行共用同一 (src,label) 时会被覆盖多次，计数需与工单条数区分
    diffs_p.write_text(json.dumps(diffs, ensure_ascii=False, indent=1), encoding="utf-8")
    conf = sum(1 for d in diffs if d.get("confident"))
    print("gap 覆盖 %d 行（工单 %d 行，diff 现高置信 %d 行）"
          % (upd, len(adopted), conf))
    if miss:
        print("  ⚠ 未匹配 %d 条：%s" % (len(miss), ", ".join(miss[:10])))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
