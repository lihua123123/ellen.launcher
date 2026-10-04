# -*- coding: utf-8 -*-
"""ellen.wiki 只读 JSON API 客户端。

只依赖标准库。原始响应按路径缓存到本地，避免重复全量拉取
（站点约定 ``Cache-Control: public, max-age=300``）。
"""

from __future__ import annotations

import json
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

DEFAULT_BASE_URL = "https://ellen.wiki"
DEFAULT_CACHE_TTL = 300  # 与站点 Cache-Control: max-age 保持一致
USER_AGENT = "EllenLauncher-EllenWikiSync/1.0 (+https://github.com/lihua123123/ellenwiki)"


class EllenWikiError(RuntimeError):
    """请求或解析 ellen.wiki 数据失败。"""


class EllenWikiApi:
    """带磁盘缓存的 ellen.wiki 数据客户端。

    缓存命中且未过期时不会发起网络请求；``offline=True`` 时只用缓存，
    缓存不存在即报错。
    """

    def __init__(
        self,
        base_url: str = DEFAULT_BASE_URL,
        cache_dir: Path | None = None,
        *,
        force: bool = False,
        offline: bool = False,
        ttl: int = DEFAULT_CACHE_TTL,
        timeout: float = 30.0,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.cache_dir = cache_dir
        self.force = force
        self.offline = offline
        self.ttl = ttl
        self.timeout = timeout

        if cache_dir is not None:
            cache_dir.mkdir(parents=True, exist_ok=True)

    # -- public -----------------------------------------------------------

    def manifest(self) -> dict[str, Any]:
        """``GET /api``：数据集清单、条目数量与新鲜度。"""
        return self.get_json("/api")

    def get_text(self, path: str) -> str:
        """取原始响应文本（用于 ``/api/attachment`` 这类单文件数据集）。"""
        return self._get(path)[1]

    def get_json(self, path: str) -> Any:
        """取 JSON 响应。"""
        text = self.get_text(path)
        try:
            return json.loads(text)
        except json.JSONDecodeError as ex:
            raise EllenWikiError(f"响应不是合法 JSON（{path}）：{ex}") from ex

    # -- internals --------------------------------------------------------

    def _get(self, path: str) -> tuple[dict[str, str], str]:
        """返回 (响应头, 文本)。优先使用未过期的本地缓存。"""
        cache_name = self._cache_name(path)
        cached = self._read_cache(cache_name)

        if cached is not None and not self.force:
            age = time.time() - cached["fetchedAt"]
            if age < self.ttl:
                return cached["headers"], cached["text"]

        if self.offline:
            if cached is not None:
                return cached["headers"], cached["text"]
            raise EllenWikiError(f"离线模式下没有缓存：{path}")

        url = f"{self.base_url}{path}"
        request = urllib.request.Request(
            url,
            headers={
                "User-Agent": USER_AGENT,
                "Accept": "application/json",
            },
        )

        # 条件请求：本地已有缓存时带上校验字，命中 304 可省流量。
        if cached is not None:
            etag = cached["headers"].get("etag")
            last_modified = cached["headers"].get("last-modified")
            if etag:
                request.add_header("If-None-Match", etag)
            if last_modified:
                request.add_header("If-Modified-Since", last_modified)

        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                text = response.read().decode("utf-8")
                headers = {k.lower(): v for k, v in response.headers.items()}
        except urllib.error.HTTPError as ex:
            if ex.code == 304 and cached is not None:
                self._write_cache(cache_name, cached["headers"], cached["text"])
                return cached["headers"], cached["text"]
            raise EllenWikiError(f"请求失败 {url}：HTTP {ex.code}") from ex
        except urllib.error.URLError as ex:
            if cached is not None:
                return cached["headers"], cached["text"]
            raise EllenWikiError(f"请求失败 {url}：{ex.reason}") from ex

        self._write_cache(cache_name, headers, text)
        return headers, text

    def _cache_name(self, path: str) -> str:
        safe = path.strip("/").replace("/", "_") or "root"
        return f"{safe}.json"

    def _read_cache(self, cache_name: str) -> dict[str, Any] | None:
        if self.cache_dir is None:
            return None

        file = self.cache_dir / cache_name
        if not file.exists():
            return None

        try:
            data = json.loads(file.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return None

        if not isinstance(data, dict) or "text" not in data or "fetchedAt" not in data:
            return None
        data.setdefault("headers", {})
        return data

    def _write_cache(self, cache_name: str, headers: dict[str, str], text: str) -> None:
        if self.cache_dir is None:
            return

        payload = {
            "fetchedAt": time.time(),
            "headers": headers,
            "text": text,
        }
        file = self.cache_dir / cache_name
        file.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
