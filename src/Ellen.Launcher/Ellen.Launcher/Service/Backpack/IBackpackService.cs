// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Yae.PlayerStore;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Backpack;

public interface IBackpackService
{
    ValueTask<IAdvancedDbCollectionView<BackpackArchive>> GetArchiveCollectionAsync();

    bool RefreshByEmbeddedYae(BackpackArchive archive, PlayerStoreResult storeResult);

    void RemoveArchive(BackpackArchive archive);

    BackpackArchive AddArchive(string name);

    ImmutableArray<BackpackItem> GetBackpackItemImmutableArrayByArchiveId(Guid archiveId);
}
