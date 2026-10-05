# -*- coding: utf-8 -*-
"""权重归档：把 stat_priority / scaling_source / l0_scaling 归成一个可审计的档位。

D11：stat_priority（含护盾/治疗语境）与 scaling_source（已排除）是两个独立字段。
D13：评分不按 base_stat 归档。base_stat 是伤害公式的槽位，服务引擎不服务评分。

归档优先级：
  1) 权重表已覆盖（stat_priority 存在）-> 直接用权重表的值
  2) 否则 scaling_source ∪ l0_scaling[shield] ∪ l0_scaling[heal]
     其中 l0_scaling 优先于 scaling_source：护盾/治疗吃哪个属性，才决定哪个 % 词条值钱。
     证据：塔利雅 scaling_source=ATK 但 l0_scaling=shield:[HP]，权重表给 HP。
  3) multi 多属性时取「非 ATK 的那个」：ATK 是默认档，非默认的才是特征。
     scaling_source 与 l0_scaling 一致时才落 ATK。

用法: python tools/ScalingStatGen/weight_fallback.py [--emit]
"""
import json
import os
import sys
from collections import Counter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DERIVED = os.path.join(ROOT, "src", "Ellen.Launcher", "Ellen.Launcher", "Resource", "Derived", "AvatarScalingStat.json")
OVERRIDE = os.path.join(ROOT, "src", "Ellen.Launcher", "Ellen.Launcher", "Resource", "Derived", "AvatarScalingStat.Override.json")

STATS = ("ATK", "HP", "DEF")


def load_overrides():
    if not os.path.exists(OVERRIDE):
        return {}
    data = json.load(open(OVERRIDE, encoding="utf-8"))
    return {row["character"]: row["archive_stat"] for row in data.get("rows", [])}


def pick_non_atk(values):
    """多属性时取非 ATK 者；全为 ATK 时才取 ATK。"""
    non_atk = sorted(v for v in values if v != "ATK")
    if non_atk:
        return non_atk[0]
    return "ATK"


def archive_for(record):
    priority = record.get("stat_priority")
    if priority:
        return priority, "stat_priority"

    l0 = record.get("l0_scaling") or {}
    from_l0 = set(l0.get("shield") or []) | set(l0.get("heal") or [])
    direct = record.get("scaling_source")
    multi = record.get("multi")

    if from_l0:
        # 护盾/治疗语境决定词条价值，优先于伤害缩放。
        pool = set(from_l0)
        if multi:
            pool |= set(multi)
        return pick_non_atk(pool), "l0_scaling"

    if multi:
        return pick_non_atk(set(multi)), "multi"

    return direct or "ATK", "scaling_source"


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    derived = json.load(open(DERIVED, encoding="utf-8"))
    characters = derived["characters"]
    overrides = load_overrides()

    rows = {}
    source_counter = Counter()
    for name, record in sorted(characters.items()):
        stat, source = archive_for(record)
        if name in overrides:
            stat, source = overrides[name], "override"
        rows[name] = {
            "archive_stat": stat,
            "evidence": source,
            "stat_priority": record.get("stat_priority"),
            "scaling_source": record.get("scaling_source"),
            "l0_scaling": record.get("l0_scaling"),
            "multi": record.get("multi"),
        }
        source_counter[source] += 1

    distribution = Counter(r["archive_stat"] for r in rows.values())
    invalid = [n for n, r in rows.items() if r["archive_stat"] not in STATS]

    print("归档分布：%s" % " / ".join("%s %d" % (k, distribution[k]) for k in STATS))
    print("证据来源：%s" % " / ".join("%s %d" % (k, v) for k, v in sorted(source_counter.items())))
    print("断言 archive_stat 恒 ∈ {ATK, HP, DEF} -> %s" % (not invalid))
    if invalid:
        print("  越界角色：%s" % "、".join(invalid))

    print()
    print("--- 争议角色（单用 scaling_source 会误配的）---")
    for name in ("塔利雅", "迪奥娜", "辛焱", "诺艾尔", "莱依拉", "绮良良", "托马"):
        if name not in rows:
            continue
        r = rows[name]
        naive = r["scaling_source"]
        wrong = "❌ 误配" if naive != r["archive_stat"] else "✅"
        print("  %-6s scaling_source=%-4s l0=%-28s 归档=%-4s %s" % (
            name, naive, json.dumps(r["l0_scaling"], ensure_ascii=False), r["archive_stat"], wrong))

    if "--emit" in sys.argv:
        out = {
            "sourceType": "derived-from-AvatarScalingStat",
            "generator": "tools/ScalingStatGen/weight_fallback.py",
            "reviewStatus": "draft",
            "fields": ["archive_stat", "evidence"],
            "archivePriority": [
                "stat_priority（权重表已覆盖）",
                "l0_scaling[shield|heal] ∪ scaling_source（护盾/治疗优先）",
                "multi 取非 ATK 者",
                "scaling_source",
                "Override.json（人工复核，最高优先）",
            ],
            "characters": rows,
        }
        json.dump(out, open(DERIVED.replace("AvatarScalingStat.json", "AvatarScalingStat.Archive.json"), "w", encoding="utf-8"),
                  ensure_ascii=False, indent=1)
        print()
        print("已写出 AvatarScalingStat.Archive.json (%d 角色)" % len(rows))

    return 0 if not invalid else 1


sys.exit(main())
