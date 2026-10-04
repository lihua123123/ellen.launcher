# -*- coding: utf-8 -*-
"""ellen.wiki 只读数据 API 的同步工具。

接口说明见 https://ellen.wiki/#/developer 。
"""

from .api import EllenWikiApi, EllenWikiError
from .datasets import DATASETS, Dataset, select_datasets

__all__ = [
    "EllenWikiApi",
    "EllenWikiError",
    "DATASETS",
    "Dataset",
    "select_datasets",
]
