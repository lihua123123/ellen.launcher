# -*- coding: utf-8 -*-
"""
米游社合集图片下载器（原图）

用法:
    python miyoushe_download.py                      # 默认合集 3572896 (原神 原魔图鉴存放合集)
    python miyoushe_download.py <collection_id> [--gids 2] [--out DIR]
    python miyoushe_download.py --post 72409634 72409897   # 直接指定帖子

依赖: 无（仅标准库）
"""
import argparse
import json
import os
import re
import time
import urllib.error
import urllib.request

UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36")
HEADERS = {
    "User-Agent": UA,
    "Referer": "https://www.miyoushe.com/",
    "Accept": "application/json, text/plain, */*",
}

API = "https://bbs-api.miyoushe.com"


def http_get_json(url):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=25) as resp:
        return json.loads(resp.read().decode("utf-8", "replace"))


def sanitize(name, limit=80):
    name = re.sub(r'[\\/:*?"<>|\r\n\t]+', "_", name or "").strip(" .")
    return (name[:limit] or "untitled")


def get_collection_posts(collection_id, gids):
    """返回合集内全部帖子 [{post_id, subject}]，按合集顺序。"""
    posts = []
    seen = set()
    url = (f"{API}/post/wapi/getPostFullInCollection"
           f"?collection_id={collection_id}&gids={gids}&order_type=1")
    data = http_get_json(url)
    if data.get("retcode") != 0:
        raise RuntimeError(f"合集接口异常 retcode={data.get('retcode')} msg={data.get('message')}")

    for item in (data.get("data") or {}).get("posts") or []:
        p = item.get("post") or {}
        pid = p.get("post_id")
        if not pid:
            continue
        s = str(pid)
        if s not in seen:
            seen.add(s)
            posts.append({"post_id": s, "subject": p.get("subject") or ""})
    return posts


def get_post_images(post_id):
    """返回 (subject, [原图url...])。优先用 image_list。"""
    data = http_get_json(f"{API}/post/wapi/getPostFull?post_id={post_id}&read=1")
    if data.get("retcode") != 0:
        return None, []
    wrap = (data.get("data") or {}).get("post") or {}
    inner = wrap.get("post") or {}
    subject = inner.get("subject") or ""
    urls = []
    for it in (wrap.get("image_list") or []):
        u = it.get("url") if isinstance(it, dict) else it
        if not u:
            continue
        u = u.split("?")[0]          # 去掉 x-oss-process 缩放参数，取原图
        if u not in urls:
            urls.append(u)
    return subject, urls


def download(url, path, retries=3):
    for attempt in range(1, retries + 1):
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=60) as resp:
                if resp.status == 200:
                    with open(path, "wb") as f:
                        while True:
                            chunk = resp.read(1 << 16)
                            if not chunk:
                                break
                            f.write(chunk)
                    return True
                print(f"    [重试{attempt}] HTTP {resp.status}")
        except Exception as e:  # noqa
            print(f"    [重试{attempt}] {e}")
        time.sleep(2)
    return False


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("collection_id", nargs="?", default="3572896")
    ap.add_argument("--gids", default="2", help="游戏ID: 原神=2")
    ap.add_argument("--out", default=None, help="输出根目录")
    ap.add_argument("--post", nargs="*", help="直接指定 post_id 列表（覆盖合集）")
    args = ap.parse_args()

    out_root = args.out or f"miyoushe_{args.collection_id}"
    os.makedirs(out_root, exist_ok=True)

    if args.post:
        posts = [{"post_id": p, "subject": ""} for p in args.post]
    else:
        print(f"== 获取合集 {args.collection_id} 帖子列表 ==")
        posts = get_collection_posts(args.collection_id, args.gids)
    print(f"共 {len(posts)} 个帖子")

    manifest = []
    total_ok = total_fail = 0
    for i, p in enumerate(posts, 1):
        pid = p["post_id"]
        subject, urls = get_post_images(pid)
        subject = subject or p.get("subject") or f"post_{pid}"
        print(f"\n[{i}/{len(posts)}] {subject} (post_id={pid})")
        if not urls:
            print("  未获取到图片（可能已删除或接口异常）")
            manifest.append({"index": i, "post_id": pid, "subject": subject, "images": []})
            continue

        folder = os.path.join(out_root, f"{i:02d}_{sanitize(subject)}")
        os.makedirs(folder, exist_ok=True)
        print(f"  {len(urls)} 张图片 -> {folder}")
        saved = []
        for j, url in enumerate(urls, 1):
            ext = os.path.splitext(url)[1].lower() or ".png"
            path = os.path.join(folder, f"{j:03d}{ext}")
            if os.path.exists(path) and os.path.getsize(path) > 0:
                total_ok += 1
                saved.append(os.path.basename(path))
                continue
            ok = download(url, path)
            print(f"    {'OK  ' if ok else 'FAIL'} {j:03d}{ext}")
            if ok:
                total_ok += 1
                saved.append(os.path.basename(path))
            else:
                total_fail += 1
            time.sleep(0.35)
        manifest.append({"index": i, "post_id": pid, "subject": subject,
                         "folder": os.path.basename(folder),
                         "images": urls})

    with open(os.path.join(out_root, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    print(f"\n完成：成功 {total_ok} 张，失败 {total_fail} 张。目录：{out_root}\\")
    print("清单已写入 manifest.json")


if __name__ == "__main__":
    main()
