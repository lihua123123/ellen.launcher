// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ellen.Launcher.Model.Entity;

[Table("gacha_items")]
public sealed class GachaItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid InnerId { get; set; }

    [ForeignKey(nameof(ArchiveId))]
    public GachaArchive Archive { get; set; } = default!;

    public Guid ArchiveId { get; set; }

    public GachaType GachaType { get; set; }

    public GachaType QueryType { get; set; }

    public uint ItemId { get; set; }

    /// <summary>
    /// 物品名称，仅绝区零记录写入（原神记录通过 ItemId 关联元数据）
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 归一化后的品质（<see cref="Model.Intrinsic.QualityType"/> 数值），仅绝区零记录写入，0 表示未记录
    /// </summary>
    public int Quality { get; set; }

    public DateTimeOffset Time { get; set; }

    public long Id { get; set; }

    public static GachaItem From(Guid archiveId, GachaLogItem item, uint itemId)
    {
        return new()
        {
            ArchiveId = archiveId,
            GachaType = item.GachaType,
            QueryType = item.GachaType.ToQueryType(),
            ItemId = itemId,
            Time = item.Time,
            Id = item.Id,
        };
    }

    /// <summary>
    /// 从绝区零调频记录创建实体，名称与稀有度直接落库，不依赖原神元数据
    /// </summary>
    public static GachaItem FromZZZ(Guid archiveId, GachaLogItem item)
    {
        return new()
        {
            ArchiveId = archiveId,
            GachaType = item.GachaType,
            // 绝区零的卡池类型无需映射，请求类型即存储类型
            QueryType = item.GachaType,
            ItemId = uint.TryParse(item.ItemId, out uint itemId) ? itemId : 0U,
            Time = item.Time,
            Id = item.Id,
            Name = item.Name,
            Quality = (int)ZZZQualityToQualityType(item.RankType),
        };
    }

    /// <summary>
    /// 绝区零稀有度（2=B/3=A/4=S）映射到通用品质（蓝/紫/橙）
    /// </summary>
    public static Model.Intrinsic.QualityType ZZZQualityToQualityType(Model.Intrinsic.QualityType rankType)
    {
        return (int)rankType switch
        {
            4 => Model.Intrinsic.QualityType.QUALITY_ORANGE,
            3 => Model.Intrinsic.QualityType.QUALITY_PURPLE,
            2 => Model.Intrinsic.QualityType.QUALITY_BLUE,
            _ => Model.Intrinsic.QualityType.QUALITY_NONE,
        };
    }

    public static GachaItem From(Guid archiveId, Hk4eItem item, int timezoneOffset)
    {
        return new()
        {
            ArchiveId = archiveId,
            GachaType = item.GachaType,
            QueryType = (GachaType)item.UIGFGachaType,
            ItemId = item.ItemId,
            Time = new(item.Time, TimeSpan.FromHours(timezoneOffset)),
            Id = item.Id,
        };
    }
}