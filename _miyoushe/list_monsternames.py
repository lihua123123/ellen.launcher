# -*- coding: utf-8 -*-
"""列出合辑一 22 只怪在元数据里的 Name -> MonsterName。"""
import json
import os

base = r"D:\艾莲的数据库\workspace\ellen-launcher\src\Ellen.Launcher\Ellen.Launcher\Resource"
with open(os.path.join(base, "Metadata", "Genshin", "CHS", "Monster.json"), encoding="utf-8") as f:
    monsters = json.load(f)

want = ["火史莱姆", "水史莱姆", "风史莱姆", "雷史莱姆", "草史莱姆", "冰史莱姆", "岩史莱姆",
        "大型火史莱姆", "大型水史莱姆", "大型风史莱姆", "大型雷史莱姆", "变异雷史莱姆",
        "大型草史莱姆", "大型冰史莱姆", "大型岩史莱姆",
        "火飘浮灵", "水飘浮灵", "风飘浮灵", "雷飘浮灵", "草飘浮灵", "冰飘浮灵", "岩飘浮灵"]

index = {}
for m in monsters:
    if m.get("Name"):
        index.setdefault(m["Name"], m)

for name in want:
    m = index.get(name)
    if not m:
        print(f"{name}\t-- 元数据无此条目 --")
    else:
        bv = m.get("BaseValue") or {}
        print(f"{name}\t{m.get('MonsterName')}\tType={m.get('Type')}\tdrops={len(m.get('Drops') or [])}"
              f"\tfire={bv.get('FireSubHurt')}")
