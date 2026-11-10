#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把安装包 + latest.json 发布到腾讯云 COS。

本地和 GitHub Actions 共用这一份实现，避免两套逻辑跑偏。

用法
----
    # 本地：版本号从 Installer/installer.iss 读，安装包从 publish/ 自动找最新的
    python tools/CosDeploy/publish_cos.py

    # 只看计划（算 sha256、打印将要上传的 Key），不联网
    python tools/CosDeploy/publish_cos.py --dry-run

    # CI / 指定产物
    python tools/CosDeploy/publish_cos.py --version 7.2.0.2 --exe cos-staging/x.exe --notes <release url>

    # 发布后清理旧版本，只留最近 3 个（默认只打印计划，加 --yes 才真删）
    python tools/CosDeploy/publish_cos.py --prune-keep 3
    python tools/CosDeploy/publish_cos.py --prune-keep 3 --yes

环境变量
--------
    COS_SECRET_ID    必填，子账号 API 密钥（不要用主账号密钥）
    COS_SECRET_KEY   必填
    COS_BUCKET       默认 ellen-launcher-1327452638
    COS_REGION       默认 ap-guangzhou
    COS_PUBLIC_BASE  默认 https://<bucket>.cos.<region>.myqcloud.com
                     绑了自定义域名（如 dl.ellen.wiki）就改这个，写进 latest.json 的 url 就用它
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
ISS_PATH = REPO_ROOT / "Installer" / "installer.iss"

DEFAULT_BUCKET = "ellen-launcher-1327452638"
DEFAULT_REGION = "ap-guangzhou"
DEFAULT_REPO = "lihua123123/ellen.launcher"

# 安装包永不覆盖：一年强缓存 + immutable
CACHE_IMMUTABLE = "public, max-age=31536000, immutable"
# latest.json 必须随时可拿到最新值，让 COS/CDN/浏览器都不缓存
CACHE_NO_STORE = "no-cache, no-store, must-revalidate"


# --------------------------------------------------------------------------- 输出

def _init_stdout() -> None:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
        except Exception:
            pass


def log(msg: str) -> None:
    print(msg, flush=True)


def die(msg: str, code: int = 2) -> "NoReturn":  # noqa: F821
    print(f"[失败] {msg}", file=sys.stderr, flush=True)
    raise SystemExit(code)


def human(size: int) -> str:
    value = float(size)
    for unit in ("B", "KB", "MB", "GB"):
        if value < 1024 or unit == "GB":
            return f"{value:.1f} {unit}" if unit != "B" else f"{int(value)} B"
        value /= 1024
    return f"{size} B"


# --------------------------------------------------------------------------- 本地信息

def read_version_from_iss() -> str:
    if not ISS_PATH.is_file():
        die(f"找不到 {ISS_PATH}，请用 --version 指定版本号")
    text = ISS_PATH.read_text(encoding="utf-8", errors="ignore")
    match = re.search(r'#define\s+MyAppVersion\s+"([^"]+)"', text)
    if not match:
        die("installer.iss 里没有 #define MyAppVersion")
    return match.group(1).strip()


