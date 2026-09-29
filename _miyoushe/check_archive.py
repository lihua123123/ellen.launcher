# -*- coding: utf-8 -*-
"""校验 EnemyArchives.json 合法性，并与元数据 Monster.json 做匹配 / 抗性对照。"""
import json
import os

base = r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher\Resource"
arch_path = os.path.join(base, "EnemyArchives", "EnemyArchives.json")
meta_path = os.path.join(base, "Metadata", "Genshin", "CHS", "Monster.json")

with open(arch_path, encoding="utf-8") as f:
    archives = json.load(f)
print(f"档案条目数: {len(archives)}  (JSON 合法)")

with open(meta_path, encoding="utf-8") as f:
    monsters = json.load(f)

by_name = {}
by_monster_name = {}
for m in monsters:
    if m.get("Name"):
        by_name.setdefault(m["Name"], m)
    if m.get("MonsterName"):
        by_monster_name.setdefault(m["MonsterName"], m)

KEYS = ["pyro", "hydro", "dendro", "electro", "anemo", "cryo", "geo", "physical"]
META_KEY = {
    "pyro": "FireSubHurt", "hydro": "WaterSubHurt", "dendro": "GrassSubHurt",
    "electro": "ElecSubHurt", "anemo": "WindSubHurt", "cryo": "IceSubHurt",
    "geo": "RockSubHurt", "physical": "PhysicalSubHurt",
}
CN = {"pyro": "火", "hydro": "水", "dendro": "草", "electro": "雷",
      "anemo": "风", "cryo": "冰", "geo": "岩", "physical": "物"}

print()
for a in archives:
    mn = a.get("monsterName")
    name = a.get("name")
    mon = by_monster_name.get(mn) or by_name.get(name)
    status = "匹配到元数据" if mon else "**元数据中不存在（不会显示）**"
    print(f"[{name}] monsterName={mn} -> {status}")
    if not mon:
        continue
    bv = mon.get("BaseValue") or {}
    ares = (a.get("resistances") or {}).get("values") or {}
    diffs = []
    cells = []
    for k in KEYS:
        mv = bv.get(META_KEY[k])
        av = ares.get(k, "-")
        meta_txt = f"{round(mv * 100)}%" if isinstance(mv, (int, float)) else "-"
        cells.append(f"{CN[k]}:{meta_txt}/{av}")
        if meta_txt != av:
            diffs.append(CN[k])
    print(f"    元数据/卡片  {(' ').join(cells)}")
    print(f"    不一致项: {('、'.join(diffs)) if diffs else '无'}")
