# -*- coding: utf-8 -*-
"""把 C2 敌人数据（index + 753 items）快照进安装包 Web753 目录。

这是 P5「方案 B」的地基：**753 条 index/items JSON 随包**，使首次无缓存断网也能离线浏览，
不再回退到内置 722。缩略图/详情大图仍按 A4 另行处理（在线 + 按需缓存）。

数据源：
  - 默认：仓库旁的 ellen-wiki/src/data/enemies（index.json + items/<id>.json）。
  - --source DIR：指定本地源目录。
  - --live：从 ellen.wiki 在线拉取（需网络，复用 ellenwiki 模块）。

产物（Installer/Publish/Resource/EnemyArchives/Web753）：
  - index.json            ：原样拷贝的 C2 索引。
  - <id>.json             ：扁平的 753 个条目文件（文件名即 slug，与线上 /api/enemies/<slug>.json 一致）。
  - bundle-manifest.json  ：{ schema, generatedAt, source, count, indexSha256, itemSha256:{id:sha} }。

用法：
    python tools/EllenWikiSync/sync_bundle.py                 # 从本地 ellen-wiki 源快照
    python tools/EllenWikiSync/sync_bundle.py --source DIR    # 指定源目录
    python tools/EllenWikiSync/sync_bundle.py --live          # 在线拉取
    python tools/EllenWikiSync/sync_bundle.py --check         # 只比对，不写文件
    python tools/EllenWikiSync/sync_bundle.py --compare       # 写完后比对线上 digest（需网络，失败不报错）
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SOURCE = REPO_ROOT.parents[0] / "ellen-wiki" / "src" / "data" / "enemies"
OUT_DIR = REPO_ROOT / "Installer" / "Publish" / "Resource" / "EnemyArchives" / "Web753"
# 同一份合并包还要写进项目资源目录：客户端把它作为 EmbeddedResource 编进程序集（内置 753 条），
# 只嵌这一个文件，逐条 json 仍只随安装包发。两边必须同源，否则内置数据会悄悄落后于随包副本。
EMBED_COPY = (REPO_ROOT / "src" / "Ellen.Launcher" / "Ellen.Launcher" / "Resource"
              / "EnemyArchives" / "Web753" / "bundled-all.json")
MANIFEST_SCHEMA = "ellen-wiki-bundle-v1"
MERGED_SCHEMA = "ellen-enemy-bundle-v1"
TOOL_VERSION = "1.1.0"


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def serialize(data: Any) -> str:
    """统一的快照序列化：UTF-8、两空格缩进、结尾换行、保留原始顺序。"""
    return json.dumps(data, ensure_ascii=False, indent=2) + "\n"


def load_local_source(source: Path) -> tuple[dict, list[tuple[str, dict]]]:
    """读取本地 index.json + items/*.json。返回 (index_dict, [(id, item_dict), ...])。"""
    index_path = source / "index.json"
    if not index_path.exists():
        raise FileNotFoundError(f"源目录缺少 index.json：{index_path}")
    index: dict = json.loads(index_path.read_text(encoding="utf-8"))
    items: list[tuple[str, dict]] = []
    items_dir = source / "items"
    if not items_dir.is_dir():
        raise FileNotFoundError(f"源目录缺少 items/ 子目录：{items_dir}")
    for file in sorted(items_dir.glob("*.json")):
        item = json.loads(file.read_text(encoding="utf-8"))
        item_id = item.get("id")
        if not item_id:
            print(f"  [警告] {file.name} 无 id 字段，跳过", file=sys.stderr)
            continue
        if item_id != file.stem:
            print(f"  [警告] {file.name} 的 id={item_id} 与文件名不符，将用 id 命名", file=sys.stderr)
        items.append((item_id, item))
    return index, items


def load_live_source(base_url: str) -> tuple[dict, list[tuple[str, dict]]]:
    """从 ellen.wiki 在线拉取（复用 ellenwiki 模块）。"""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from ellenwiki import EllenWikiApi, EllenWikiError  # noqa: E402

    api = EllenWikiApi(base_url=base_url, cache_dir=Path(__file__).resolve().parent / ".cache", force=False, offline=False)
    # C2 敌人索引：/api/enemies/index.json
    try:
        index = api.get_json("/api/enemies/index.json")
    except EllenWikiError as ex:
        raise RuntimeError(f"拉取 index 失败：{ex}") from ex
    items: list[tuple[str, dict]] = []
    enemies = index.get("enemies", []) if isinstance(index, dict) else index
    for entry in enemies:
        eid = entry.get("id")
        if not eid:
            continue
        try:
            item = api.get_json(f"/api/enemies/{eid}.json")
        except EllenWikiError as ex:
            print(f"  [警告] 拉取 {eid} 失败：{ex}", file=sys.stderr)
            continue
        items.append((eid, item))
    return index, items


def write_bundle(out_dir: Path, index: dict, items: list[tuple[str, dict]], source_label: str, check: bool) -> dict:
    out_dir.mkdir(parents=True, exist_ok=True)
    index_text = serialize(index)
    index_sha = sha256_bytes(index_text.encode("utf-8"))

    item_shas: dict[str, str] = {}
    writes: list[tuple[Path, str]] = [(out_dir / "index.json", index_text)]
    for eid, item in items:
        text = serialize(item)
        item_shas[eid] = sha256_bytes(text.encode("utf-8"))
        writes.append((out_dir / f"{eid}.json", text))

    # 合并包：客户端逐个打开 753 个文件要 ~600ms；这一个文件（~2.3 MiB）一次反序列化即可。
    # 逐条 JSON 仍然保留——合并包缺失/损坏时客户端回退到逐条扫描，不改变既有契约。
    merged = {
        "schema": MERGED_SCHEMA,
        "count": len(items),
        "index": index,
        "items": {eid: item for eid, item in items},
    }
    merged_text = json.dumps(merged, ensure_ascii=False, separators=(",", ":"))
    merged_sha = sha256_bytes(merged_text.encode("utf-8"))
    writes.append((out_dir / "bundled-all.json", merged_text))
    writes.append((EMBED_COPY, merged_text))

    if check:
        print(f"  [check] 将写入 {len(writes)} 个文件到 {out_dir}")
        changed = 0
        for path, text in writes:
            if not path.exists() or path.read_text(encoding="utf-8") != text:
                changed += 1
        print(f"  [check] 其中 {changed} 个文件需要更新")
        return {"changed": changed, "count": len(items), "indexSha256": index_sha}

    for path, text in writes:
        path.write_text(text, encoding="utf-8", newline="\n")

    manifest = {
        "schema": MANIFEST_SCHEMA,
        "toolVersion": TOOL_VERSION,
        "generatedAt": __import__("datetime").datetime.now(__import__("datetime").timezone.utc).isoformat(),
        "source": source_label,
        "count": len(items),
        "indexSha256": index_sha,
        "itemSha256": item_shas,
        "merged": {"file": "bundled-all.json", "sha256": merged_sha, "bytes": len(merged_text.encode("utf-8"))},
    }
    # assets 段由 sync_assets.py 维护（P6 图片包统计），重跑本脚本不能把它冲掉
    previous = out_dir / "bundle-manifest.json"
    if previous.exists():
        try:
            prior = json.loads(previous.read_text(encoding="utf-8"))
            if isinstance(prior.get("assets"), dict):
                manifest["assets"] = prior["assets"]
        except (json.JSONDecodeError, OSError):
            pass
    (out_dir / "bundle-manifest.json").write_text(serialize(manifest), encoding="utf-8", newline="\n")
    print(f"  已写入 {len(writes)} 个文件（index + {len(items)} items）+ bundle-manifest.json")
    return manifest


def compare_with_live(out_dir: Path) -> int:
    """构建后比对线上 digest：拉取线上 index 的 sha，与本地 manifest 比较。失败不报错。"""
    manifest_path = out_dir / "bundle-manifest.json"
    if not manifest_path.exists():
        print("  [compare] 无 bundle-manifest.json，跳过比对", file=sys.stderr)
        return 0
    local_sha = json.loads(manifest_path.read_text(encoding="utf-8")).get("indexSha256")
    try:
        sys.path.insert(0, str(Path(__file__).resolve().parent))
        from ellenwiki import EllenWikiApi  # noqa: E402

        api = EllenWikiApi(base_url="https://ellen.wiki", cache_dir=Path(__file__).resolve().parent / ".cache", force=False, offline=False)
        live_bytes = api.get_raw("/api/enemies/index.json")
        live_sha = sha256_bytes(live_bytes)
        if live_sha == local_sha:
            print("  [compare] 随包 index 与线上 digest 一致（sha256 相同）")
            return 0
        print(f"  [compare] 随包与线上不一致：local={local_sha[:12]}… live={live_sha[:12]}…（构建期漂移，发布前需重跑本脚本）", file=sys.stderr)
        return 1
    except Exception as ex:  # noqa: BLE001 - 比对失败不应阻断构建
        print(f"  [compare] 无法比对线上（{ex}），跳过", file=sys.stderr)
        return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="快照 C2 敌人数据到安装包 Web753 目录（P5 方案 B 地基）。")
    parser.add_argument("--source", type=str, default=None, help="本地源目录（含 index.json 与 items/）")
    parser.add_argument("--out", type=str, default=None, help="输出目录（默认 Installer/Publish/Resource/EnemyArchives/Web753）")
    parser.add_argument("--live", action="store_true", help="从 ellen.wiki 在线拉取（需网络）")
    parser.add_argument("--base-url", default="https://ellen.wiki", help="在线源根地址")
    parser.add_argument("--check", action="store_true", help="只比对是否需要更新，不写文件")
    parser.add_argument("--compare", action="store_true", help="写完后比对线上 digest（需网络，失败不报错）")
    args = parser.parse_args()

    out_dir = Path(args.out) if args.out else OUT_DIR
    source_label: str

    try:
        if args.live:
            print(f"数据源：{args.base_url}（在线）")
            index, items = load_live_source(args.base_url)
            source_label = f"live:{args.base_url}"
        else:
            source = Path(args.source) if args.source else DEFAULT_SOURCE
            print(f"数据源：{source}")
            if not source.exists():
                print(f"[错误] 源目录不存在：{source}（可用 --source 指定，或 --live 在线拉取）", file=sys.stderr)
                return 2
            index, items = load_local_source(source)
            source_label = f"local:{source}"
    except (FileNotFoundError, RuntimeError) as ex:
        print(f"[错误] {ex}", file=sys.stderr)
        return 2

    print(f"读取到 index + {len(items)} 个条目")

    if args.check:
        result = write_bundle(out_dir, index, items, source_label, check=True)
        return 1 if result["changed"] > 0 else 0

    manifest = write_bundle(out_dir, index, items, source_label, check=False)
    print(f"完成：{manifest['count']} 条，index sha256={manifest['indexSha256'][:16]}…")

    if args.compare:
        return compare_with_live(out_dir)
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
