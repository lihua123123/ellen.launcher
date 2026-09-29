# -*- coding: utf-8 -*-
"""清理状态行的 `condition` 字段：把注释区 OCR 原始串挪到 `condition_ocr_raw`。

背景（26/9/30 全量入库后）
    新增的 97 个 state 里 **74 个**的 `condition` 是本地 OCR 直接抓的注释区文本
    （形如 `10-40 70 20 10-40 ...`、`30 30 30 90 ... -140-140...`），基线为 0/6。
    `values` 全部正确，坏的只是这一列。

为什么可以安全清理（两条都已在代码里查实）
    1. `EnemyArchiveResistanceState.Condition` 在 C#/XAML 里**从未被渲染** ——
       全仓搜 `.Condition` 只命中无关的 `StandardView.ShowCondition` 等，
       `WikiMonsterPage.xaml` 的状态下拉只绑 `ResistanceStates`（状态**名**）。
       → 目前是死字段，清理不影响任何现有界面。
    2. `EnemyArchiveService` 用 `JsonSerializer.DeserializeAsync<T>(stream)` **不传 options**，
       默认 `UnmappedMemberHandling = Skip` → 新增 `condition_ocr_raw` 不会导致
       反序列化失败。（服务层是**静默失败**的，这点必须先确认，否则整页不显示。）

判据
    命中「`\\d-\\d` 粘连」**或**「数字 ≥ 6 个」→ 判为 OCR 原始串。
    干净的短条件（如 `07/030` 的 `深渊+80`，1 个数字、无粘连）天然安全。

置空用 `null` 而不是 `""`
    字段是 `string?`；项目 `JsonOptions.DefaultIgnoreCondition = WhenWritingNull`，
    `null` 是本仓库写 JSON 的既有约定；语义上「无条件」≠「空条件」。

用法
    python clean_conditions.py            # 干跑，只报数字与样例
    python clean_conditions.py --write     # 实际写入
"""
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).parent
ROOT = HERE.parent
ARCH = ROOT / "src/Ellen.Launcher/Ellen.Launcher/Resource/EnemyArchives/EnemyArchives.json"

RAW_FIELD = "condition_ocr_raw"


def is_ocr_garbage(c) -> bool:
    if not c:
        return False
    s = str(c)
    if re.search(r"\d+\s*-\s*\d+", s):          # 10-40 这类粘连值
        return True
    return len(re.findall(r"\d", s)) >= 6       # 数字过多 → 是表格/注释串


def main():
    a = lambda s: str(s).encode("ascii", "replace").decode()
    write = "--write" in sys.argv
    arch = json.loads(ARCH.read_text(encoding="utf-8"))

    hit = kept = already = 0
    samples, kepts = [], []
    for e in arch:
        for st in ((e.get("resistances") or {}).get("states") or []):
            c = st.get("condition")
            if RAW_FIELD in st and not c:
                already += 1
                continue
            if is_ocr_garbage(c):
                st[RAW_FIELD] = c
                st["condition"] = None
                hit += 1
                if len(samples) < 4:
                    samples.append((e.get("name"), str(c)[:70]))
            else:
                kept += 1
                if c and len(kepts) < 4:
                    kepts.append((e.get("name"), str(c)[:70]))

    print("命中清理 %d 个 state；保留原样 %d 个；已是清理态 %d 个" % (hit, kept, already))
    print("\n被清理（原值已存入 %s）:" % RAW_FIELD)
    for nm, c in samples:
        print("  %-20s %s" % (a(str(nm)), a(c)))
    print("\n保留的 condition（应是人话）:")
    for nm, c in kepts:
        print("  %-20s %s" % (a(str(nm)), a(c)))

    if not write:
        print("\n[干跑] 未写入。加 --write 生效。")
        return
    ARCH.write_text(json.dumps(arch, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("\n已写入 %s" % ARCH.name)


if __name__ == "__main__":
    main()
