# -*- coding: utf-8 -*-
"""把「元素附着及产球」表转成角色资料页用的战斗备注数据。

数据源（唯一来源，以后更新角色图鉴就改这里）：
    D:\\艾莲的数据库\\ellen-wiki\\content\\attachment\\元素附着及产球.md

产物：
    src/Ellen.Launcher/Ellen.Launcher/Resource/CombatNotes/AvatarCombatNotes.json

用法（在仓库根目录执行）：
    python tools/GenAvatarCombatNotes.py                     # 直接写回产物
    python tools/GenAvatarCombatNotes.py --out 临时文件.json   # 只写到别处
    python tools/GenAvatarCombatNotes.py --report             # 额外打印统计与差异

以后更新角色图鉴的流程：
    1. 在 ellen-wiki 里改这份 md（新增角色/修正数据）；
    2. 跑 python tools/GenAvatarCombatNotes.py --report，看「新增/移除/内容有变」
       以及「对不上 metadata 名」；
    3. 名字对不上时在 NAME_ALIASES 里补一条（运行期是按 metadata 的名字查表的）；
    4. 重新构建并把产物部署到 Installer\\Publish。

归属规则（与角色资料页的 target 枚举对应）：
    普攻/重击/瞄准/下落/变式        -> NormalAttack
    e / E / E 释放 / E 变招一 …    -> ElementalSkill
    Q / Q 释放 / Q 后续 …          -> ElementalBurst
    突破天赋 N / 固有天赋 N         -> InherentN
    命座 N                         -> ConstellationN

转换规则：
    附着规则 "3 hits / 1 s" 或 "2.5 s/3 hit" -> "3 hit/2.5 s"
    一组里有多行时技能列写全称（"元素战技 变招一"），只有一行时留空
"""
import argparse
import json
import re
import sys
from collections import OrderedDict
from pathlib import Path

DEFAULT_SRC = Path(r"D:\艾莲的数据库\ellen-wiki\content\attachment\元素附着及产球.md")
REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DST = (
    REPO_ROOT
    / "src"
    / "Ellen.Launcher"
    / "Ellen.Launcher"
    / "Resource"
    / "CombatNotes"
    / "AvatarCombatNotes.json"
)
# 与 metadata 对账用的角色名快照（--report 时用它检查 key 对不对得上）
DEFAULT_METADATA = (
    REPO_ROOT
    / "src"
    / "Ellen.Launcher"
    / "Ellen.Launcher"
    / "Resource"
    / "Metadata"
    / "Genshin"
    / "CHS"
    / "Avatar"
)

# 表的角色名与游戏内 metadata 用字不一致时的修正（左：表里的写法，右：metadata 的写法）。
# 运行时是按 metadata 的角色名查表的，名字对不上就等于没收录。
NAME_ALIASES = {
    "茜特拉莉": "茜特菈莉",  # 茜特菈莉（Citlali），表里写成了「特拉莉」
}

COLUMN_COUNT = 6
HEADER_FIRST_CELL = "技能"
# 表头顺序：技能 | 元素量 | 附着规则 | 产球 | 备注 | 抗打断
REMARK_INDEX = 4
POISE_INDEX = 5

ATTACHMENT_PATTERNS = (
    # 3 hits / 1 s  或  3 hit/1 s
    (re.compile(r"^([\d.]+)\s*hits?\s*/\s*([\d.]+)\s*s$"), lambda m: f"{m.group(1)} hit/{m.group(2)} s"),
    # 2.5 s / 3 hit
    (re.compile(r"^([\d.]+)\s*s\s*/\s*([\d.]+)\s*hits?$"), lambda m: f"{m.group(2)} hit/{m.group(1)} s"),
)


def classify(skill):
    """把一个技能名映射到角色资料页里的天赋卡片。"""
    s = skill.strip()

    m = re.match(r"^命座\s*([\d/、,]+)", s)
    if m:
        return [f"Constellation{n}" for n in re.findall(r"\d+", m.group(1))]

    m = re.match(r"^(?:突破|固有)天赋\s*([\d/、,]+)", s)
    if m:
        return [f"Inherent{n}" for n in re.findall(r"\d+", m.group(1))]

    if s[:1] in ("e", "E"):
        return ["ElementalSkill"]

    if s[:1] == "Q":
        return ["ElementalBurst"]

    return ["NormalAttack"]


def convert_attachment(value):
    s = value.strip()
    for pattern, formatter in ATTACHMENT_PATTERNS:
        m = pattern.match(s)
        if m:
            return formatter(m)
    return s


def convert_skill_label(skill, target, multi):
    if not multi:
        return ""

    s = skill.strip()
    if target == "ElementalSkill":
        s = re.sub(r"^[eE](/[eE])?\s*", "元素战技 ", s).strip()
    elif target == "ElementalBurst":
        s = re.sub(r"^Q\s*", "元素爆发 ", s).strip()
    return s


