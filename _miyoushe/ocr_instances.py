# -*- coding: utf-8 -*-
"""Umi-OCR 多实例管理（提速用）。

背景：单个 Umi-OCR 实例内部对请求是串行的（实测同端口并发 4 次 = 4 次串行，加速比 ×0.99），
      所以提速必须多开进程。做法 = 复制程序目录 + 改各自 `.pre_settings` 的 `server_port`。

用法：
    python ocr_instances.py status     # 查看各端口是否可用
    python ocr_instances.py start      # 复制（缺则复制）并启动 1224~1227
    python ocr_instances.py stop       # 停掉全部 Umi-OCR 进程（保留磁盘副本，便于下次秒起）

注意（曾误判过）：
    start 时若看到「Umi-OCR 已在运行 / HTTP跨进程传输指令失败 / WinError 10061」，
    那是 Umi-OCR **自身的单实例转交竞态**（把指令 POST 给自己的 /argv，而服务还没监听完），
    非致命；现已把子进程 stdout 重定向到 `_gen2/ocr_logs/<port>.log`，不再污染控制台。
    **判断成功与否只看最后一行 `-> N/4 ready`**，别被中途输出带偏。
"""
import json
import shutil
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

BASE = Path(r"D:\uim-ocr")
SRC = BASE / "Umi-OCR_Paddle_v2.1.5"
PORTS = [1224, 1225, 1226, 1227]
LOG_DIR = Path(__file__).parent / "_gen2" / "ocr_logs"


def probe(port, timeout=6):
    """真实探活：发一次 OCR 请求，要求返回**带 code 字段的 JSON**才算就绪。

    不能只看「有没有 HTTP 响应」——启动瞬间服务可能处于半死状态；
    返回 (是否就绪, 原因)。
    """
    try:
        op = urllib.request.build_opener(urllib.request.ProxyHandler({}))  # 绕开系统代理
        req = urllib.request.Request(
            f"http://127.0.0.1:{port}/api/ocr", data=b'{"base64":""}',
            headers={"Content-Type": "application/json"})
        with op.open(req, timeout=timeout) as r:
            d = json.loads(r.read().decode("utf-8", "replace"))
        return ("code" in d, "api ok (code=%s)" % d.get("code"))
    except Exception as e:
        return False, type(e).__name__


def alive(port, timeout=3):
    return probe(port, timeout)[0]


def cmd_status():
    ok = 0
    for p in PORTS:
        good, why = probe(p)
        ok += good
        print(f"  port {p}: {'UP  ' if good else 'down'}  {why}")
    print(f"  -> {ok}/{len(PORTS)} ready")
    return ok


def cmd_stop():
    subprocess.run(["powershell", "-NoProfile", "-Command",
                    "Get-Process Umi-OCR -ErrorAction SilentlyContinue | "
                    "ForEach-Object { taskkill /F /PID $_.Id }"],
                   capture_output=True, text=True, errors="replace", timeout=180)
    time.sleep(2)
    print("stopped")


def cmd_start():
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    for i, port in enumerate(PORTS):
        dst = SRC if i == 0 else BASE / f"Umi-OCR_i{i + 1}"
        if not dst.exists():
            print(f"copying -> {dst.name} ...")
            shutil.copytree(SRC, dst)
        pf = dst / "UmiOCR-data" / ".pre_settings"
        cfg = json.loads(pf.read_text(encoding="utf-8", errors="ignore"))
        cfg["server_port"], cfg["last_pid"] = port, -1
        pf.write_text(json.dumps(cfg, ensure_ascii=False, indent=2), encoding="utf-8")
        if not alive(port, 2):
            # ⚠️ 子进程 stdout 必须重定向：Umi-OCR 启动时会打印
            # 「Umi-OCR 已在运行，HTTP跨进程传输指令失败 / urlopen error WinError 10061」。
            # 那是它**自己的单实例转交竞态**（试图把指令 POST 给自己的 /argv，而服务还没监听完），
            # 非致命，进程随后照常起来 —— 但直接透传到控制台会让人以为启动失败了。
            with open(LOG_DIR / f"{port}.log", "ab") as log:
                subprocess.Popen([str(dst / "Umi-OCR.exe"), "--hide"], cwd=str(dst),
                                 stdout=log, stderr=subprocess.STDOUT,
                                 stdin=subprocess.DEVNULL)
            for _ in range(15):
                time.sleep(2)
                if alive(port, 3):
                    break
    ok = cmd_status()
    print(f"  启动日志: {LOG_DIR}")
    return ok


if __name__ == "__main__":
    action = sys.argv[1] if len(sys.argv) > 1 else "status"
    rc = {"status": cmd_status, "start": cmd_start, "stop": cmd_stop}[action]()
    # 未全部就绪时返回非 0，便于上游脚本 / 流水线直接判断
    if action in ("status", "start") and rc != len(PORTS):
        sys.exit(1)
