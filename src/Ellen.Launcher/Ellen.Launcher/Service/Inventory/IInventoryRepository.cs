// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Inventory;

public interface IInventoryRepository : IRepository<InventoryItem>
{
    void AddInventoryItemRangeByProjectId(IEnumerable<InventoryItem> items);

    void RemoveInventoryItemRangeByProjectId(Guid projectId);

    void UpdateInventoryItem(InventoryItem item);

    ImmutableArray<InventoryItem> GetInventoryItemImmutableArrayByProjectId(Guid projectId);

    ImmutableDictionary<uint, InventoryItem> GetInventoryItemImmutableDictionaryByProjectId(Guid projectId);
}