// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.GachaLog.QueryProvider;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.GachaLog;

public sealed class GachaLogFetchContext
{
    private readonly GachaLogServiceMetadataContext serviceContext;
    private readonly IGachaLogRepository repository;
    private readonly bool isLazy;
    private readonly GachaGameType game;

    public GachaLogFetchContext(IGachaLogRepository repository, GachaLogServiceMetadataContext serviceContext, bool isLazy, GachaGameType game = GachaGameType.Genshin)
    {
        this.repository = repository;
        this.serviceContext = serviceContext;
        this.isLazy = isLazy;
        this.game = game;
    }

    public GachaLogFetchStatus Status { get; set; } = default!;

    public List<GachaItem> ItemsToAdd { get; set; } = [];

    public GachaArchive? TargetArchive { get; set; }

    public long? DbEndId { get; set; }

    public GachaLogTypedQueryOptions TypedQueryOptions { get; set; } = default!;

    public bool CurrentTypeAddingCompleted { get; set; }

    public GachaType CurrentType { get; set; }

    public void ResetType(GachaType configType, in GachaLogQuery query)
    {
        DbEndId = null;
        CurrentType = configType;
        ItemsToAdd.Clear();
        Status = new(configType);
        TypedQueryOptions = new(query, configType);
        CurrentTypeAddingCompleted = false;
    }

    public void ResetCurrentPage()
    {
        Status = new(CurrentType);
    }

    public void EnsureArchiveAndEndId(GachaLogItem item, IAdvancedDbCollectionView<GachaArchive> archives, IGachaLogRepository repository)
    {
        TargetArchive ??= GachaArchiveOperation.GetOrAdd(repository, item.Uid, game, archives);
        DbEndId ??= repository.GetNewestGachaItemIdByArchiveIdAndQueryType(TargetArchive.InnerId, CurrentType);
    }

    public bool ShouldAddItem(GachaLogItem item)
    {
        // For non-lazy mode, all items should be added
        return !isLazy || item.Id > DbEndId; // DbEndId will be evaluated to 0 if null
    }

    public bool HasReachCurrentTypeEnd(ImmutableArray<GachaLogItem> items)
    {
        if (CurrentTypeAddingCompleted)
        {
            return true;
        }

        // 绝区零 API 每页返回的条数可能与 Size 不同（实测每页 5 条），
        // 用「空页」判断结束，与 zzz-signal-search-export 一致
        if (game is GachaGameType.ZZZ)
        {
            return items.Length == 0;
        }

        return items.Length < GachaLogTypedQueryOptions.Size;
    }

    public void AddItem(GachaLogItem item)
    {
        ArgumentNullException.ThrowIfNull(TargetArchive);

        if (game is GachaGameType.ZZZ)
        {
            // 绝区零物品不在原神元数据中，名称与稀有度直接落库
            ItemsToAdd.Add(GachaItem.FromZZZ(TargetArchive.InnerId, item));
            Status.Items.Add(new Item
            {
                Name = item.Name,
                Quality = GachaItem.ZZZQualityToQualityType(item.RankType),
            });
        }
        else
        {
            ItemsToAdd.Add(GachaItem.From(TargetArchive.InnerId, item, serviceContext.GetItemId(item)));
            Status.Items.Add(serviceContext.GetItemByNameAndType(item.Name, item.ItemType));
        }

        TypedQueryOptions.EndId = item.Id;
    }

    public void SaveItems()
    {
        if (ItemsToAdd.Count <= 0)
        {
            return;
        }

        if (TargetArchive is null)
        {
            return;
        }

        if (!isLazy)
        {
            // Aggressive mode: Remove all items of the same type and newer than end id
            repository.RemoveGachaItemRangeByArchiveIdAndQueryTypeNewerThanEndId(TargetArchive.InnerId, TypedQueryOptions.Type, TypedQueryOptions.EndId);
        }

        repository.AddGachaItemRange(ItemsToAdd);
    }

    public void CompleteCurrentTypeAdding()
    {
        CurrentTypeAddingCompleted = true;
    }

    public void Report(IProgress<GachaLogFetchStatus> progress, bool isAuthKeyTimeout = false)
    {
        Status.AuthKeyTimeout = isAuthKeyTimeout;
        progress.Report(Status);
    }
}