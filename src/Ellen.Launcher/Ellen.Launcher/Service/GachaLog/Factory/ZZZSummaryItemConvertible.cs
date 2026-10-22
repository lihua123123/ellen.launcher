// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.ZZZ;
using Ellen.Launcher.ViewModel.GachaLog;
using Ellen.Launcher.Web.Endpoint.Upstream;

namespace Ellen.Launcher.Service.GachaLog.Factory;

/// <summary>
/// 绝区零 GachaItem 适配为 ISummaryItemConvertible，复用原神的 TypedWishSummaryBuilder 管线。
/// 物品名称与稀有度优先取落库字段，图标由随包的绝区零物品元数据（Starward 数据源）补齐。
/// </summary>
public sealed class ZZZSummaryItemConvertible : ISummaryItemConvertible
{
    private readonly GachaItem item;
    private readonly ZZZGachaItemInfo? info;

    public ZZZSummaryItemConvertible(GachaItem item, ZZZGachaItemInfo? info)
    {
        this.item = item;
        this.info = info;
    }

    public QualityType Quality
    {
        get
        {
            QualityType quality = (QualityType)item.Quality;
            return quality is QualityType.QUALITY_NONE ? info?.Quality ?? QualityType.QUALITY_NONE : quality;
        }
    }

    public SummaryItem ToSummaryItem(int lastPull, in DateTimeOffset time, bool isUp)
    {
        QualityType quality = Quality;
        return new SummaryItem
        {
            Id = item.ItemId,
            Name = item.Name ?? info?.Name ?? string.Empty,
            Icon = info?.IconUri ?? StaticResourcesEndpoints.UIIconNone,
            Quality = quality,
            IsUp = isUp,
            LastPull = lastPull,
            Time = time,
            Color = QualityColorConverter.QualityToColor(quality),
            GuaranteeOrangeThreshold = 90,
        };
    }
}