def parse(markdown, unknown_attachments, skipped_rows, element_marks):
    """按角色解析 markdown，返回 [{name, rows}]。"""
    characters = []
    current = None

    for raw in markdown.splitlines():
        line = raw.strip()

        if line.startswith("# ") and not line.startswith("## "):
            element_marks.append(line[2:].strip())
            current = None
            continue

        if line.startswith("## "):
            name = re.sub(r"（[^（）]*/[^（）]*）\s*$", "", line[3:].strip()).strip()
            current = {"name": name, "rows": []}
            characters.append(current)
            continue

        if current is None or not line.startswith("|"):
            continue

        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) != COLUMN_COUNT:
            # 表头、分隔行、以及文档开头的全局表都会走到这里
            if len(cells) > 1 and cells[0] != HEADER_FIRST_CELL and not set(cells[0]) <= set(":- "):
                skipped_rows.append((current["name"], cells))
            continue

        if cells[0] == HEADER_FIRST_CELL or set(cells[0]) <= set(":- "):
            continue

        row = {
            "skill": cells[0],
            "elementAmount": cells[1],
            "attachment": cells[2],
            "particle": cells[3],
            "remark": cells[REMARK_INDEX],
            "poise": cells[POISE_INDEX],
        }

        if row["attachment"] and convert_attachment(row["attachment"]) == row["attachment"]:
            unknown_attachments.add(row["attachment"])

        # 整行全空的“占位行”直接丢掉
        if any(row[k] for k in ("elementAmount", "attachment", "particle", "poise", "remark")):
            current["rows"].append(row)

    return characters


def build(characters):
    result = []
    for ch in characters:
        if not ch["rows"]:
            continue

        grouped = OrderedDict()
        for row in ch["rows"]:
            for target in classify(row["skill"]):
                grouped.setdefault(target, []).append(row)

        entries = []
        for target, rows in grouped.items():
            multi = len(rows) > 1
            entries.append(
                {
                    "target": target,
                    "rows": [
                        {
                            "skill": convert_skill_label(r["skill"], target, multi),
                            "elementAmount": r["elementAmount"],
                            "attachment": convert_attachment(r["attachment"]),
                            "particle": r["particle"],
                            "poise": r["poise"],
                            "remark": r["remark"],
                        }
                        for r in rows
                    ],
                }
            )

        result.append({"name": NAME_ALIASES.get(ch["name"], ch["name"]), "entries": entries})

    return result


def load_metadata_names(folder):
    if not folder.exists():
        return set()

    names = set()
    for file in folder.glob("*.json"):
        data = json.loads(file.read_text(encoding="utf-8"))
        if isinstance(data, dict) and "Name" in data:
            names.add(data["Name"])
    return names


def load_existing(path):
    if not path.exists():
        return {}
    with open(path, encoding="utf-8") as f:
        return {c["name"]: c["entries"] for c in json.load(f)}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--src", type=Path, default=DEFAULT_SRC)
    parser.add_argument("--out", type=Path, default=DEFAULT_DST)
    parser.add_argument("--metadata", type=Path, default=DEFAULT_METADATA)
    parser.add_argument("--report", action="store_true")
    args = parser.parse_args()

    unknown_attachments = set()
    skipped_rows = []
    element_marks = []
    characters = parse(args.src.read_text(encoding="utf-8"), unknown_attachments, skipped_rows, element_marks)
    result = build(characters)

    # 先把上一版读进来，再覆盖，这样 --report 才能看到真正的差异
    existing = load_existing(DEFAULT_DST) if args.report else {}

    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write("\n")

    total_entries = sum(len(c["entries"]) for c in result)
    total_rows = sum(len(e["rows"]) for c in result for e in c["entries"])
    print(f"源文件  : {args.src}")
    print(f"输出    : {args.out}")
    print(f"系别    : {'、'.join(element_marks)}")
    print(f"角色数  : {len(result)}")
    print(f"条目数  : {total_entries}")
    print(f"行数    : {total_rows}")

    if args.report:
        new = {c["name"]: c["entries"] for c in result}

        added = [n for n in new if n not in existing]
        removed = [n for n in existing if n not in new]
        changed = [
            n
            for n in new
            if n in existing
            and json.dumps(new[n], ensure_ascii=False, sort_keys=True)
            != json.dumps(existing[n], ensure_ascii=False, sort_keys=True)
        ]

        print(f"新增角色({len(added)})  : {'、'.join(added) if added else '无'}")
        print(f"移除角色({len(removed)})  : {'、'.join(removed) if removed else '无'}")
        print(f"内容有变({len(changed)})  : {'、'.join(changed) if changed else '无'}")

        metadata_names = load_metadata_names(args.metadata)
        if metadata_names:
            unmatched = [n for n in new if n not in metadata_names]
            uncovered = [n for n in sorted(metadata_names) if n not in new]
            print(f"对不上 metadata 名({len(unmatched)})：{'、'.join(unmatched) if unmatched else '无'}")
            print(f"metadata 已有、表里没收录({len(uncovered)})：{'、'.join(uncovered) if uncovered else '无'}")

        if unknown_attachments:
            print(f"未归一化附着规则({len(unknown_attachments)})：")
            for value in sorted(unknown_attachments):
                print(f"    {value}")
        if skipped_rows:
            print(f"因列数不符被跳过的行({len(skipped_rows)})：")
            for name, cells in skipped_rows[:20]:
                print(f"    [{name}] {cells}")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
