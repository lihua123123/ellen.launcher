// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Database;
using Microsoft.EntityFrameworkCore;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Inventory;

[Service(ServiceLifetime.Singleton, typeof(IInventoryRepository))]
public sealed partial class InventoryRepository : IInventoryRepository
{
    [GeneratedConstructor]
    public partial InventoryRepository(IServiceProvider serviceProvider);

    public partial IServiceProvider ServiceProvider { get; }

    public void RemoveInventoryItemRangeByProjectId(Guid projectId)
    {
        this.Delete(i => i.ProjectId == projectId);
    }

    public void AddInventoryItemRangeByProjectId(IEnumerable<InventoryItem> items)
    {
        this.AddRange(items);
    }

    public void UpsertInventoryItemRangeByProjectId(Guid projectId, IEnumerable<InventoryItem> items)
    {
        InventoryItem[] incomingItems = items
            .GroupBy(static item => item.ItemId)
            .Select(group => InventoryItem.From(projectId, group.Key, group.Max(static item => item.Count)))
            .ToArray();

        if (incomingItems.Length == 0)
        {
            return;
        }

        using IServiceScope scope = ServiceProvider.CreateScope();
        AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Dictionary<uint, InventoryItem> existingItems = appDbContext.InventoryItems
            .AsTracking()
            .Where(item => item.ProjectId == projectId)
            .ToDictionary(item => item.ItemId);

        foreach (InventoryItem incomingItem in incomingItems)
        {
            if (existingItems.TryGetValue(incomingItem.ItemId, out InventoryItem? existingItem))
            {
                existingItem.Count = incomingItem.Count;
            }
            else
            {
                appDbContext.InventoryItems.Add(incomingItem);
            }
        }

        // Persist all returned material updates together. Items absent from the response remain untouched.
        appDbContext.SaveChanges();
    }

    public void ReplaceInventoryItemRangeByProjectId(Guid projectId, IEnumerable<InventoryItem> items)
    {
        using IServiceScope scope = ServiceProvider.CreateScope();
        AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = appDbContext.Database.BeginTransaction();
        appDbContext.InventoryItems.Where(item => item.ProjectId == projectId).ExecuteDelete();
        appDbContext.InventoryItems.AddRange(items);
        appDbContext.SaveChanges();
        transaction.Commit();
    }

    public void UpdateInventoryItem(InventoryItem item)
    {
        this.Update(item);
    }

    public ImmutableArray<InventoryItem> GetInventoryItemImmutableArrayByProjectId(Guid projectId)
    {
        return this.ImmutableArray(i => i.ProjectId == projectId);
    }

    public ImmutableDictionary<uint, InventoryItem> GetInventoryItemImmutableDictionaryByProjectId(Guid projectId)
    {
        return this.Query(query => query.Where(i => i.ProjectId == projectId).ToImmutableDictionary(i => i.ItemId));
    }
}