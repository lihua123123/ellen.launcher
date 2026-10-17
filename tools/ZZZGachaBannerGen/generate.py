"""绝区零调频档期（卡池 UP 与卡池图）生成器。

数据源：
1. 档期与 UP 物品：HoYo.Gacha 公共元数据 v2
   https://hoyo-gacha-v1.lgou2w.com/GachaMetadata/v2/index.json
   https://hoyo-gacha-v1.lgou2w.com/GachaMetadata/v2/<latest>.json
2. 卡池图：米游社官方公告（公开搜索接口，按 UP 物品名匹配）
   https://bbs-api.miyoushe.com/post/wapi/searchPosts
   - 「X版本限时频段」：一期频段的多张卡池图
   - 「XXX调频说明」：单个卡池的卡池图（补齐 1.0 ~ 1.6）

生成 `src/Ellen.Launcher/Ellen.Launcher/Resource/Metadata/ZZZ/GachaBanner.json`。
HoYo.Gacha 把同期开放的两个频段合并成一行（例如 1.4 同时开放「砺世澄沧」「循此苦旅」），
这里按 UP 物品把每一行拆成独立的卡池记录，并各自配上官方公告图，
使历史页与原神一样按「单个卡池」展示。
物品名称与方形图标由 `GachaItem.json`（Starward 数据源）提供。

用法：
    python tools/ZZZGachaBannerGen/generate.py
    python tools/ZZZGachaBannerGen/generate.py --check   # 只比对，不写文件
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
import urllib.parse
import urllib.request
from datetime import datetime, timedelta
from pathlib import Path

INDEX_URL = "https://hoyo-gacha-v1.lgou2w.com/GachaMetadata/v2/index.json"
DATA_URL = "https://hoyo-gacha-v1.lgou2w.com/GachaMetadata/v2/{0}.json"
BBS_SEARCH_URL = "https://bbs-api.miyoushe.com/post/wapi/searchPosts?keyword={0}&gids=8&size=50&last_id={1}"
USER_AGENT = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Ellen.Launcher"}

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
METADATA_DIR = REPOSITORY_ROOT / "src" / "Ellen.Launcher" / "Ellen.Launcher" / "Resource" / "Metadata" / "ZZZ"
TARGET_PATH = METADATA_DIR / "GachaBanner.json"
ITEM_PATH = METADATA_DIR / "GachaItem.json"

VERSION_LIMITED_RE = re.compile(r"(\d+\.\d+)\u7248\u672c\u9650\u65f6\u9891\u6bb5")  # X版本限时频段
VERSION_IN_SUBJECT_RE = re.compile(r"(\d+\.\d+)\u7248\u672c")  # X版本
SUBJECT_TITLE_RE = re.compile(r"\u300c([^\u300d]+)\u300d")  # 「...」
BANNER_TITLE_RE = re.compile(r"\u300c([^\u300d]+)\u300d\u8c03\u9891\u6d3b\u52a8")  # 「...」调频活动
S_ITEM_RE = re.compile(r"(?:\u9650\u5b9a)?S\u7ea7(\u4ee3\u7406\u4eba|\u97f3\u64ce)\[([^\(\[]+)")  # (限定)S级代理人/音擎[名字

# 公告里的名字与游戏内物品名存在细微差异（正体罗马数字、角色全名）。
ROMAN_NUMERALS = {
    "\u2160": "I",
    "\u2161": "II",
    "\u2162": "III",
    "\u2163": "IV",
    "\u2164": "V",
    "\u2165": "VI",
    "\u2166": "VII",
    "\u2167": "VIII",
    "\u2168": "IX",
    "\u2169": "X",
}
NAME_NOISE = "\u300c\u300d\u300e\u300f\u00b7\u30fb \u3000"


def fetch_json(url: str) -> dict:
    last_error: Exception | None = None
    for attempt in range(4):
        try:
            request = urllib.request.Request(url, headers=USER_AGENT)
            with urllib.request.urlopen(request, timeout=120) as response:
                return json.load(response)
        except Exception as error:  # noqa: BLE001 - retry any transport/JSON error
            last_error = error
            time.sleep(2 * (attempt + 1))
    raise RuntimeError(f"无法下载 {url}: {last_error}")


def fetch_bbs_posts(keyword: str) -> dict[str, dict]:
    posts: dict[str, dict] = {}
    last_id = 0
    visited: set[int] = set()
    encoded = urllib.parse.quote(keyword)
    for _ in range(10):
        if last_id in visited:
            break
        visited.add(last_id)
        data = fetch_json(BBS_SEARCH_URL.format(encoded, last_id))
        page = data.get("data", {}).get("posts", [])
        for entry in page:
            post = entry.get("post") or {}
            post_id = post.get("post_id")
            if post_id:
                posts[post_id] = post
        next_id = data.get("data", {}).get("last_id")
        if not page or not next_id:
            break
        last_id = next_id
        time.sleep(0.3)
    return posts


def extract_banner_images(post: dict) -> list[dict]:
    """从公告的 structured_content 中按顺序提取「卡池图 + S 级物品名」。"""
    try:
        content = json.loads(post.get("structured_content") or "[]")
    except json.JSONDecodeError:
        return []

    sequence: list[tuple[str, str]] = []
    for entry in content:
        insert = entry.get("insert")
        if isinstance(insert, str):
            sequence.append(("text", insert))
        elif isinstance(insert, dict) and "image" in insert:
            sequence.append(("image", str(insert["image"])))

    images = post.get("images") or []
    results: list[dict] = []
    image_index = 0
    buffer = ""

    for index, (kind, value) in enumerate(sequence):
        if kind == "text":
            buffer += value
            continue

        image_url = images[image_index] if image_index < len(images) else ""
        image_index += 1

        after = ""
        for next_kind, next_value in sequence[index + 1 :]:
            if next_kind == "image":
                break
            after += next_value

        match = S_ITEM_RE.search(after)
        if match and image_url:
            title = BANNER_TITLE_RE.search(buffer)
            results.append(
                {
                    "title": title.group(1) if title else "",
                    "kind": "agent" if match.group(1) == "\u4ee3\u7406\u4eba" else "engine",
                    "name": match.group(2).strip(),
                    "image": image_url,
                }
            )
        buffer = ""

    return results


def normalize_name(text: str) -> str:
    text = text.strip()
    for numeral, plain in ROMAN_NUMERALS.items():
        text = text.replace(numeral, plain)
    for noise in NAME_NOISE:
        text = text.replace(noise, "")
    return text.upper()


def match_score(item_name: str, entry_name: str) -> int:
    left, right = normalize_name(item_name), normalize_name(entry_name)
    if not left or not right:
        return 0
    if left == right:
        return 4
    if right.startswith(left):
        return 3
    if left in right:
        return 2
    if right in left:
        return 1
    return 0


def collect_announcement_entries() -> list[dict]:
    """返回公告中的卡池图条目：(类型, 物品名, 图 URL, 卡池名, 版本, 发布时间)。"""
    entries: list[dict] = []

    def add_posts(keyword: str) -> None:
        for post in fetch_bbs_posts(keyword).values():
            if not (post.get("post_status") or {}).get("is_official"):
                continue

            subject = post.get("subject") or ""
            version = ""
            limited = VERSION_LIMITED_RE.search(subject)
            if limited:
                version = limited.group(1)
            else:
                in_subject = VERSION_IN_SUBJECT_RE.search(subject)
                if in_subject:
                    version = in_subject.group(1)

            subject_title = SUBJECT_TITLE_RE.search(subject)
            created = post.get("created_at")
            created = created if isinstance(created, (int, float)) else 0

            for image in extract_banner_images(post):
                entries.append(
                    {
                        "kind": image["kind"],
                        "name": image["name"],
                        "image": image["image"],
                        "title": image["title"] or (subject_title.group(1) if subject_title else ""),
                        "version": version,
                        "created": created,
                    }
                )

    add_posts("\u8c03\u9891\u8bf4\u660e")  # 调频说明
    add_posts("\u9650\u65f6\u9891\u6bb5")  # 限时频段
    return entries


def parse_time(text: str) -> datetime:
    return datetime.fromisoformat(text)


def is_near_start(entry: dict, start: datetime) -> bool:
    if not entry["created"]:
        return False
    created = datetime.fromtimestamp(entry["created"], tz=start.tzinfo)
    return start - timedelta(days=10) <= created <= start + timedelta(days=2)


def pick_entry(entries: list[dict], used: set[int], item_name: str, version: str, start: datetime) -> int | None:
    best_index = None
    best_score = 0

    for index, entry in enumerate(entries):
        if index in used:
            continue
        if version and entry["version"] and entry["version"] != version:
            continue

        score = match_score(item_name, entry["name"])
        if score == 0:
            continue

        # 同名的卡池会在多个版本重复上架，优先取本期公告
        score = (score * 10) + (3 if is_near_start(entry, start) else 0)
        if score > best_score:
            best_index, best_score = index, score

    return best_index


def build_document() -> dict:
    index = fetch_json(INDEX_URL)
    latest = index["Latest"]
    metadata = fetch_json(DATA_URL.format(latest))

    nap = next((entry for entry in metadata if int(entry.get("Id", -1)) == 2), None)
    if nap is None:
        raise RuntimeError("元数据中没有绝区零（Id = 2）条目")

    item_names: dict[int, str] = {}
    if ITEM_PATH.exists():
        item_doc = json.loads(ITEM_PATH.read_text(encoding="utf-8"))
        item_names = {int(item["id"]): item["name"] for item in item_doc.get("items", [])}

    entries = collect_announcement_entries()
    entries_by_kind: dict[str, list[dict]] = {
        "agent": [entry for entry in entries if entry["kind"] == "agent"],
        "engine": [entry for entry in entries if entry["kind"] == "engine"],
    }

    # 先按档期把每个 UP 物品配上自己的卡池图；配不上的（早期复刻自选频段）稍后复用同物品的历史图。
    resolved: dict[tuple[int, int], tuple[str, str]] = {}
    images_by_item: dict[int, tuple[str, str]] = {}

    banner_rows = nap.get("Banners", [])
    for row_index, row in enumerate(banner_rows):
        gacha_type = int(row["GachaType"])
        kind = "agent" if gacha_type in (2, 102) else "engine"
        version = row.get("Version") or ""
        start = parse_time(row["StartTime"])
        candidates = entries_by_kind[kind]
        used: set[int] = set()

        for item_id in (int(value) for value in row.get("UpGolden", [])):
            index_found = pick_entry(candidates, used, item_names.get(item_id, ""), version, start)
            if index_found is None:
                continue

            used.add(index_found)
            entry = candidates[index_found]
            resolved[(row_index, item_id)] = (entry["image"], entry["title"])
            images_by_item.setdefault(item_id, (entry["image"], entry["title"]))

    # 复刻/回归的卡池公告常常没有单独标题，退回使用该物品首次上架时的卡池名。
    titles_by_item: dict[int, str] = {}
    for (_row_index, item_id), (_image, title) in resolved.items():
        if title:
            titles_by_item.setdefault(item_id, title)

    banners = []
    for row_index, row in enumerate(banner_rows):
        gacha_type = int(row["GachaType"])
        up = [int(value) for value in row.get("UpGolden", [])]

        for item_id in up:
            image, title = resolved.get((row_index, item_id)) or images_by_item.get(item_id, ("", ""))
            banners.append(
                {
                    "type": gacha_type,
                    "start": row["StartTime"],
                    "end": row["EndTime"],
                    "name": title or titles_by_item.get(item_id, ""),
                    "up": [item_id],
                    "up_purple": [int(value) for value in row.get("UpPurple", [])],
                    "version": row.get("Version") or "",
                    "image": image,
                }
            )

    banners.sort(key=lambda banner: (banner["start"], banner["type"], banner["up"]))
    with_image = sum(1 for banner in banners if banner["image"])

    return {
        "source": DATA_URL.format(latest),
        "index": INDEX_URL,
        "latest": latest,
        "banner_image_source": "https://bbs-api.miyoushe.com/post/wapi/searchPosts",
        "banner_image_count": with_image,
        "banners": banners,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="只比对，不写文件")
    args = parser.parse_args()

    document = build_document()
    content = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    total = len(document["banners"])
    print(f"卡池 {total} 个，其中 {document['banner_image_count']} 个有官方卡池图")

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
