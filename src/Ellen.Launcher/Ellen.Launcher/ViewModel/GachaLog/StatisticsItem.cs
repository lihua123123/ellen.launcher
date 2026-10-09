// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;

namespace Ellen.Launcher.ViewModel.GachaLog;

public sealed class StatisticsItem : Item
{
    public int Count { get; set; }

    /// <summary>
    /// 这次抽到它花了多少抽（1 起）：等于本次抽数减去上一次同品质物品的抽数。
    /// 历史列表按抽到顺序逐条展开时使用，此时 <see cref="Count"/> 恒为 1、真正展示在右上角的是抽数。
    /// </summary>
    public int PullCount { get; set; }
}