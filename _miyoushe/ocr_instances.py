# -*- coding: utf-8 -*-
"""Umi-OCR 多实例管理（提速用）。

背景：单个 Umi-OCR 实例内部对请求是串行的（实测同端口并发 4 次 = 4 次串行，加速比 ×0.99），
      所以提速必须多开进程。做法 = 复制程序目录 + 改各自 `.pre_settings` 的 `server_port`。

用法：
    python ocr_instances.py status     # 查看各端口是否可用
    python ocr_instances.py start      # 复制（缺则复制）并启动 1224~1227
    python ocr_instances.py stop       # 停掉全部 Umi-OCR 进程（保留磁盘副本，便于下次秒起）
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


def alive(port, timeout=3):
    try:
        op = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        req = urllib.request.Request(f"http://127.0.0.1:{port}/api/ocr", data=b'{"base64":""}',
                                     headers={"Content-Type": "application/json"})
        op.open(req, timeout=timeout)
        return True
    except Exception:
        return False


def cmd_status():
    for p in PORTS:
        print(f"  port {p}: {'UP' if alive(p) else 'down'}")


def cmd_stop():
    subprocess.run(["powershell", "-NoProfile", "-Command",
                    "Get-Process Umi-OCR -ErrorAction SilentlyContinue | "
                    "ForEach-Object { taskkill /F /PID $_.Id }"],
                   capture_output=True, text=True, errors="replace", timeout=180)
    time.sleep(2)
    print("stopped")


def cmd_start():
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
            subprocess.Popen([str(dst / "Umi-OCR.exe"), "--hide"], cwd=str(dst))
            for _ in range(6):
                time.sleep(4)
                if alive(port, 3):
                    break
        print(f"  port {port}: {'UP' if alive(port) else 'FAILED'}")


if __name__ == "__main__":
    action = sys.argv[1] if len(sys.argv) > 1 else "status"
    {"status": cmd_status, "start": cmd_start, "stop": cmd_stop}[action]()
