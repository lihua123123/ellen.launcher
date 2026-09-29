// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Database;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.Backpack;

[Service(ServiceLifetime.Singleton, typeof(IBackpackRepository))]
public sealed partial class BackpackRepository : IBackpackRepository
{
    [GeneratedConstructor]
    public partial BackpackRepository(IServiceProvider serviceProvider);

    public partial IServiceProvider ServiceProvider { get; }

    public ObservableCollection<BackpackArchive> GetBackpackArchiveCollection()
    {
        return this.ObservableCollection<BackpackArchive>();
    }

    public ImmutableArray<BackpackItem> GetBackpackItemImmutableArrayByArchiveId(Guid archiveId)
    {
        return this.ImmutableArray<BackpackItem, BackpackItem>(query => query.Where(i => i.ArchiveId == archiveId).OrderBy(i => i.ItemId));
    }

    public void AddBackpackArchive(BackpackArchive archive)
    {
        using (IServiceScope scope = ServiceProvider.CreateScope())
        {
            AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            appDbContext.BackpackArchives.AddAndSave(archive);
        }
    }

    public void RemoveBackpackArchiveById(Guid archiveId)
    {
        this.DeleteByInnerId<BackpackArchive>(archiveId);
    }

    public BackpackArchive? GetBackpackArchiveById(Guid archiveId)
    {
        return this.SingleOrDefault<BackpackArchive>(a => a.InnerId == archiveId);
    }

    public BackpackArchive? GetBackpackArchiveByName(string name)
    {
        return this.SingleOrDefault<BackpackArchive>(a => a.Name == name);
    }

    public void AddBackpackItemRange(IEnumerable<BackpackItem> items)
    {
        this.AddRange(items);
    }

    public void RemoveBackpackItemRangeByArchiveId(Guid archiveId)
    {
        this.Delete<BackpackItem>(i => i.ArchiveId == archiveId);
    }
}
