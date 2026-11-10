# -*- coding: utf-8 -*-
"""把 A4 的 assets manifest 与图片拷进安装包（P6 方案 B 最后一层：图片随包）。

与 sync_bundle.py 的分工：
  - sync_bundle.py：C2 的 753 条 index/items JSON → Web753/<slug>.json
  - 本脚本      ：A4 的 assets-manifest.json + 图片 → Web753/assets/

产物布局（Web753/assets/）：
  assets-manifest.json                上游清单原样拷贝（含 license 块，客户端展示署名用）
  orig/<id>/<file>                    清单里**没有 thumb** 的条目（icon / 技能图标等小图，本身就是显示资源）
  thumbs/<id>/thumb-<stem>.webp       清单声明了 thumb 的大图（256px 预览，离线时顶替详情大图）

硬门槛：
  1. `license.redistributable` 不为 true → **拒绝拷贝**（退出 2）。许可没签字就不能打包，不能用「先打包后补许可」。
  2. 每个拷进来的文件都按清单 sha256 校验，不符即失败（退出 3），不静默放过。
  3. 清单条数、缩略图声明数与实际拷贝数必须一致，否则失败（退出 3）。

用法：
    python tools/EllenWikiSync/sync_assets.py                 # 源默认取仓库旁的 ellen-wiki/dist
    python tools/EllenWikiSync/sync_assets.py --source D:/EllenDB/ellen-wiki
    python tools/EllenWikiSync/sync_assets.py --check         # 只比对，不写文件（0=一致，1=有漂移）
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_WIKI = REPO_ROOT.parents[0] / "ellen-wiki"
OUT_DIR = REPO_ROOT / "Installer" / "Publish" / "Resource" / "EnemyArchives" / "Web753" / "assets"
MANIFEST_NAME = "assets-manifest.json"
ORIGINALS_DIR = "orig"
THUMBS_DIR = "thumbs"


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def thumb_name(file: str) -> str:
    return "thumb-" + Path(file).stem + ".webp"


def plan(wiki: Path) -> tuple[dict, list[tuple[Path, Path, str]], list[tuple[Path, Path, str]]]:
    """返回 (manifest, [(src, dst, sha)], [(src, dst, sha)])；校验清单自身的合法性。"""
    manifest_path = wiki / "dist" / "api" / "enemies" / MANIFEST_NAME
    if not manifest_path.exists():
        raise FileNotFoundError(f"缺少 A4 清单：{manifest_path}（先跑 npm run enemy-assets && npm run build）")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("schema") != "ellen-enemy-assets-v1":
        raise ValueError(f"清单 schema 不是 ellen-enemy-assets-v1：{manifest.get('schema')}")

    license_block = manifest.get("license") or {}
    if license_block.get("redistributable") is not True:
        raise PermissionError(
            "license.redistributable != true，拒绝把图片打进安装包。\n"
            f"  approvedBy={license_block.get('approvedBy')!r} approvalSource={license_block.get('approvalSource')!r}\n"
            "  授权后改 ellen-wiki/scripts/seed/enemy-assets-license.json 并重跑 npm run enemy-assets。"
        )

    originals_root = wiki / "public" / "images" / "enemies"
    thumbs_root = wiki / "dist" / "images" / "enemies" / "_thumbs"

    originals: list[tuple[Path, Path, str]] = []
    thumbs: list[tuple[Path, Path, str]] = []
    missing: list[str] = []
    for entry in manifest.get("files") or []:
        enemy_id = entry["id"]
        file = entry["file"]
        thumb = entry.get("thumb")

        # 原图：**所有条目都随包**（含声明了缩略图的一图流）。
        # 以前带 thumb 的大图只拷 256px 预览，详情页的一图流永远看不清技能名；
        # 一图流已改成 1600px webp（单张 100~320 KB），原图随包的代价可以接受。
        src = originals_root / enemy_id / file
        dst = Path(ORIGINALS_DIR) / enemy_id / file
        if not src.exists():
            missing.append(f"{enemy_id}/{file}")
        else:
            originals.append((src, dst, entry["sha256"]))

        # 缩略图：列表/卡片与「原图缺失时降级」都用它，照旧拷。
        if thumb:
            thumb_src = thumbs_root / enemy_id / thumb["file"]
            thumb_dst = Path(THUMBS_DIR) / enemy_id / thumb["file"]
            if not thumb_src.exists():
                missing.append(f"{enemy_id}/{thumb['file']}")
                continue
            thumbs.append((thumb_src, thumb_dst, thumb["sha256"]))

    if missing:
        raise FileNotFoundError(f"{len(missing)} 个清单条目在磁盘上找不到，样例：{', '.join(missing[:5])}")

    declared_thumbs = manifest.get("thumbs", {}).get("count")
    if declared_thumbs is not None and declared_thumbs != len(thumbs):
        raise ValueError(f"清单声明缩略图 {declared_thumbs} 张，实际可拷 {len(thumbs)} 张")

    return manifest, originals, thumbs


def verify(items: list[tuple[Path, Path, str]], label: str) -> tuple[int, int]:
    """逐条校验 sha256。返回 (文件数, 字节数)；有不符直接抛错。"""
    total = 0
    bad: list[str] = []
    for src, _dst, expected in items:
        actual = sha256_file(src)
        if actual != expected:
            bad.append(f"{src}（期望 {expected[:12]}… 实际 {actual[:12]}…）")
            continue
        total += src.stat().st_size
    if bad:
        raise ValueError(f"{label} sha256 校验失败 {len(bad)} 个，样例：{'; '.join(bad[:3])}")
    return len(items), total


def drift(dest_root: Path, items: list[tuple[Path, Path, str]]) -> int:
    """--check：目标缺文件或内容不符的条目数（用 sha 比对，不看 mtime）。"""
    changed = 0
    for src, dst, expected in items:
        target = dest_root / dst
        if not target.exists() or target.stat().st_size != src.stat().st_size:
            changed += 1
            continue
        if sha256_file(target) != expected:
            changed += 1
    return changed


def update_bundle_manifest(out_root: Path, record: dict) -> None:
    """把图片包统计写进 Web753/bundle-manifest.json 的 assets 段（sync_bundle.py 会保留该段）。"""
    manifest_path = out_root / "bundle-manifest.json"
    if not manifest_path.exists():
        print(f"  [warn] 没有 {manifest_path}，跳过 bundle-manifest 更新（先跑 sync_bundle.py）", file=sys.stderr)
        return
    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    data["assets"] = record
    manifest_path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def main() -> int:
    parser = argparse.ArgumentParser(description="把 A4 敌人图片包拷进安装包（P6）。")
    parser.add_argument("--source", type=str, default=None, help="ellen-wiki 仓库根目录（默认仓库旁的 ../ellen-wiki）")
    parser.add_argument("--out", type=str, default=None, help="输出目录（默认 Web753/assets）")
    parser.add_argument("--check", action="store_true", help="只比对，不写文件")
    args = parser.parse_args()

    wiki = Path(args.source) if args.source else DEFAULT_WIKI
    out_dir = Path(args.out) if args.out else OUT_DIR
    out_root = out_dir.parent

    try:
        manifest, originals, thumbs = plan(wiki)
        orig_count, orig_bytes = verify(originals, "原图")
        thumb_count, thumb_bytes = verify(thumbs, "缩略图")
    except PermissionError as ex:
        print(f"[拒绝] {ex}", file=sys.stderr)
        return 2
    except (FileNotFoundError, ValueError) as ex:
        print(f"[失败] {ex}", file=sys.stderr)
        return 3

    license_block = manifest["license"]
    print(f"清单：{manifest['count']} 条（原图 {orig_count} / 缩略图 {thumb_count}）")
    print(f"体积：{(orig_bytes + thumb_bytes) / 1048576:.1f} MiB（原图 {orig_bytes / 1048576:.1f} + 缩略图 {thumb_bytes / 1048576:.1f}）")
    print(f"许可：redistributable={license_block['redistributable']} approvedBy={license_block.get('approvedBy')}")

    if args.check:
        changed = drift(out_dir, originals + thumbs)
        print(f"[check] 需要更新的文件 {changed} 个" + ("（一致）" if changed == 0 else "（发布前需重跑）"))
        return 1 if changed > 0 else 0

    # 清空旧图片包：窗口外的图要跟着消失，否则安装包里会堆历史文件。
    # 真机验收证据（§3 ③④ / W1-4 物理断网）由测试写进 assets 下的 acceptance.json 或 index.json，
    # 重建时保留并沿用到统计里。两份都要读并合并：acquisition 顺序固定为 index.json -> acceptance.json，
    # 后者逐键覆盖前者。为什么不能「取第一个非空就 break」：acceptance.json 可能只补了 physicalOffline，
    # 直接覆盖会让 index.json 里的 offlineColdStartThumbs/detailPlaceholder 证据凭空消失。
    acceptance: dict = {}
    if out_dir.exists():
        for name in ("index.json", "acceptance.json"):
            probe = out_dir / name
            if not probe.exists():
                continue
            try:
                prior = json.loads(probe.read_text(encoding="utf-8"))
            except json.JSONDecodeError:
                continue
            if not isinstance(prior, dict):
                continue
            block = prior.get("acceptance") if isinstance(prior.get("acceptance"), dict) else (
                prior if name == "acceptance.json" else None
            )
            if isinstance(block, dict):
                acceptance.update(block)
        shutil.rmtree(out_dir)
    (out_dir / THUMBS_DIR).mkdir(parents=True, exist_ok=True)
    (out_dir / ORIGINALS_DIR).mkdir(parents=True, exist_ok=True)

    for src, dst, _sha in originals + thumbs:
        target = out_dir / dst
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, target)

    shutil.copyfile(wiki / "dist" / "api" / "enemies" / MANIFEST_NAME, out_dir / MANIFEST_NAME)

    manifest_sha = sha256_file(out_dir / MANIFEST_NAME)
    record = {
        "manifest": MANIFEST_NAME,
        "manifestSha256": manifest_sha,
        "originals": orig_count,
        "thumbs": thumb_count,
        "bytes": orig_bytes + thumb_bytes,
        "licenseRedistributable": license_block["redistributable"],
        "approvedBy": license_block.get("approvedBy"),
        "approvalSource": license_block.get("approvalSource"),
        "attribution": license_block.get("attribution"),
        # §3 ③④ 需要真机证据：acceptance.json 里 offlineColdStartThumbs / detailPlaceholder 为 true 才算过。
        # 脚本只负责搬运与校验，绝不替测试写这两个 true。
        "acceptance": acceptance,
    }
    update_bundle_manifest(out_root, record)
    (out_dir / "index.json").write_text(
        json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n"
    )

    print(f"已写入 {out_dir}")
    print(f"  {ORIGINALS_DIR}/ {orig_count} 个 · {THUMBS_DIR}/ {thumb_count} 个 · manifest sha256={manifest_sha[:16]}…")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
