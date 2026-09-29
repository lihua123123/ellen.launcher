# -*- coding: utf-8 -*-
"""通过 Umi-OCR 的 HTTP API（POST /api/ocr）逐张识别图鉴卡。

为什么不用命令行：Umi-OCR.exe 的 CLI 本质是个 HTTP 客户端（POST /argv），
它会把返回结果 print 到 GBK 控制台 → 文本里有 U+2027 之类字符就 UnicodeEncodeError；
而且它是「先 print 再写 --output」，崩了就什么都不落盘（看起来却像成功）。

这里直接调 API：一张图一个 txt，边界清晰，也不受控制台编码影响。
"""
import base64
import json
import time
import urllib.request
from pathlib import Path

ROOT = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\_miyoushe\miyoushe_3572896")
OUT = Path(r"D:\艾莲的数据库\workspace\ellen-launcher\_miyoushe\_ocr")
API = "http://127.0.0.1:1224/api/ocr"

# 不传这个的话，data 回的是识别块列表（不是纯文本）
OPTIONS = {"data.format": "text"}

# 不走系统代理（本机 127.0.0.1:7890）
OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def to_text(data) -> str:
    """data 可能是 str（data.format=text）或识别块列表，统一成纯文本。"""
    if isinstance(data, str):
        return data
    if isinstance(data, list):
        parts = []
        for item in data:
            if isinstance(item, dict):
                parts.append(str(item.get("text", "")) + str(item.get("end", "\n")))
            else:
                parts.append(str(item) + "\n")
        return "".join(parts)
    return ""


def ocr_one(path: Path) -> str:
    payload = {
        "base64": base64.b64encode(path.read_bytes()).decode("ascii"),
        "options": OPTIONS,
    }
    req = urllib.request.Request(
        API,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )
    with OPENER.open(req, timeout=300) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    if data.get("code") != 100:
        raise RuntimeError(f"code={data.get('code')} data={str(data.get('data'))[:120]}")
    return to_text(data.get("data"))


def main() -> None:
    folders = sorted(d for d in ROOT.iterdir() if d.is_dir())
    total = ok = fail = 0
    started = time.time()
    for folder in folders:
        tag = folder.name[:2]
        images = sorted(folder.glob("*.png"))
        outdir = OUT / tag
        outdir.mkdir(parents=True, exist_ok=True)
        for img in images:
            target = outdir / (img.stem + ".txt")
            if target.exists() and target.stat().st_size > 0:
                total += 1
                ok += 1
                continue
            total += 1
            try:
                target.write_text(ocr_one(img), encoding="utf-8")
                ok += 1
            except Exception as ex:  # noqa: BLE001
                fail += 1
                print(f"FAIL {tag}/{img.name}: {ex}", flush=True)
            if total % 25 == 0:
                print(f"progress {total} ok={ok} fail={fail} {time.time() - started:.0f}s", flush=True)
        print(f"folder {tag} done ({len(images)} imgs)", flush=True)
    print(f"=== ALL DONE total={total} ok={ok} fail={fail} {time.time() - started:.0f}s ===")


if __name__ == "__main__":
    main()
