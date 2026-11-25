// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.AvatarInfo;
using Ellen.Launcher.Service.Backpack;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.Service.Cultivation.Consumption;
using Ellen.Launcher.Service.Cultivation.Offline;
using Ellen.Launcher.Service.Game.Island;
using Ellen.Launcher.Service.Inventory;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Navigation;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.Service.Yae;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.ViewModel.Game;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using AvatarPromotionDelta = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.AvatarPromotionDelta;
using CalculateBatchConsumption = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.BatchConsumption;
using CalculateItem = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.Item;
using DetailedCharacter = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar.DetailedCharacter;
using PromotionDelta = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.PromotionDelta;

namespace Ellen.Launcher.ViewModel.Cultivation;

[SuppressMessage("", "CA1001")]
[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class CultivationViewModel : Abstraction.ViewModel
{
    private readonly ExclusiveTokenProvider exclusiveTokenProvider = new();

    /// <summary>统计重建的串行闸门，配合 <see cref="statisticsRefreshQueued"/> 做高频刷新合并。</summary>
    private readonly SemaphoreSlim statisticsRefreshGate = new(1, 1);

    private int statisticsRefreshQueued;

    private readonly IBackpackService backpackService;
    private readonly AvatarInfoRepositoryOperation avatarInfoRepositoryOperation;
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IEmbeddedYaeGate embeddedYaeGate;
    private readonly ICultivationService cultivationService;
    private readonly INavigationService navigationService;
    private readonly IInventoryService inventoryService;
    private readonly IServiceProvider serviceProvider;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly IUserService userService;
    private readonly IYaeService yaeService;
    private readonly IMessenger messenger;

    private CultivationMetadataContext? metadataContext;

    [GeneratedConstructor]
    public partial CultivationViewModel(IServiceProvider serviceProvider);

    public IAdvancedDbCollectionView<CultivateProject>? Projects
    {
        get;
        set
        {
            AdvancedCollectionViewCurrentChanged.Detach(field, OnCurrentProjectChanged);
            SetProperty(ref field, value);
            AdvancedCollectionViewCurrentChanged.Attach(field, OnCurrentProjectChanged);
        }
    }

    [ObservableProperty]
    public partial ImmutableArray<InventoryItemView> InventoryItems { get; set; } = [];

    [ObservableProperty]
    public partial IAdvancedCollectionView<CultivateEntryView>? CultivateEntries { get; set; }

    [ObservableProperty]
    public partial bool EntriesUpdating { get; set; }

    [ObservableProperty]
    public partial bool IncompleteFirst { get; set; }

    [ObservableProperty]
    public partial bool AutoSynthesisPreview { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<StatisticsCultivateItem>? StatisticsItems { get; set; }

    [ObservableProperty]
    public partial ResinStatistics? ResinStatistics { get; set; }

    [ObservableProperty]
    public partial SearchData? SearchData { get; set; }

    /// <summary>外层标签页索引：0 = 材料清单，1 = 背包物品。</summary>
    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    /// <summary>材料清单内层标签页索引：0 = 养成条目，1 = 材料统计。</summary>
    [ObservableProperty]
    public partial int SelectedMaterialTabIndex { get; set; }

    private const int TabMaterialList = 0;
    private const int TabInventory = 1;
    private const int MaterialTabEntry = 0;
    private const int MaterialTabStatistics = 1;

    /// <summary>统计集合与背包视图是否已过期。只有对应标签页可见时才会真正重建。</summary>
    private bool statisticsDirty = true;

    private bool inventoryDirty = true;

    private bool IsStatisticsVisible => SelectedTabIndex is TabMaterialList && SelectedMaterialTabIndex is MaterialTabStatistics;

    private bool IsInventoryVisible => SelectedTabIndex is TabInventory;

    partial void OnSelectedTabIndexChanged(int value)
    {
        PublishVisibleTabAsync().SafeForget();
    }

    partial void OnSelectedMaterialTabIndexChanged(int value)
    {
        PublishVisibleTabAsync().SafeForget();
    }

    /// <summary>
    /// 按需发布当前可见标签页的数据。
    /// </summary>
    /// <remarks>
    /// 与背包页同一套做法：Pivot 会 measure 它持有的每一个 PivotItem，如果进页面就把
    /// 养成条目、材料统计、背包物品三份数据同时发出去，三个列表会一起实例化一屏容器
    /// 并同时发起图片加载。所以这里只构建当前可见的那一页，其余标记为过期，
    /// 等用户真正切过去时再算。
    /// </remarks>
    private async ValueTask PublishVisibleTabAsync()
    {
        await taskContext.SwitchToMainThreadAsync();

        if (IsInventoryVisible)
        {
            if (inventoryDirty)
            {
                await RefreshInventoryItemsCoreAsync().ConfigureAwait(false);
            }

            return;
        }

        if (IsStatisticsVisible && statisticsDirty)
        {
            await RefreshStatisticsItemsCoreAsync().ConfigureAwait(false);
        }
    }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        metadataContext = await metadataService.GetContextAsync<CultivationMetadataContext>(token).ConfigureAwait(false);
        SearchData searchData = SearchData.CreateForCultivation();

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            IAdvancedDbCollectionView<CultivateProject> projects = await cultivationService.GetProjectCollectionAsync().ConfigureAwait(false);
            await taskContext.SwitchToMainThreadAsync();
            Projects = projects;
            Projects.MoveCurrentTo(Projects.Source.SelectedOrFirstOrDefault());
        }

        // Force update when re-entering the page
        if (Projects.CurrentItem is not null && CultivateEntries is null)
        {
            await UpdateEntryCollectionAsync(Projects.CurrentItem).ConfigureAwait(false);
        }
        else if (Projects.CurrentItem is not null && StatisticsItems is null)
        {
            await UpdateStatisticsItemsAsync().ConfigureAwait(false);
        }

        await taskContext.SwitchToMainThreadAsync();
        SearchData = searchData;

        return true;
    }

    protected override void UninitializeOverride()
    {
        using (Projects?.SuppressChangeCurrentItem())
        {
            Projects = default;
        }

        if (AutoSynthesisPreview)
        {
            StatisticsItems = null;
            ResinStatistics = null;
            AutoSynthesisPreview = false;
        }
    }

    private void OnCurrentProjectChanged(object? sender, object? e)
    {
        UpdateEntryCollectionAsync(Projects?.CurrentItem).SafeForget();
    }

    [Command("AddProjectCommand")]
    private async Task AddProjectAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Add project", "CultivationViewModel.Command"));

        CultivateProjectDialog dialog = await contentDialogFactory.CreateInstanceAsync<CultivateProjectDialog>(serviceProvider).ConfigureAwait(false);
        (bool isOk, CultivateProject project) = await dialog.CreateProjectAsync().ConfigureAwait(false);

        if (!isOk)
        {
            return;
        }

        InfoBarMessage message = await cultivationService.TryAddProjectAsync(project).ConfigureAwait(false) switch
        {
            ProjectAddResultKind.Added => InfoBarMessage.Success(SH.ViewModelCultivationProjectAdded),
            ProjectAddResultKind.InvalidName => InfoBarMessage.Information(SH.ViewModelCultivationProjectInvalidName),
            ProjectAddResultKind.AlreadyExists => InfoBarMessage.Information(SH.ViewModelCultivationProjectAlreadyExists),
            _ => throw EllenException.NotSupported(),
        };

        messenger.Send(message);
    }

    /// <summary>
    /// 添加自定义材料计划：弹窗里输入等级模板参数并自选材料种类，
    /// 数量由离线计算器按既有等级模板算表推导，落在两个自定义养成条目上。
    /// 后续的统计、合成展开和背包抵扣都沿用现有规则。
    /// </summary>
    [Command("AddCustomMaterialCommand")]
    private async Task AddCustomMaterialAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Add custom material plan", "CultivationViewModel.Command"));

        CustomMaterialPlanDialog dialog = await contentDialogFactory
            .CreateInstanceAsync<CustomMaterialPlanDialog>(serviceProvider)
            .ConfigureAwait(false);
        (bool isOk, CustomMaterialPlanResult result) = await dialog.SelectAsync().ConfigureAwait(false);
        if (!isOk)
        {
            return;
        }

        if (result.AvatarDelta is { } avatarDelta && result.AvatarSelection is { } avatarSelection)
        {
            ImmutableArray<CalculateItem> items = OfflineCalculator.CalculateCustomAvatarConsumption(avatarDelta, avatarSelection);
            await SaveCustomMaterialsAsync(items, LevelInformation.From(avatarDelta), CultivateType.CustomAvatarMaterial).ConfigureAwait(false);
        }

        if (result.WeaponDelta is { } weaponDelta && result.WeaponSelection is { } weaponSelection)
        {
            ImmutableArray<CalculateItem> items = OfflineCalculator.CalculateCustomWeaponConsumption(weaponDelta, weaponSelection);
            await SaveCustomMaterialsAsync(items, LevelInformation.From(weaponDelta), CultivateType.CustomWeaponMaterial).ConfigureAwait(false);
        }

        await taskContext.SwitchToMainThreadAsync();
        messenger.Send(InfoBarMessage.Success(SH.ViewModelCultivationCustomMaterialAdded));
        await UpdateEntryCollectionAsync(Projects?.CurrentItem).ConfigureAwait(false);
    }

    private async ValueTask SaveCustomMaterialsAsync(ImmutableArray<CalculateItem> items, LevelInformation levelInformation, CultivateType type)
    {
        if (items.IsEmpty)
        {
            return;
        }

        await cultivationService.SaveConsumptionAsync(new InputConsumption
        {
            Type = type,
            ItemId = 0,
            Items = items,
            LevelInformation = levelInformation,
            Strategy = ConsumptionSaveStrategyKind.CreateNewEntry,
        }).ConfigureAwait(false);
    }

    [Command("RemoveProjectCommand")]
    private async Task RemoveProjectAsync(CultivateProject? project)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Remove project", "CultivationViewModel.Command"));

        if (project is null)
        {
            return;
        }

        ContentDialogResult result = await contentDialogFactory
            .CreateForConfirmCancelAsync(
                SH.ViewModelCultivationRemoveProjectTitle,
                SH.ViewModelCultivationRemoveProjectContent)
            .ConfigureAwait(false);

        if (result is not ContentDialogResult.Primary)
        {
            return;
        }

        await cultivationService.RemoveProjectAsync(project).ConfigureAwait(false);
        await taskContext.SwitchToMainThreadAsync();
        Projects?.MoveCurrentToFirst();
    }

    private async ValueTask UpdateEntryCollectionAsync(CultivateProject? project)
    {
        if (project is null)
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        EntriesUpdating = true;

        try
        {
            CultivationMetadataContext context = await metadataService
                .GetContextAsync<CultivationMetadataContext>()
                .ConfigureAwait(false);

            ObservableCollection<CultivateEntryView> entries = await cultivationService
                .GetCultivateEntryCollectionAsync(project, context)
                .ConfigureAwait(false);

            await taskContext.SwitchToMainThreadAsync();

            IAdvancedCollectionView<CultivateEntryView> entriesView = entries.AsAdvancedCollectionView();
            CultivateEntries = entriesView;

            // 条目变了，统计和背包都可能过期；只重建当前可见的那一页。
            statisticsDirty = true;
            inventoryDirty = true;

            await PublishVisibleTabAsync().ConfigureAwait(false);
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            EntriesUpdating = false;
        }
    }

    [Command("RemoveEntryCommand")]
    private async Task RemoveEntryAsync(CultivateEntryView? entry)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Remove entry", "CultivationViewModel.Command"));

        if (entry is not null)
        {
            ArgumentNullException.ThrowIfNull(CultivateEntries);
            CultivateEntries.Remove(entry);
            await cultivationService.RemoveCultivateEntryAsync(entry.EntryId).ConfigureAwait(false);
            await UpdateStatisticsItemsAsync().ConfigureAwait(false);
        }
    }

    [Command("FinishStateCommand")]
    private async Task UpdateFinishedStateAsync(CultivateItemView? item)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Toggle item finish state", "CultivationViewModel.Command"));

        if (item is not null)
        {
            item.IsFinished = !item.IsFinished;
            cultivationService.SaveCultivateItem(item);
            await UpdateStatisticsItemsAsync().ConfigureAwait(false);
        }
    }

    [Command("SaveInventoryItemCommand")]
    private async Task SaveInventoryItemAsync(InventoryItemView? inventoryItem)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Save inventory item", "CultivationViewModel.Command"));

        if (inventoryItem is not null)
        {
            inventoryService.SaveInventoryItem(inventoryItem);
            await UpdateStatisticsItemsAsync().ConfigureAwait(false);
        }
    }

    [Command("RefreshInventoryByEmbeddedYaeCommand")]
    private async Task RefreshInventoryByEmbeddedYaeAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh inventory", "CultivationViewModel.Command", [("source", "Embedded Yae")]));

        if (Projects?.CurrentItem is null || metadataContext is null)
        {
            return;
        }

        if (!await embeddedYaeGate.EnsureElevatedAsync().ConfigureAwait(false))
        {
            return;
        }

        if (!await embeddedYaeGate.EnsureConfirmedAsync().ConfigureAwait(false))
        {
            return;
        }

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            EmbeddedYaeLaunchExecutionViewModel viewModel = serviceProvider.GetRequiredService<EmbeddedYaeLaunchExecutionViewModel>();
            if (!await viewModel.InitializeAsync().ConfigureAwait(false))
            {
                return;
            }

            RefreshOptions options = RefreshOptions.CreateForEmbeddedYae(Projects.CurrentItem, yaeService, viewModel);
            await inventoryService.RefreshInventoryAsync(options).ConfigureAwait(false);

            await UpdateInventoryItemsAsync().ConfigureAwait(false);
            await UpdateStatisticsItemsAsync().ConfigureAwait(false);
        }
    }

    [Command("RefreshInventoryByCalculatorCommand")]
    private async Task RefreshInventoryByCalculatorAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh inventory", "CultivationViewModel.Command", [("source", "Web Calculator")]));

        if (Projects?.CurrentItem is null || metadataContext is null)
        {
            return;
        }

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            ContentDialog dialog = await contentDialogFactory
                .CreateForIndeterminateProgressAsync(SH.ViewModelCultivationRefreshInventoryProgress)
                .ConfigureAwait(false);

            using (await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
            {
                await inventoryService.RefreshInventoryAsync(RefreshOptions.CreateForWebCalculator(Projects.CurrentItem, metadataContext)).ConfigureAwait(false);

                await UpdateInventoryItemsAsync().ConfigureAwait(false);
                await UpdateStatisticsItemsAsync().ConfigureAwait(false);
            }
        }
    }

    [Command("RefreshInventoryByBackpackArchiveCommand")]
    private async Task RefreshInventoryByBackpackArchiveAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh inventory", "CultivationViewModel.Command", [("source", "Backpack Archive")]));

        if (Projects?.CurrentItem is null || metadataContext is null)
        {
            return;
        }

        BackpackArchivePickerDialog dialog = await contentDialogFactory.CreateInstanceAsync<BackpackArchivePickerDialog>(serviceProvider).ConfigureAwait(false);
        (bool isOk, BackpackArchive archive) = await dialog.GetSelectedArchiveAsync().ConfigureAwait(false);

        if (!isOk || archive is null)
        {
            return;
        }

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            ContentDialog waitDialog = await contentDialogFactory
                .CreateForIndeterminateProgressAsync(SH.ViewModelCultivationRefreshInventoryProgress)
                .ConfigureAwait(false);

            using (await contentDialogFactory.BlockAsync(waitDialog).ConfigureAwait(false))
            {
                ImmutableArray<BackpackItem> backpackItems = backpackService.GetBackpackItemImmutableArrayByArchiveId(archive.InnerId);
                inventoryService.SaveInventoryItemsFromBackpackArchive(Projects.CurrentItem, backpackItems, metadataContext);

                await UpdateInventoryItemsAsync().ConfigureAwait(false);
                await UpdateStatisticsItemsAsync().ConfigureAwait(false);
            }
        }
    }

    [Command("SyncAvatarInfoByHoyolabGameRecordCommand")]
    private async Task SyncAvatarInfoByHoyolabGameRecordAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Sync avatar info", "CultivationViewModel.Command", [("source", "Hoyolab game record")]));

        if (Projects?.CurrentItem is not { } project || metadataContext is not { } context || CultivateEntries is null)
        {
            return;
        }

        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            messenger.Send(InfoBarMessage.Warning(SH.MustSelectUserAndUid));
            return;
        }

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            ContentDialog progressDialog = await contentDialogFactory
                .CreateForIndeterminateProgressAsync(SH.ViewModelCultivationSyncAvatarInfoProgress)
                .ConfigureAwait(false);

            int updated = 0;
            int skipped = 0;

            using (await contentDialogFactory.BlockAsync(progressDialog).ConfigureAwait(false))
            {
                ImmutableArray<CultivateEntryView> entries = [.. CultivateEntries.Source];

                ImmutableArray<Model.Entity.AvatarInfo> avatarInfos = await avatarInfoRepositoryOperation
                    .UpdateDbAvatarInfosAsync(userAndUid, CancellationToken.None)
                    .ConfigureAwait(false);

                Dictionary<AvatarId, DetailedCharacter> characterMap = [];
                foreach (ref readonly Model.Entity.AvatarInfo avatarInfo in avatarInfos.AsSpan())
                {
                    if (avatarInfo.Info2 is { } character)
                    {
                        characterMap[character.Base.Id] = character;
                    }
                }

                if (characterMap.Count is 0)
                {
                    messenger.Send(InfoBarMessage.Information(SH.ViewModelCultivationSyncAvatarInfoNoCharacter));
                    return;
                }

                foreach (CultivateEntryView entryView in entries)
                {
                    if (entryView.Type is not Model.Entity.Primitive.CultivateType.AvatarAndSkill)
                    {
                        continue;
                    }

                    if (entryView.Entry.LevelInformation is not { } levelInfo)
                    {
                        ++skipped;
                        continue;
                    }

                    if (!characterMap.TryGetValue(entryView.Id, out DetailedCharacter? character))
                    {
                        ++skipped;
                        continue;
                    }

                    if (!context.IdAvatarMap.TryGetValue(entryView.Id, out Model.Metadata.Avatar.Avatar? avatar))
                    {
                        ++skipped;
                        continue;
                    }

                    if (avatar.SkillDepot.CompositeSkillsNoInherents is not [{ } skillA, { } skillE, { } skillQ, ..])
                    {
                        ++skipped;
                        continue;
                    }

                    if (levelInfo.AvatarLevelTo is 0U || levelInfo.SkillALevelTo is 0U || levelInfo.SkillELevelTo is 0U || levelInfo.SkillQLevelTo is 0U)
                    {
                        ++skipped;
                        continue;
                    }

                    // The game record skill id matches the metadata proud skill id.
                    FrozenDictionary<SkillId, SkillLevel> talents = character.Skills.ToFrozenDictionary(static s => s.SkillId, static s => s.Level);

                    if (!talents.TryGetValue(skillA.Id, out SkillLevel talentA)
                        || !talents.TryGetValue(skillE.Id, out SkillLevel talentE)
                        || !talents.TryGetValue(skillQ.Id, out SkillLevel talentQ))
                    {
                        ++skipped;
                        continue;
                    }

                    AvatarPromotionDelta delta = new()
                    {
                        AvatarId = avatar.Id,
                        AvatarLevelCurrent = Math.Min((uint)character.Base.Level, levelInfo.AvatarLevelTo),
                        AvatarLevelTarget = levelInfo.AvatarLevelTo,
                        AvatarPromoteLevel = character.Base.PromoteLevel,
                        SkillList =
                        [
                            new PromotionDelta { Id = skillA.GroupId, LevelCurrent = Math.Min((uint)talentA, levelInfo.SkillALevelTo), LevelTarget = levelInfo.SkillALevelTo },
                            new PromotionDelta { Id = skillE.GroupId, LevelCurrent = Math.Min((uint)talentE, levelInfo.SkillELevelTo), LevelTarget = levelInfo.SkillELevelTo },
                            new PromotionDelta { Id = skillQ.GroupId, LevelCurrent = Math.Min((uint)talentQ, levelInfo.SkillQLevelTo), LevelTarget = levelInfo.SkillQLevelTo },
                        ],
                    };

                    LevelInformation levelInformation = LevelInformation.From(delta);
                    if (IsSameLevelInformation(levelInfo, levelInformation))
                    {
                        ++skipped;
                        continue;
                    }

                    CalculateBatchConsumption batchConsumption = OfflineCalculator.CalculateWikiAvatarConsumption(delta, avatar);
                    if (batchConsumption.OverallConsume.IsEmpty)
                    {
                        ++skipped;
                        continue;
                    }

                    InputConsumption input = new()
                    {
                        Type = Model.Entity.Primitive.CultivateType.AvatarAndSkill,
                        ItemId = avatar.Id,
                        Items = batchConsumption.OverallConsume,
                        LevelInformation = levelInformation,
                        Strategy = ConsumptionSaveStrategyKind.OverwriteExisting,
                    };

                    await cultivationService.SaveConsumptionAsync(input).ConfigureAwait(false);
                    ++updated;
                }

                await UpdateEntryCollectionAsync(project).ConfigureAwait(false);
            }

            InfoBarMessage message = updated > 0
                ? InfoBarMessage.Success(SH.FormatViewModelCultivationSyncAvatarInfoCompleted(updated, skipped))
                : InfoBarMessage.Information(SH.ViewModelCultivationSyncAvatarInfoNoCharacter);

            messenger.Send(message);
        }
    }

    private static bool IsSameLevelInformation(CultivateEntryLevelInformation current, LevelInformation updated)
    {
        return current.AvatarLevelFrom == updated.AvatarLevelFrom
            && current.AvatarLevelTo == updated.AvatarLevelTo
            && current.AvatarIsPromoting == updated.AvatarIsPromoting
            && current.SkillALevelFrom == updated.SkillALevelFrom
            && current.SkillALevelTo == updated.SkillALevelTo
            && current.SkillELevelFrom == updated.SkillELevelFrom
            && current.SkillELevelTo == updated.SkillELevelTo
            && current.SkillQLevelFrom == updated.SkillQLevelFrom
            && current.SkillQLevelTo == updated.SkillQLevelTo;
    }

    [Command("ClearInventoryCommand")]
    private async Task ClearInventoryAsync(CultivateProject? project)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Clear inventory", "CultivationViewModel.Command"));

        if (project is null)
        {
            return;
        }

        ContentDialogResult result = await contentDialogFactory
            .CreateForConfirmCancelAsync(
                SH.ViewModelCultivationClearInventoryTitle,
                SH.ViewModelCultivationClearInventoryContent)
            .ConfigureAwait(false);

        if (result is not ContentDialogResult.Primary)
        {
            return;
        }

        ContentDialog dialog = await contentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ViewModelCultivationClearInventoryProgress)
            .ConfigureAwait(false);
        using (await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
        {
            inventoryService.RemoveInventoryItems(project);

            await UpdateInventoryItemsAsync().ConfigureAwait(false);
            await UpdateStatisticsItemsAsync().ConfigureAwait(false);
        }
    }

    [Command("RefreshStatisticsItemsCommand")]
    private async Task UpdateStatisticsItemsAsync()
    {
        statisticsDirty = true;
        await PublishVisibleTabAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 重建统计集合（串行 + 合并）。
    /// </summary>
    /// <remarks>
    /// 勾选完成状态、拖动库存数量框都会连续触发重建，而一次重建要重算 500 多条统计。
    /// 这里保证同一时刻只跑一个重建，期间到来的请求只记一次、跑完再补一轮：
    /// 既不会堆积上百个并发重建，最终结果也对应最后一次操作。
    /// </remarks>
    private async ValueTask RefreshStatisticsItemsCoreAsync()
    {
        Interlocked.Increment(ref statisticsRefreshQueued);
        await statisticsRefreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            while (Interlocked.Exchange(ref statisticsRefreshQueued, 0) > 0)
            {
                await BuildAndPublishStatisticsAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            statisticsRefreshGate.Release();
        }
    }

    private async ValueTask BuildAndPublishStatisticsAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh statistics items", "CultivationViewModel.Command"));

        await taskContext.SwitchToMainThreadAsync();

        // 离开统计页后不再白算；保留 dirty 标记，等用户切回来再算。
        if (!IsStatisticsVisible)
        {
            return;
        }

        ObservableCollection<StatisticsCultivateItem>? previous = StatisticsItems;

        if (Projects?.CurrentItem is not { } project)
        {
            StatisticsItems = null;
            ResinStatistics = null;
            statisticsDirty = false;
            return;
        }

        if (metadataContext is null)
        {
            return;
        }

        await taskContext.SwitchToBackgroundAsync();

        CancellationToken token = exclusiveTokenProvider.GetNewToken();
        StatisticsCultivateItemCollection statistics;
        ResinStatistics resinStatistics;
        try
        {
            statistics = await cultivationService.GetStatisticsCultivateItemCollectionAsync(project, metadataContext, token).ConfigureAwait(false);
            if (AutoSynthesisPreview)
            {
                statistics.AutoSynthesize(metadataContext.ResultMaterialIdCombineMap);
            }

            resinStatistics = await cultivationService.GetResinStatisticsAsync(statistics, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IncompleteFirst)
        {
            statistics.SortAsIncompleteFirst();
        }

        await taskContext.SwitchToMainThreadAsync();

        /* 内容与当前已发布的完全一致时直接跳过赋值。
           重新赋 ItemsSource 会让列表把已实例化的卡片全部拆掉重建、滚动位置被拉回顶部，
           而勾选一个完成状态其实并不会改变统计数字（Current 来自背包、Count 来自条目），
           这条路径就是最常见的情况。 */
        if (previous is not null && IsSameStatistics(previous, statistics))
        {
            statisticsDirty = false;
            return;
        }

        StatisticsItems = statistics.ToObservableCollection();
        ResinStatistics = resinStatistics;
        statisticsDirty = false;
    }

    private static bool IsSameStatistics(ObservableCollection<StatisticsCultivateItem> previous, StatisticsCultivateItemCollection next)
    {
        int index = 0;
        foreach (StatisticsCultivateItem item in next)
        {
            if (item.ExcludedFromPresentation)
            {
                continue;
            }

            if (index >= previous.Count)
            {
                return false;
            }

            StatisticsCultivateItem old = previous[index++];
            if (!old.Inner.Id.Equals(item.Inner.Id) || old.Count != item.Count || old.Current != item.Current)
            {
                return false;
            }
        }

        return index == previous.Count;
    }

    private async ValueTask UpdateInventoryItemsAsync()
    {
        inventoryDirty = true;
        await PublishVisibleTabAsync().ConfigureAwait(false);
    }

    private async ValueTask RefreshInventoryItemsCoreAsync()
    {
        if (Projects?.CurrentItem is not { } project || metadataContext is not { } context)
        {
            await taskContext.SwitchToMainThreadAsync();
            InventoryItems = [];
            inventoryDirty = false;
            return;
        }

        /* 这一步包含一次库存查询 + 对一万多个材料的枚举判定，原先整段跑在 UI 线程上，
           切到背包页或刷新库存时会明显卡一下。视图对象是纯 CLR 对象，构造放后台即可。 */
        await taskContext.SwitchToBackgroundAsync();
        ImmutableArray<InventoryItemView> items = inventoryService.GetInventoryItemViews(context, project, SaveInventoryItemCommand);

        await taskContext.SwitchToMainThreadAsync();
        InventoryItems = items;
        inventoryDirty = false;
    }

    [Command("ModifyEntryCommand")]
    private async Task ModifyEntryAsync(CultivateEntryView? entryView)
    {
        await taskContext.SwitchToMainThreadAsync();

        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Modify entry", "CultivationViewModel.Command"));

        if (entryView?.Entry is not { } entry)
        {
            return;
        }

        if (metadataContext is null || Projects?.CurrentItem is null)
        {
            return;
        }

        AvatarPromotionDelta delta = new();

        switch (entry.Type)
        {
            case Model.Entity.Primitive.CultivateType.AvatarAndSkill:
                {
                    if (entryView.Avatar is not { } calculableAvatar)
                    {
                        return;
                    }

                    delta.AvatarId = calculableAvatar.AvatarId;
                    delta.AvatarLevelCurrent = Math.Clamp(calculableAvatar.LevelCurrent, calculableAvatar.LevelMin, calculableAvatar.LevelMax);
                    delta.AvatarLevelTarget = Math.Clamp(calculableAvatar.LevelTarget, calculableAvatar.LevelMin, calculableAvatar.LevelMax);
                    delta.AvatarPromoteLevel = calculableAvatar.PromoteLevel;
                    delta.SkillList = calculableAvatar.Skills.SelectAsArray(static skill => new PromotionDelta
                    {
                        Id = skill.GroupId,
                        LevelCurrent = Math.Clamp(skill.LevelCurrent, skill.LevelMin, skill.LevelMax),
                        LevelTarget = Math.Clamp(skill.LevelTarget, skill.LevelMin, skill.LevelMax),
                    });

                    break;
                }

            case Model.Entity.Primitive.CultivateType.Weapon:
                {
                    if (entryView.Weapon is not { } calculableWeapon)
                    {
                        return;
                    }

                    delta.Weapon = new PromotionDelta
                    {
                        Id = calculableWeapon.WeaponId,
                        LevelCurrent = Math.Clamp(calculableWeapon.LevelCurrent, calculableWeapon.LevelMin, calculableWeapon.LevelMax),
                        LevelTarget = Math.Clamp(calculableWeapon.LevelTarget, calculableWeapon.LevelMin, calculableWeapon.LevelMax),
                        WeaponPromoteLevel = calculableWeapon.PromoteLevel,
                    };

                    break;
                }

            default:
                return;
        }

        CalculateBatchConsumption batchConsumption;
        InputConsumption input;

        switch (entry.Type)
        {
            case Model.Entity.Primitive.CultivateType.AvatarAndSkill:
                {
                    Model.Metadata.Avatar.Avatar avatar = metadataContext.IdAvatarMap[entry.Id];

                    batchConsumption = OfflineCalculator.CalculateWikiAvatarConsumption(delta, avatar);

                    if (batchConsumption.OverallConsume.IsEmpty)
                    {
                        messenger.Send(InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddNoConsumptionWarning));
                        return;
                    }

                    input = new()
                    {
                        Type = Model.Entity.Primitive.CultivateType.AvatarAndSkill,
                        ItemId = avatar.Id,
                        Items = batchConsumption.OverallConsume,
                        LevelInformation = LevelInformation.From(delta),
                        Strategy = ConsumptionSaveStrategyKind.OverwriteExisting,
                    };

                    break;
                }

            case Model.Entity.Primitive.CultivateType.Weapon:
                {
                    Model.Metadata.Weapon.Weapon weapon = metadataContext.IdWeaponMap[entry.Id];

                    batchConsumption = OfflineCalculator.CalculateWikiWeaponConsumption(delta, weapon);

                    if (batchConsumption.OverallConsume.IsEmpty)
                    {
                        messenger.Send(InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddNoConsumptionWarning));
                        return;
                    }

                    input = new()
                    {
                        Type = Model.Entity.Primitive.CultivateType.Weapon,
                        ItemId = weapon.Id,
                        Items = batchConsumption.OverallConsume,
                        LevelInformation = LevelInformation.From(delta),
                        Strategy = ConsumptionSaveStrategyKind.OverwriteExisting,
                    };

                    break;
                }

            default:
                return;
        }

        ConsumptionSaveResultKind result = await cultivationService.SaveConsumptionAsync(input).ConfigureAwait(false);

        InfoBarMessage? message = result switch
        {
            ConsumptionSaveResultKind.NoProject => InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning),
            ConsumptionSaveResultKind.NoItem => InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveNoItemHint),
            ConsumptionSaveResultKind.Added => InfoBarMessage.Success(SH.ViewModelCultivationEntryModifySuccess),
            _ => null,
        };

        await taskContext.SwitchToMainThreadAsync();

        if (message is not null)
        {
            messenger.Send(message);
        }

        CultivateProject? currentProject = Projects?.CurrentItem;

        await UpdateEntryCollectionAsync(currentProject).ConfigureAwait(false);
    }

    [Command("NavigateToPageCommand")]
    private void NavigateToPage(string? typeString)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI($"Navigate to {typeString}", "CultivationViewModel.Command"));

        if (typeString is not null)
        {
            Type? pageType = Type.GetType(typeString);
            ArgumentNullException.ThrowIfNull(pageType);
            navigationService.Navigate(pageType, NavigationExtraData.Default, true);
        }
    }

    [Command("FilterCommand")]
    private void ApplyFilter()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Filter", "CultivationViewModel.Command"));

        if (CultivateEntries is null || metadataContext is null)
        {
            return;
        }

        CultivateEntries.Filter = CultivateEntryViewFilter.Compile(SearchData, metadataContext);
        CultivateEntries.Refresh();
    }
}