# -*- coding: utf-8 -*-
"""同步 ellen.wiki 的公开数据 API 到仓库内的快照文件。

用法（在仓库根目录执行）：:

    python tools/EllenWikiSync/sync.py                 # 同步全部数据集
    python tools/EllenWikiSync/sync.py attachment      # 只同步指定数据集
    python tools/EllenWikiSync/sync.py --list          # 列出可用数据集
    python tools/EllenWikiSync/sync.py --check         # 只比对，不写文件
    python tools/EllenWikiSync/sync.py --force         # 忽略本地缓存重新拉取

数据源与字段说明见 https://ellen.wiki/#/developer 。
快照写入后需要重新构建，并按 README「打包」一节同步到 ``Installer\\Publish``。
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))

from ellenwiki import DATASETS, Dataset, EllenWikiApi, EllenWikiError, select_datasets  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[2]
CACHE_DIR = Path(__file__).resolve().parent / ".cache"


def serialize(data: Any) -> str:
    """统一的快照序列化方式：UTF-8、两空格缩进、结尾换行、保留原始顺序。"""
    return json.dumps(data, ensure_ascii=False, indent=2) + "\n"


def run(args: argparse.Namespace) -> int:
    if args.list:
        print(f"数据源：{args.base_url}")
        for dataset in DATASETS.values():
            print(f"  {dataset.name:<12} {dataset.api_path:<20} {dataset.description}")
        return 0

    try:
        datasets = select_datasets(args.datasets)
    except KeyError as ex:
        print(f"[错误] {ex.args[0]}", file=sys.stderr)
        return 2

    api = EllenWikiApi(
        base_url=args.base_url,
        cache_dir=CACHE_DIR,
        force=args.force,
        offline=args.offline,
    )

    try:
        manifest = api.manifest()
    except EllenWikiError as ex:
        print(f"[警告] 读取清单失败：{ex}", file=sys.stderr)
        manifest = {}

    generated_at = manifest.get("generatedAt") if isinstance(manifest, dict) else None
    if generated_at:
        print(f"站点数据生成于：{generated_at}")

    has_change = False
    failed = False

    for dataset in datasets:
        print(f"\n== {dataset.name} ==")
        print(f"  {dataset.description}")

        try:
            data = api.get_json(dataset.api_path)
        except EllenWikiError as ex:
            print(f"  [失败] {ex}", file=sys.stderr)
            failed = True
            continue

        if dataset.summarize is not None:
            try:
                print(f"  概览：{dataset.summarize(data)}")
            except Exception as ex:  # noqa: BLE001 - 概览失败不应影响同步
                print(f"  [警告] 生成概览失败：{ex}", file=sys.stderr)

        destination = REPO_ROOT / dataset.destination
        new_text = serialize(data)
        old_text = destination.read_text(encoding="utf-8") if destination.exists() else None
        changed = old_text != new_text
        has_change = has_change or changed

        if not changed:
            print(f"  未变化：{dataset.destination}")
            continue

        verb = "将写入" if old_text is None else "将更新"
        if args.check or args.dry_run:
            verb = "需要写入" if old_text is None else "需要更新"
            print(f"  {verb}：{dataset.destination}")
            continue

        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(new_text, encoding="utf-8", newline="\n")
        print(f"  {verb}：{dataset.destination}")

    print()
    if failed:
        print("存在失败的数据集。", file=sys.stderr)
        return 1
    if args.check or args.dry_run:
        print("有变化，可执行同步。" if has_change else "全部已是最新。")
    else:
        print("同步完成；有变化。" if has_change else "同步完成；全部已是最新。")
        if has_change:
            print("提示：产物已更新，需重新构建并按 README 打包流程部署。")
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="同步 ellen.wiki 公开数据 API 到仓库内快照。",
    )
    parser.add_argument("datasets", nargs="*", help="要同步的数据集；留空表示全部")
    parser.add_argument("--list", action="store_true", help="列出可用数据集后退出")
    parser.add_argument("--check", action="store_true", help="只比对是否需要更新，不写文件")
    parser.add_argument("--dry-run", action="store_true", help="同 --check")
    parser.add_argument("--force", action="store_true", help="忽略本地缓存，强制重新拉取")
    parser.add_argument("--offline", action="store_true", help="只用本地缓存，不发起网络请求")
    parser.add_argument(
        "--base-url",
        default="https://ellen.wiki",
        help="接口根地址（默认 https://ellen.wiki）",
    )
    return parser.parse_args(argv)


def main() -> int:
    return run(parse_args(sys.argv[1:]))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
