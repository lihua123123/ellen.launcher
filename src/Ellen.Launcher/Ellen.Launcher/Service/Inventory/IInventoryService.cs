// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.ViewModel.Cultivation;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Inventory;

public interface IInventoryService
{
    ImmutableArray<InventoryItemView> GetInventoryItemViews(ICultivationMetadataContext context, CultivateProject cultivateProject, ICommand saveCommand);

    void RemoveInventoryItems(CultivateProject cultivateProject);

    void SaveInventoryItem(InventoryItemView item);

    void SaveInventoryItemsFromBackpackArchive(CultivateProject project, ImmutableArray<BackpackItem> backpackItems, ICultivationMetadataContext context);

    ValueTask RefreshInventoryAsync(RefreshOptions refreshOptions);
}