def find_installer(version: str, explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit).expanduser().resolve()
        if not path.is_file():
            die(f"指定的安装包不存在：{path}")
        return path

    exact = REPO_ROOT / "publish" / f"Ellen.Launcher-{version}-Setup.exe"
    if exact.is_file():
        return exact

    candidates = sorted(
        (REPO_ROOT / "publish").glob("*-Setup.exe"),
        key=lambda p: p.stat().st_mtime,
        reverse=True,
    )
    if not candidates:
        die("publish/ 下没有找到 *-Setup.exe，请先出包或用 --exe 指定")
    log(f"[提示] 未找到版本号精确匹配的安装包，改用最新的 {candidates[0].name}")
    return candidates[0]


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    total = path.stat().st_size
    read = 0
    started = time.time()
    with path.open("rb") as fp:
        while True:
            chunk = fp.read(4 * 1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
            read += len(chunk)
            if total:
                pct = read * 100 // total
                if pct // 20 and pct % 20 == 0:
                    print(f"\r  校验中 {pct:3d}%  {human(read)}/{human(total)}", end="", flush=True)
    print(f"\r  校验完成 {human(total)}，用时 {time.time() - started:.1f}s" + " " * 20)
    return digest.hexdigest()


def iso_utc(ts: float) -> str:
    return datetime.fromtimestamp(ts, timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


# --------------------------------------------------------------------------- COS

class Cos:
    def __init__(self, bucket: str, region: str, secret_id: str, secret_key: str) -> None:
        from qcloud_cos import CosConfig, CosS3Client  # 延迟导入，--dry-run 不需要

        config = CosConfig(
            Region=region,
            SecretId=secret_id,
            SecretKey=secret_key,
            Scheme="https",
            Token=None,
        )
        self.bucket = bucket
        self.client = CosS3Client(config)

    def put_bytes(self, key: str, body: bytes, content_type: str, cache_control: str) -> None:
        self.client.put_object(
            Bucket=self.bucket,
            Key=key,
            Body=body,
            ContentType=content_type,
            CacheControl=cache_control,
        )

    def upload_file(self, key: str, path: Path) -> None:
        self.client.upload_file(
            Bucket=self.bucket,
            Key=key,
            LocalFilePath=str(path),
            PartSize=10,
            MAXThread=4,
            ContentType="application/octet-stream",
            CacheControl=CACHE_IMMUTABLE,
        )

    def list_release_prefixes(self) -> list[str]:
        prefixes: list[str] = []
        marker = ""
        while True:
            kwargs = {"Bucket": self.bucket, "Prefix": "releases/", "Delimiter": "/"}
            if marker:
                kwargs["Marker"] = marker
            resp = self.client.list_objects(**kwargs)
            prefixes.extend(item["Prefix"] for item in resp.get("CommonPrefixes") or [])
            if str(resp.get("IsTruncated", "false")).lower() != "true":
                return prefixes
            marker = resp.get("NextMarker", "")
            if not marker:
                return prefixes

    def delete_prefix(self, prefix: str) -> int:
        keys: list[str] = []
        marker = ""
        while True:
            kwargs = {"Bucket": self.bucket, "Prefix": prefix}
            if marker:
                kwargs["Marker"] = marker
            resp = self.client.list_objects(**kwargs)
            keys.extend(item["Key"] for item in resp.get("Contents") or [])
            if str(resp.get("IsTruncated", "false")).lower() != "true":
                break
            marker = resp.get("NextMarker", "")
            if not marker:
                break
        if not keys:
            return 0
        for start in range(0, len(keys), 1000):
            batch = keys[start:start + 1000]
            self.client.delete_objects(
                Bucket=self.bucket,
                Delete={"Object": [{"Key": k} for k in batch], "Quiet": True},
            )
        return len(keys)


# --------------------------------------------------------------------------- 校验

def _opener() -> urllib.request.OpenerDirector:
    # 本机常年挂着 HTTP(S)_PROXY，访问国内桶必须绕开，否则实测会 20s 超时
    return urllib.request.build_opener(urllib.request.ProxyHandler({}))


def http_get(url: str, timeout: int = 25) -> tuple[int, bytes]:
    request = urllib.request.Request(url, headers={"Cache-Control": "no-cache", "Pragma": "no-cache"})
    try:
        with _opener().open(request, timeout=timeout) as resp:
            return resp.status, resp.read()
    except urllib.error.HTTPError as exc:
        return exc.code, exc.read()


def http_head(url: str, timeout: int = 25) -> tuple[int, dict[str, str]]:
    request = urllib.request.Request(url, method="HEAD")
    try:
        with _opener().open(request, timeout=timeout) as resp:
            return resp.status, {k.lower(): v for k, v in resp.headers.items()}
    except urllib.error.HTTPError as exc:
        return exc.code, {k.lower(): v for k, v in exc.headers.items()}


def verify(base: str, version: str, key: str, sha256: str, size: int) -> bool:
    ok = True

    stamp = int(time.time())
    status, body = http_get(f"{base}/latest.json?t={stamp}")
    if status != 200:
        log(f"[校验失败] latest.json HTTP {status}")
        ok = False
    else:
        try:
            doc = json.loads(body.decode("utf-8"))
        except Exception as exc:
            log(f"[校验失败] latest.json 不是合法 JSON：{exc}")
            return False
        for field, expect in (("version", version), ("sha256", sha256)):
            got = doc.get(field)
            if got != expect:
                log(f"[校验失败] latest.json.{field} = {got!r}，期望 {expect!r}")
                ok = False
        if (doc.get("installer") or {}).get("size") != size:
            log(f"[校验失败] latest.json.installer.size 不一致")
            ok = False
        log(f"[校验] latest.json -> v{doc.get('version')} ({human(int((doc.get('installer') or {}).get('size') or 0))})")

    status, headers = http_head(f"{base}/{key}")
    if status != 200:
        log(f"[校验失败] 安装包 HEAD HTTP {status}")
        ok = False
    else:
        remote = int(headers.get("content-length") or -1)
        cache = headers.get("cache-control", "")
        if remote != size:
            log(f"[校验失败] 远程序号字节数 {remote} != 本地 {size}")
            ok = False
        if "immutable" not in cache:
            log(f"[警告] 安装包 Cache-Control 未生效：{cache!r}")
        log(f"[校验] 安装包 {human(remote)}，Cache-Control: {cache or '(空)'}")

    return ok


# --------------------------------------------------------------------------- 主流程

def main() -> int:
    _init_stdout()

    parser = argparse.ArgumentParser(description="发布安装包与 latest.json 到腾讯云 COS")
    parser.add_argument("--version", help="版本号 x.y.z.w，默认读 Installer/installer.iss")
    parser.add_argument("--exe", help="安装包路径，默认 publish/Ellen.Launcher-<version>-Setup.exe")
    parser.add_argument("--channel", default="stable", help="发布通道，默认 stable")
    parser.add_argument("--notes", help="更新说明链接，默认指向 GitHub Release 页")
    parser.add_argument("--github-url", help="GitHub 下载直链，写进 mirrors")
    parser.add_argument("--status", action="store_true", help="只查看桶里已发布的版本与线上 latest.json")
    parser.add_argument("--dry-run", action="store_true", help="只打印计划，不联网")
    parser.add_argument("--no-verify", action="store_true", help="跳过上传后的线上校验")
    parser.add_argument("--prune-keep", type=int, default=0, metavar="N", help="只保留最近 N 个版本（默认只打印，不清）")
    parser.add_argument("--yes", action="store_true", help="配合 --prune-keep 真正执行删除")
    args = parser.parse_args()

    bucket = os.environ.get("COS_BUCKET", DEFAULT_BUCKET)
    region = os.environ.get("COS_REGION", DEFAULT_REGION)
    base = os.environ.get("COS_PUBLIC_BASE", f"https://{bucket}.cos.{region}.myqcloud.com").rstrip("/")
    secret_id = os.environ.get("COS_SECRET_ID", "").strip()
    secret_key = os.environ.get("COS_SECRET_KEY", "").strip()

    if args.status:
        # 看线上 latest.json 不需要密钥；列举桶内版本才需要
        if secret_id and secret_key:
            try:
                names = sorted(p.strip("/").split("/")[-1] for p in Cos(bucket, region, secret_id, secret_key).list_release_prefixes())
                log(f"桶内已发布版本（{len(names)}）：{'、'.join(names) if names else '(空)'}")
            except Exception as exc:
                log(f"[警告] 列举桶内版本失败：{exc}")
        else:
            log("（未配置密钥，跳过桶内版本列举）")
        log("")
        status, body = http_get(f"{base}/latest.json?t={int(time.time())}")
        if status == 200:
            log("线上 latest.json：")
            log(body.decode("utf-8", errors="replace"))
        else:
            log(f"线上 latest.json 尚不存在（HTTP {status}），第一次发布后就有了")
        return 0

    version = (args.version or read_version_from_iss()).strip().lstrip("v")
    if not re.fullmatch(r"\d+\.\d+\.\d+(\.\d+)?", version):
        die(f"版本号格式不对：{version!r}（应形如 7.2.0.2）")

    installer = find_installer(version, args.exe)
    size = installer.stat().st_size
    key = f"releases/{version}/{installer.name}"
    url = f"{base}/{key}"
    notes = args.notes or f"https://github.com/{DEFAULT_REPO}/releases/tag/v{version}"
    github_url = args.github_url or f"https://github.com/{DEFAULT_REPO}/releases/download/v{version}/{installer.name}"

    log(f"版本      {version}  ({args.channel})")
    log(f"安装包    {installer}")
    log(f"大小      {human(size)}")
    log(f"目标      cos://{bucket}/{key}")
    log(f"公网地址  {url}")
    log("")

    log("计算 sha256 …")
    sha256 = sha256_of(installer)
    log(f"sha256    {sha256}")
    log("")

    doc = {
        "schema": 1,
        "channel": args.channel,
        "version": version,
        "tag": f"v{version}",
        "publishedAt": iso_utc(installer.stat().st_mtime),
        "generatedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "sha256": sha256,
        "installer": {
            "name": installer.name,
            "size": size,
            "sha256": sha256,
            "url": url,
        },
        "mirrors": [url, github_url],
        "notes": notes,
    }
    payload = json.dumps(doc, ensure_ascii=False, indent=2).encode("utf-8")

    if args.dry_run:
        log("[dry-run] 将要上传：")
        log(f"  PUT cos://{bucket}/{key}   ({human(size)}, {CACHE_IMMUTABLE})")
        log(f"  PUT cos://{bucket}/{key.rsplit('/', 1)[0]}/sha256.txt")
        log(f"  PUT cos://{bucket}/latest.json   ({CACHE_NO_STORE})")
        log("")
        log("[dry-run] latest.json 内容：")
        log(json.dumps(doc, ensure_ascii=False, indent=2))
        return 0

    if not secret_id or not secret_key:
        die("缺少 COS_SECRET_ID / COS_SECRET_KEY 环境变量（子账号 API 密钥）")

    cos = Cos(bucket, region, secret_id, secret_key)

    log("上传安装包 …")
    started = time.time()
    try:
        cos.upload_file(key, installer)
    except Exception as exc:
        die(f"安装包上传失败：{exc}", 3)
    log(f"完成，用时 {time.time() - started:.1f}s（断点续传可用，失败直接重跑即可）")

    cos.put_bytes(f"releases/{version}/sha256.txt", (sha256 + "  " + installer.name + "\n").encode("utf-8"),
                  "text/plain; charset=utf-8", CACHE_IMMUTABLE)

    log("上传 latest.json …")
    cos.put_bytes("latest.json", payload, "application/json; charset=utf-8", CACHE_NO_STORE)

    if not args.no_verify:
        log("")
        log("线上校验 …")
        if not verify(base, version, key, sha256, size):
            die("上传完成但校验未通过，先别发公告", 4)

    if args.prune_keep > 0:
        log("")
        log(f"清理旧版本（保留最近 {args.prune_keep} 个）…")
        prefixes = cos.list_release_prefixes()

        def sort_key(prefix: str) -> tuple:
            name = prefix.strip("/").split("/")[-1]
            try:
                return tuple(int(x) for x in name.split("."))
            except ValueError:
                return (0,)

        ordered = sorted(prefixes, key=sort_key, reverse=True)
        doomed = [p for p in ordered if p != f"releases/{version}/"][max(0, args.prune_keep - 1):]
        if not doomed:
            log("没有需要清理的版本")
        else:
            for prefix in doomed:
                log(f"  {'删除' if args.yes else '待删'} {prefix}")
            if args.yes:
                total = sum(cos.delete_prefix(p) for p in doomed)
                log(f"已删除 {total} 个对象")
            else:
                log("（加 --yes 才会真删；也可以直接用 COS 生命周期规则）")

    log("")
    log(f"PUBLISHED version={version} sha256={sha256} size={size} url={url}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
