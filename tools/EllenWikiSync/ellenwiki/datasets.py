# -*- coding: utf-8 -*-
"""数据集注册表：ellen.wiki API 端点 → 仓库内快照文件。

新增数据集时在这里加一条即可，``sync.py`` 会自动带上它。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Callable


@dataclass(frozen=True)
class Dataset:
    """一个「API 端点 → 随应用发布的快照」映射。"""

    name: str
    api_path: str
    destination: str  # 相对仓库根目录，统一用 '/'
    description: str
    summarize: Callable[[Any], str] | None = None


def _summarize_attachment(data: Any) -> str:
    characters = data.get("characters", {}) if isinstance(data, dict) else {}
    element_ids = data.get("elementIds", []) if isinstance(data, dict) else []
    char_count = sum(len(characters.get(element, [])) for element in element_ids)
    skill_count = sum(
        len(character.get("skills", []))
        for element in element_ids
        for character in characters.get(element, [])
    )
    return f"{len(element_ids)} 系 · {char_count} 角色 · {skill_count} 技能行"


DATASETS: dict[str, Dataset] = {
    "attachment": Dataset(
        name="attachment",
        api_path="/api/attachment",
        destination="src/Ellen.Launcher/Ellen.Launcher/Resource/CombatNotes/AvatarCombatNotes.json",
        description="元素附着及产球（逐角色技能的元素量 / 附着规则 / 产球）",
        summarize=_summarize_attachment,
    ),
}


def select_datasets(names: list[str]) -> list[Dataset]:
    """按名称挑选数据集；未指定时返回全部（保持注册顺序）。"""
    if not names:
        return list(DATASETS.values())

    unknown = [name for name in names if name not in DATASETS]
    if unknown:
        known = "、".join(DATASETS)
        raise KeyError(f"未知数据集：{'、'.join(unknown)}（可用：{known}）")

    return [DATASETS[name] for name in names]
