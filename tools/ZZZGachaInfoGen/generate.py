"""绝区零调频物品元数据生成器。

数据源与 Starward 一致：
    https://starward-static.scighost.com/metadata/v1/zzz/ZZZGachaInfo.nap_cn.zh-cn.json

生成 `src/Ellen.Launcher/Ellen.Launcher/Resource/Metadata/ZZZ/GachaItem.json`。
该文件只保存物品 id / 名称 / 图标 URL / 稀有度 / 类型，不包含任何图片本体；
图标在运行期由 CachedImage 从米游社 CDN 下载并缓存。

用法：
    python tools/ZZZGachaInfoGen/generate.py
    python tools/ZZZGachaInfoGen/generate.py --check   # 只比对，不写文件
"""

from __future__ import annotations

import argparse
import json
import sys
import urllib.request
from pathlib import Path

SOURCE_URL = (
    "https://starward-static.scighost.com/metadata/v1/zzz/"
    "ZZZGachaInfo.nap_cn.zh-cn.json"
)

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
TARGET_PATH = (
    REPOSITORY_ROOT
    / "src"
    / "Ellen.Launcher"
    / "Ellen.Launcher"
    / "Resource"
    / "Metadata"
    / "ZZZ"
    / "GachaItem.json"
)


def classify(item_id: int, icon: str) -> str:
    if "role_square_avatar" in icon:
        return "agent"
    if 50_000 <= item_id < 60_000:
        return "bangboo"
    return "wengine"


def fetch_items() -> list[dict]:
    request = urllib.request.Request(SOURCE_URL, headers={"User-Agent": "Ellen.Launcher"})
    with urllib.request.urlopen(request, timeout=60) as response:
        payload = json.load(response)

    return [
        {
            "id": int(item["id"]),
            "name": item["name"],
            "icon": item["icon"],
            "rarity": int(item["rarity"]),
            "type": classify(int(item["id"]), item["icon"]),
        }
        for item in payload["data"]["list"]
    ]


def build_document(items: list[dict]) -> dict:
    return {
        "source": SOURCE_URL,
        "game": "nap_cn",
        "lang": "zh-cn",
        "items": sorted(items, key=lambda item: item["id"]),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="只比对，不写文件")
    args = parser.parse_args()

    content = json.dumps(build_document(fetch_items()), ensure_ascii=False, indent=2) + "\n"

    if args.check:
        if not TARGET_PATH.exists():
            print(f"缺少目标文件: {TARGET_PATH}")
            return 1
        if TARGET_PATH.read_text(encoding="utf-8") != content:
            print(f"数据源已更新，请重新生成: {TARGET_PATH}")
            return 1
        print("已是最新。")
        return 0

    TARGET_PATH.parent.mkdir(parents=True, exist_ok=True)
    TARGET_PATH.write_text(content, encoding="utf-8", newline="\n")
    print(f"已写入 {TARGET_PATH}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
