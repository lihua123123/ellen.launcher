# -*- coding: utf-8 -*-
"""Umi-OCR 多实例端口池。

背景（实测）：
  - 单个 Umi-OCR 实例内部对请求是**串行**的：同一端口并发 4 次 = 4 次串行（加速比 ×0.99）；
  - 因此提速必须**多开进程**，每个实例监听不同端口（复制程序目录 + 改 .pre_settings 的 server_port）；
  - 实测 4 实例（1224~1227）可同时服务，启动约 5~12 秒。

本模块提供：端口探测、按端口调用、轮询分发（线程安全）。
"""
import base64
import itertools
import json
import threading
import urllib.request

CANDIDATE_PORTS = [1224, 1225, 1226, 1227, 1228, 1229, 1230, 1231]
_LOCK = threading.Lock()
_cycle = itertools.cycle([1224])
PORTS: list[int] = []


def _open(url, body, timeout):
    op = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json"})
    with op.open(req, timeout=timeout) as r:
        return json.loads(r.read().decode("utf-8"))


def alive(port, timeout=4):
    try:
        _open(f"http://127.0.0.1:{port}/api/ocr", b'{"base64":""}', timeout)
        return True
    except Exception:
        return False


def refresh():
    """探测可用端口并设置为轮询池。"""
    global PORTS, _cycle
    PORTS = [p for p in CANDIDATE_PORTS if alive(p)]
    if not PORTS:
        PORTS = [1224]
    _cycle = itertools.cycle(PORTS)
    return PORTS


def next_port():
    with _LOCK:
        return next(_cycle)


def ocr(image_bytes: bytes, fmt: str = "text", port: int | None = None) -> object:
    """调用 OCR；空图（code=101）返回空串。"""
    p = port or next_port()
    body = json.dumps({"base64": base64.b64encode(image_bytes).decode("ascii"),
                       "options": {"data.format": fmt}}).encode()
    try:
        res = _open(f"http://127.0.0.1:{p}/api/ocr", body, timeout=300)
    except Exception:
        # 该端口不可用 → 换一个重试一次
        with _LOCK:
            alt = next(_cycle)
        res = _open(f"http://127.0.0.1:{alt}/api/ocr", body, timeout=300)
    if res.get("code") != 100:
        return "" if res.get("code") == 101 else (_ for _ in ()).throw(
            RuntimeError(f"code={res.get('code')} {str(res.get('data'))[:80]}"))
    return res["data"]
