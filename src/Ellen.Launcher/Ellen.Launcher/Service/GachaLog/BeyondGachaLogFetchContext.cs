// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Service.GachaLog.QueryProvider;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.GachaLog;

public sealed class BeyondGachaLogFetchContext
{
    private readonly GachaLogServiceMetadataContext serviceContext;
    private readonly IGachaLogRepository repository;
    private readonly bool isLazy;

    public BeyondGachaLogFetchContext(IGachaLogRepository repository, GachaLogServiceMetadataContext serviceContext, bool isLazy)
    {
        this.repository = repository;
        this.serviceContext = serviceContext;
        this.isLazy = isLazy;
    }

    public GachaLogFetchStatus Status { get; set; } = default!;

    public List<BeyondGachaItem> ItemsToAdd { get; set; } = [];

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

    public void EnsureArchiveAndEndId(BeyondGachaLogItem item, IAdvancedDbCollectionView<GachaArchive> archives, IGachaLogRepository repository)
    {
        TargetArchive ??= GachaArchiveOperation.GetOrAdd(repository, item.Uid, GachaGameType.Genshin, archives);
        DbEndId ??= repository.GetNewestBeyondGachaItemIdByArchiveIdAndGachaType(TargetArchive.InnerId, CurrentType);
    }

    public bool ShouldAddItem(BeyondGachaLogItem item)
    {
        // For non-lazy mode, all items should be added
        return !isLazy || item.Id > DbEndId; // DbEndId will be evaluated to 0 if null
    }

    public bool HasReachCurrentTypeEnd(ImmutableArray<BeyondGachaLogItem> items)
    {
        return CurrentTypeAddingCompleted || items.Length < GachaLogTypedQueryOptions.BeyondSize;
    }

    public void AddItem(BeyondGachaLogItem item)
    {
        ArgumentNullException.ThrowIfNull(TargetArchive);
        ItemsToAdd.Add(BeyondGachaItem.From(TargetArchive.InnerId, item));
        Status.Items.Add(serviceContext.GetBeyondItem(uint.Parse(item.ItemId)).GetOrCreateItem());
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
            repository.RemoveBeyondGachaItemRangeByArchiveIdAndGachaTypeNewerThanEndId(TargetArchive.InnerId, TypedQueryOptions.Type, TypedQueryOptions.EndId);
        }

        repository.AddBeyondGachaItemRange(ItemsToAdd);
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
