// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.Inventory;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.Service.Yae;
using Ellen.Launcher.ViewModel.Cultivation;
using Ellen.Launcher.ViewModel.Game;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;
using Ellen.Launcher.Web.Response;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Inventory;

[Service(ServiceLifetime.Singleton, typeof(IInventoryService))]
public sealed partial class InventoryService : IInventoryService
{
    private readonly PromotionDeltaFactory promotionDeltaFactory;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IInventoryRepository inventoryRepository;
    private readonly IUserService userService;
    private readonly IMessenger messenger;
    private readonly ILogger<InventoryService> logger;

    [GeneratedConstructor]
    public partial InventoryService(IServiceProvider serviceProvider);

    public ImmutableArray<InventoryItemView> GetInventoryItemViews(ICultivationMetadataContext context, CultivateProject cultivateProject, ICommand saveCommand)
    {
        Guid projectId = cultivateProject.InnerId;
        ImmutableDictionary<uint, InventoryItem> entities = inventoryRepository.GetInventoryItemImmutableDictionaryByProjectId(projectId);

        ImmutableArray<InventoryItemView>.Builder results = ImmutableArray.CreateBuilder<InventoryItemView>();
        foreach (Material meta in context.EnumerateInventoryMaterial())
        {
            InventoryItem entity = entities.GetValueOrDefault(meta.Id) ?? InventoryItem.From(projectId, meta.Id);
            results.Add(new(entity, meta, saveCommand));
        }

        return results.ToImmutable();
    }

    public void SaveInventoryItem(InventoryItemView item)
    {
        inventoryRepository.UpdateInventoryItem(item.Entity);
    }

    public ValueTask RefreshInventoryAsync(RefreshOptions refreshOptions)
    {
        switch (refreshOptions.Kind)
        {
            case RefreshOptionKind.WebCalculator:
                ArgumentNullException.ThrowIfNull(refreshOptions.MetadataContext);
                return RefreshInventoryByCalculatorAsync(refreshOptions.MetadataContext, refreshOptions.Project);
            case RefreshOptionKind.EmbeddedYae:
                ArgumentNullException.ThrowIfNull(refreshOptions.YaeService);
                ArgumentNullException.ThrowIfNull(refreshOptions.ViewModelSupportLaunchExecution);
                return RefreshInventoryByEmbeddedYaeAsync(refreshOptions.YaeService, refreshOptions.ViewModelSupportLaunchExecution, refreshOptions.Project);
        }

        return ValueTask.CompletedTask;
    }

