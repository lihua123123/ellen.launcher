// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.Cultivation;

public interface ICultivationRepository : IRepository<CultivateEntryLevelInformation>,
    IRepository<CultivateProject>,
    IRepository<CultivateEntry>,
    IRepository<CultivateItem>
{
    void AddCultivateProject(CultivateProject project);

    void RemoveCultivateEntryById(Guid entryId);

    void RemoveCultivateItemRangeByEntryId(Guid entryId);

    void RemoveCultivateProjectById(Guid projectId);

    ImmutableArray<CultivateEntry> GetCultivateEntryImmutableArrayByProjectId(Guid projectId);

    ImmutableArray<CultivateItem> GetCultivateItemImmutableArrayByEntryId(Guid entryId);

    /// <summary>
    /// 一次取回整个项目下所有条目的养成物品，按 EntryId 分组前使用。
    /// 统计页原先对每个条目单独查一次库（条目上百个 = 上百次作用域 + DbContext），
    /// 这里合并成一条 JOIN，避免勾选一次完成状态就重建上百个上下文。
    /// </summary>
    ImmutableArray<CultivateItem> GetCultivateItemImmutableArrayByProjectId(Guid projectId);

    ObservableCollection<CultivateProject> GetCultivateProjectCollection();

    CultivateProject? GetCultivateProjectById(Guid projectId);

    void AddCultivateEntry(CultivateEntry entry);

    void AddCultivateItemRange(IEnumerable<CultivateItem> toAdd);

    void UpdateCultivateItem(CultivateItem item);

    void RemoveLevelInformationByEntryId(Guid entryId);

    void AddLevelInformation(CultivateEntryLevelInformation levelInformation);

    ImmutableArray<CultivateEntry> GetCultivateEntryImmutableArrayIncludingLevelInformationByProjectId(Guid projectId);

    ImmutableArray<CultivateEntry> GetCultivateEntryImmutableArrayByProjectIdAndItemId(Guid projectId, uint itemId);

    Guid GetCultivateProjectIdByEntryId(Guid entryId);
}