#!/usr/bin/env python3
"""生成仓库根 latest.json —— 客户端更新检查读的版本清单。

用法：
    python tools/CosDeploy/write_latest.py publish/Ellen.Launcher-7.2.0.3-Setup.exe 7.2.0.3
    python tools/CosDeploy/write_latest.py <setup.exe> <version> --notes "修了什么"

为什么要有这个文件：更新检查本来只认一个远端接口，接口一挂就没结论。
改成读静态 JSON 后，同一份内容可以放在 GitCode / COS / GitHub 三处互为备份，
客户端按顺序试，谁先回谁生效。三处的内容必须完全一致，改一处就要同步其余两处。
"""

import argparse
import datetime
import hashlib
import json
import os
import sys

GITHUB_REPO = "lihua123123/ellen.launcher"


def sha256_of(path: str) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("setup", help="安装包路径")
    parser.add_argument("version", help="版本号，例如 7.2.0.3")
    parser.add_argument("--notes", default="", help="一句话更新说明")
    parser.add_argument("--output", default="latest.json", help="输出路径（默认仓库根 latest.json）")
    args = parser.parse_args()

    if not os.path.isfile(args.setup):
        print(f"找不到安装包: {args.setup}", file=sys.stderr)
        return 1

    name = os.path.basename(args.setup)
    version = args.version.strip()
    tag = f"v{version}"

    manifest = {
        "version": version,
        "publishedAt": datetime.datetime.now().astimezone().isoformat(timespec="seconds"),
        "pageUrl": f"https://github.com/{GITHUB_REPO}/releases/tag/{tag}",
        "downloadUrl": f"https://github.com/{GITHUB_REPO}/releases/download/{tag}/{name}",
        "sha256": sha256_of(args.setup),
        "size": os.path.getsize(args.setup),
        "notes": args.notes,
    }

    with open(args.output, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(manifest, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    print(f"写入 {args.output}")
    for key, value in manifest.items():
        print(f"  {key:12s} {value}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