    public void SaveInventoryItemsFromBackpackArchive(CultivateProject project, ImmutableArray<BackpackItem> backpackItems, ICultivationMetadataContext context)
    {
        Dictionary<uint, uint> backpackCounts = [];
        foreach (BackpackItem item in backpackItems)
        {
            // Use IDs rather than the display whitelist (which can omit new or custom materials).
            if (item.Count > 0 && context.IdMaterialMap.ContainsKey(item.ItemId))
            {
                ulong total = (ulong)backpackCounts.GetValueOrDefault(item.ItemId) + item.Count;
                backpackCounts[item.ItemId] = (uint)Math.Min(total, uint.MaxValue);
            }
        }

        if (backpackCounts.Count == 0)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ServiceInventoryBackpackNoMaterials));
            return;
        }

        Guid projectId = project.InnerId;
        inventoryRepository.ReplaceInventoryItemRangeByProjectId(
            projectId,
            backpackCounts.Select(pair => InventoryItem.From(projectId, pair.Key, pair.Value)));
    }

    public void RemoveInventoryItems(CultivateProject cultivateProject)
    {
        Guid projectId = cultivateProject.InnerId;
        inventoryRepository.RemoveInventoryItemRangeByProjectId(projectId);
    }

    private async ValueTask RefreshInventoryByCalculatorAsync(ICultivationMetadataContext context, CultivateProject project)
    {
        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            messenger.Send(InfoBarMessage.Warning(SH.MustSelectUserAndUid));
            return;
        }

        ImmutableArray<AvatarPromotionDelta> deltas = await promotionDeltaFactory.GetAsync(context, userAndUid).ConfigureAwait(false);
        if (deltas.IsDefault)
        {
            // The catalog endpoint already showed its error. Do not run a partial batch.
            return;
        }

        BatchConsumption? batchConsumption;
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            CalculateClient calculateClient = scope.ServiceProvider.GetRequiredService<CalculateClient>();

            Response<BatchConsumption> resp = await calculateClient
                .BatchComputeAsync(userAndUid, deltas, true)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(resp, scope.ServiceProvider, out batchConsumption))
            {
                return;
            }
        }

        if (batchConsumption is not { HasUserInfo: true })
        {
            messenger.Send(InfoBarMessage.Warning(SH.ServiceInventoryCalculatorMissingInventoryData));
            return;
        }

        Dictionary<uint, uint> available = [];
        if (!batchConsumption.AvailableMaterial.IsDefaultOrEmpty)
        {
            foreach (Item item in batchConsumption.AvailableMaterial)
            {
                if (item.Id != 0)
                {
                    available[item.Id] = Math.Max(available.GetValueOrDefault(item.Id), item.Num);
                }
            }
        }

        if (available.Count == 0)
        {
            // 服务端没有回任何背包数据时保留原库存，绝不拿「被满足的需求」去覆盖。
            logger.LogWarning("Calculator inventory: available_material is empty, inventory kept unchanged");
            return;
        }

        Dictionary<uint, uint> owned = DeriveOwnedCounts(batchConsumption, available, context);

        inventoryRepository.UpsertInventoryItemRangeByProjectId(
            project.InnerId,
            owned.Select(pair => InventoryItem.From(project.InnerId, pair.Key, pair.Value)));

        // Only aggregate counts: no account identifiers, credentials or response bodies.
        logger.LogInformation("Calculator inventory: {Deltas} deltas, {Available} available entries, {Owned} owned entries written", deltas.Length, available.Count, owned.Count);
    }

    /// <summary>
    /// 从养成计算器的响应还原「持有量」。
    /// </summary>
    /// <remarks>
    /// 响应里两个数字都不是持有量，必须合起来看：
    /// <list type="bullet">
    /// <item><c>available_material.num</c> 是「扣掉本次直接需求后的剩余」，且只有剩余 &gt; 0 时才返回该条目。</item>
    /// <item><c>overall_consume.num</c> 是本次需求，<c>lack_num</c> 是扣掉「库存 + 3:1 合成」后仍缺的部分，
    /// 所以 <c>num - lack_num</c> 是「被满足的需求」，而不是持有量。</item>
    /// </list>
    /// 于是分三种情形还原（实测嵌合种/并生嵌合胞/源生嵌合体 687/175/38 全部命中）：
    /// <list type="number">
    /// <item>出现在 available 里：说明剩余 &gt; 0，即库存已付清需求，持有量 = 剩余 + 需求。</item>
    /// <item>未出现且能靠 3:1 合成补：持有量 = 被满足的需求 - 低阶剩余可合成出的数量。</item>
    /// <item>未出现且不能合成：持有量 = 被满足的需求。</item>
    /// </list>
    /// 第 2 条是关键：服务端算合成额度时用的是「低阶材料的剩余」（即 available 里的那个数），
    /// 而不是扣掉合成消耗之后的数，所以这里可以直接用低阶 available / 比例 反推，无需拓扑排序。
    /// </remarks>
    private static Dictionary<uint, uint> DeriveOwnedCounts(
        BatchConsumption batchConsumption,
        Dictionary<uint, uint> available,
        ICultivationMetadataContext context)
    {
        Dictionary<uint, uint> owned = [];

        if (batchConsumption.OverallConsume.IsDefaultOrEmpty)
        {
            return owned;
        }

        // 只看「单素材 3:1 往上合成」的配方；1:1 的异元素转化/摆设制作不参与需求补足，必须排除。
        Dictionary<uint, (uint LowerId, uint Ratio)> chain = [];
        if (context.ResultMaterialIdCombineMap is { Count: > 0 } combines)
        {
            foreach (Combine combine in combines.Values)
            {
                if (combine?.Result is not { Count: 1 } result
                    || combine.Materials.IsDefaultOrEmpty
                    || combine.Materials.Length != 1)
                {
                    continue;
                }

                IdCount material = combine.Materials[0];
                if (material is not null && material.Count > 1)
                {
                    chain.TryAdd(result.Id.Value, (material.Id.Value, material.Count));
                }
            }
        }

        foreach (Item item in batchConsumption.OverallConsume)
        {
            if (item.Id == 0 || item.LackNum < 0 || item.LackNum > item.Num)
            {
                continue;
            }

            uint demand = item.Num;
            uint satisfied = demand - (uint)item.LackNum;

            uint count;
            if (available.TryGetValue(item.Id, out uint remaining))
            {
                // 剩余 > 0 意味着库存足够付清需求，服务端已扣掉 demand。
                ulong total = (ulong)remaining + demand;
                count = (uint)Math.Min(total, uint.MaxValue);
            }
            else
            {
                // 剩余为 0（未返回）说明库存不足以付清需求，被满足的部分里可能含 3:1 合成。
                uint credit = 0;
                if (chain.TryGetValue(item.Id, out (uint LowerId, uint Ratio) link)
                    && link.Ratio > 0
                    && available.TryGetValue(link.LowerId, out uint lowerRemaining))
                {
                    credit = Math.Min(lowerRemaining / link.Ratio, satisfied);
                }

                count = satisfied - credit;
            }

            owned[item.Id] = Math.Max(owned.GetValueOrDefault(item.Id), count);
        }

        // 有背包数据但本次没有需求的材料，剩余即持有量。
        foreach ((uint itemId, uint remaining) in available)
        {
            if (!owned.ContainsKey(itemId))
            {
                owned[itemId] = remaining;
            }
        }

        return owned;
    }

    private async ValueTask RefreshInventoryByEmbeddedYaeAsync(IYaeService yaeService, IViewModelSupportLaunchExecution viewModel, CultivateProject project)
    {
        if (await yaeService.GetInventoryAsync(viewModel).ConfigureAwait(false) is not { } uiif)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ServiceYaeEmbeddedYaeErrorTitle, SH.ServiceInventoryRefreshByEmbeddedYaeErrorMessage));
            return;
        }

        inventoryRepository.RemoveInventoryItemRangeByProjectId(project.InnerId);
        inventoryRepository.AddInventoryItemRangeByProjectId(UIIFItemToInventoryItem(project.InnerId, uiif.List));

        static IEnumerable<InventoryItem> UIIFItemToInventoryItem(Guid projectId, ImmutableArray<UIIFItem> uiif)
        {
            foreach (UIIFItem item in uiif)
            {
                if (item.Material is not null)
                {
                    yield return InventoryItem.From(projectId, item.ItemId, item.Material.Count);
                }
            }
        }
    }
}