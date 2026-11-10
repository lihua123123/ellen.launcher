"""把编译输出（bin）同步到 Installer/Publish —— 只更新程序文件，不碰 Resource/。

为什么需要：WinUI 的 XAML 编译产物是 112 个 `.xbf`，它们**必须和 `Ellen.Launcher.dll` 同版本**。
只拷 dll 不拷 xbf，会出现导航到页面时
`InvalidCastException: Unable to cast 'WinRT.IInspectable' to 'LayoutSwitch'`
（连接器按新 g.cs 逐个 x:Name 赋值，页面却还是旧 XBF 的元素树）。

用法（不出包、只想在 Publish 里查验时）：
    python tools/EllenWikiSync/sync_bin_to_publish.py
    python tools/EllenWikiSync/sync_bin_to_publish.py --check   # 只比对
"""
from __future__ import annotations

import argparse
import filecmp
import shutil
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
BIN = REPO_ROOT / "src" / "Ellen.Launcher" / "Ellen.Launcher" / "bin" / "Release" / "net10.0-windows10.0.26100.0" / "win-x64"
PUBLISH = REPO_ROOT / "Installer" / "Publish"

# 这些是运行时资源，由 sync_bundle.py / sync_assets.py 单独维护，绝不能从 bin 覆盖。
SKIP_DIRS = {"Resource"}
SKIP_SUFFIX = {".pdb", ".log", ".xbf.bak"}


def iter_files(root: Path):
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        rel = path.relative_to(root)
        if rel.parts and rel.parts[0] in SKIP_DIRS:
            continue
        if path.suffix.lower() in SKIP_SUFFIX:
            continue
        yield path, rel


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="只比对，不写文件")
    args = parser.parse_args()

    if not BIN.is_dir():
        print(f"[失败] 找不到编译输出：{BIN}（先 dotnet build）", file=sys.stderr)
        return 3

    copied = 0
    same = 0
    for src, rel in iter_files(BIN):
        dst = PUBLISH / rel
        if dst.exists() and filecmp.cmp(src, dst, shallow=False):
            same += 1
            continue
        copied += 1
        if args.check:
            print(f"  差异 {rel}")
            continue
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)

    verb = "需要更新" if args.check else "已同步"
    print(f"{verb} {copied} 个文件（一致 {same} 个）→ {PUBLISH}")
    return 0 if copied == 0 or not args.check else 1


if __name__ == "__main__":
    raise SystemExit(main())
