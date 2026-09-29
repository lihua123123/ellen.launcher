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