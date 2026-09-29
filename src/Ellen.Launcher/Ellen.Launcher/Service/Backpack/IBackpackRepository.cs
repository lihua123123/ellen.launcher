// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.Backpack;

public interface IBackpackRepository : IRepository<BackpackArchive>, IRepository<BackpackItem>
{
    void AddBackpackArchive(BackpackArchive archive);

    void RemoveBackpackArchiveById(Guid archiveId);

    BackpackArchive? GetBackpackArchiveById(Guid archiveId);

    BackpackArchive? GetBackpackArchiveByName(string name);

    ObservableCollection<BackpackArchive> GetBackpackArchiveCollection();

    void AddBackpackItemRange(IEnumerable<BackpackItem> items);

    void RemoveBackpackItemRangeByArchiveId(Guid archiveId);

    ImmutableArray<BackpackItem> GetBackpackItemImmutableArrayByArchiveId(Guid archiveId);
}
