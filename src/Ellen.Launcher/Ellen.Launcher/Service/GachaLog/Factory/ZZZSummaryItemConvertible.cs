// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.ViewModel.GachaLog;

namespace Ellen.Launcher.Service.GachaLog.Factory;

/// <summary>
/// 绝区零 GachaItem 适配为 ISummaryItemConvertible，复用原神的 TypedWishSummaryBuilder 管线。
/// 物品名称与稀有度直接从 GachaItem 的 Name/Quality 字段取（不依赖原神元数据）。
/// </summary>
public sealed class ZZZSummaryItemConvertible : ISummaryItemConvertible
{
    private readonly GachaItem item;

    public ZZZSummaryItemConvertible(GachaItem item)
    {
        this.item = item;
    }

    public QualityType Quality => (QualityType)item.Quality;

    public SummaryItem ToSummaryItem(int lastPull, in DateTimeOffset time, bool isUp)
    {
        QualityType quality = (QualityType)item.Quality;
        return new SummaryItem
        {
            Name = item.Name ?? string.Empty,
            Quality = quality,
            IsUp = isUp,
            LastPull = lastPull,
            Time = time,
            Color = QualityColorConverter.QualityToColor(quality),
            GuaranteeOrangeThreshold = 90,
        };
    }
}
