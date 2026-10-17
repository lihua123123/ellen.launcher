// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Factory.Progress;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.ZZZ;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.GachaLog.Factory;
using Ellen.Launcher.Service.GachaLog.QueryProvider;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.UIGF;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using Ellen.Launcher.Web.Endpoint.Upstream;
using Ellen.Launcher.Web.Upstream.GachaLog;
using Ellen.Launcher.Win32.Foundation;
using Ellen.Launcher.Core.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.ViewModel.GachaLog;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class GachaLogViewModel : Abstraction.ViewModel
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly IProgressFactory progressFactory;
    private readonly IGachaLogService gachaLogService;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;
    private readonly IFileSystemPickerInteraction fileSystemPickerInteraction;
    private readonly IGachaLogRepository gachaLogRepository;
    private readonly IUIGFService uigfService;
    private readonly IZZZGachaItemMetadataService zzzGachaItemMetadataService;
    private readonly IZZZGachaBannerService zzzGachaBannerService;

    private bool suppressCurrentItemChangedHandling;
    private GachaLogServiceMetadataContext? metadataContext;
    private AppOptions? appOptions;

    [GeneratedConstructor]
    public partial GachaLogViewModel(IServiceProvider serviceProvider);

    public partial WishCountdownViewModel WishCountdownViewModel { get; }

    /// <summary>
    /// 可切换的游戏（原神 / 绝区零）
    /// </summary>
    public ImmutableArray<NameValue<GachaGameType>> Games { get; } =
    [
        new(SH.ViewPageGachaLogGameGenshin, GachaGameType.Genshin),
        new(SH.ViewPageGachaLogGameZZZ, GachaGameType.ZZZ),
    ];

    /// <summary>
    /// 当前选中的游戏
    /// </summary>
    [ObservableProperty]
    public partial NameValue<GachaGameType> SelectedGame { get; set; } = new(SH.ViewPageGachaLogGameGenshin, GachaGameType.Genshin);

    /// <summary>
    /// 是否处于绝区零模式
    /// </summary>
    [ObservableProperty]
    public partial bool IsZZZMode { get; set; }

    public IAdvancedDbCollectionView<GachaArchive>? Archives
    {
        get;
        set
        {
            AdvancedCollectionViewCurrentChanged.Detach(field, OnCurrentArchiveChanged);
            SetProperty(ref field, value);
            AdvancedCollectionViewCurrentChanged.Attach(field, OnCurrentArchiveChanged);
        }
    }

    public GachaStatistics? Statistics
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                field?.HistoryWishes.MoveCurrentToFirst();
                UpdateCardVisibility();
            }
        }
    }

    [ObservableProperty]
    public partial bool IsAggressiveRefresh { get; set; }

    [ObservableProperty]
    public partial bool IsBeyondMode { get; set; }

    private AppOptions AppOptions => appOptions ??= serviceProvider.GetRequiredService<AppOptions>();

    [ObservableProperty]
    public partial UIGFVersion SelectedUIGFVersion { get; set; } = UIGFVersion.UIGF42;

    public ImmutableArray<UIGFVersion> UIGFVersions { get; } =
    [
        UIGFVersion.UIGF22,
        UIGFVersion.UIGF23,
        UIGFVersion.UIGF24,
        UIGFVersion.UIGF30,
        UIGFVersion.UIGF40,
        UIGFVersion.UIGF41,
        UIGFVersion.UIGF42,
    ];

    [ObservableProperty]
    public partial bool IsAvatarWishCardVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsWeaponWishCardVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsStandardWishCardVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsChronicledWishCardVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsNoviceWishCardVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBeyondStandardWishCardVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBeyondEventWishCardVisible { get; set; } = true;

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        try
        {
            if (!await metadataService.InitializeAsync().ConfigureAwait(false))
            {
                return false;
            }

            metadataContext = await metadataService.GetContextAsync<GachaLogServiceMetadataContext>(token).ConfigureAwait(false);
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                IAdvancedDbCollectionView<GachaArchive> archives = await gachaLogService.GetArchiveCollectionAsync(SelectedGame.Value).ConfigureAwait(false);
                await taskContext.SwitchToMainThreadAsync();
                Archives = archives;
                Archives.MoveCurrentTo(Archives.Source.SelectedOrFirstOrDefault());
            }

            // When `Archives.CurrentItem` is not null, the `Initialization` actually completed in
            // `UpdateStatisticsAsync`, so we return false to make the view hide until the actual
            // initialization is complete. But we return true when no archives are available,
            // so that the empty view can show up.
            if (Archives.CurrentItem is null)
            {
                return true;
            }
        }
        catch (OperationCanceledException)
        {
        }

        AppOptions.IsEmptyOverviewVisible.PropertyChanged += OnIsEmptyOverviewVisibleChanged;

        return false;
    }

    protected override void UninitializeOverride()
    {
        if (appOptions is not null)
        {
            appOptions.IsEmptyOverviewVisible.PropertyChanged -= OnIsEmptyOverviewVisibleChanged;
        }

        using (Archives?.SuppressChangeCurrentItem())
        {
            Archives = default;
        }
    }

    private void OnCurrentArchiveChanged(object? sender, object? e)
    {
        if (suppressCurrentItemChangedHandling)
        {
            return;
        }

        UpdateStatisticsAsync(Archives?.CurrentItem).SafeForget();
    }

    /// <summary>
    /// 切换游戏：换用对应游戏的存档集合，并复位当前存档
    /// </summary>
    partial void OnSelectedGameChanged(NameValue<GachaGameType> value)
    {
        IsZZZMode = value.Value is GachaGameType.ZZZ;
        if (IsZZZMode && IsBeyondMode)
        {
            // 千星奇域（UGC）模式仅适用于原神
            IsBeyondMode = false;
        }

        SwitchGameAsync(value.Value).SafeForget();
    }

    private async ValueTask SwitchGameAsync(GachaGameType game)
    {
        try
        {
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                suppressCurrentItemChangedHandling = true;

                IAdvancedDbCollectionView<GachaArchive> archives = await gachaLogService.GetArchiveCollectionAsync(game).ConfigureAwait(false);
                await taskContext.SwitchToMainThreadAsync();

                Archives = archives;
                Archives.MoveCurrentTo(archives.Source.SelectedOrFirstOrDefault());

                GachaArchive? current = Archives.CurrentItem;
                UpdateStatisticsAsync(current).SafeForget();
            }
        }
        finally
        {
            suppressCurrentItemChangedHandling = false;
        }
    }

    [Command("RefreshByWebCacheCommand")]
    private async Task RefreshByWebCacheAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh gachalog", "GachaLogViewModel.Command", [("source", "WebCache")]));

        await PrivateRefreshAsync(RefreshOptionKind.WebCache).ConfigureAwait(false);
    }

    [Command("RefreshBySTokenCommand")]
    private async Task RefreshBySTokenAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh gachalog", "GachaLogViewModel.Command", [("source", "SToken")]));

        await PrivateRefreshAsync(RefreshOptionKind.SToken).ConfigureAwait(false);
    }

    [Command("RefreshByManualInputCommand")]
    private async Task RefreshByManualInputAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Refresh gachalog", "GachaLogViewModel.Command", [("source", "Manual Input")]));

        await PrivateRefreshAsync(RefreshOptionKind.ManualInput).ConfigureAwait(false);
    }

    [Command("SwitchToBeyondModeCommand")]
    private void SwitchToBeyondMode()
    {
        IsBeyondMode = true;
        UpdateStatisticsAsync(Archives?.CurrentItem).SafeForget();
    }

    [Command("SwitchToNormalModeCommand")]
    private void SwitchToNormalMode()
    {
        IsBeyondMode = false;
        UpdateStatisticsAsync(Archives?.CurrentItem).SafeForget();
    }

    partial void OnIsBeyondModeChanged(bool value)
    {
        UpdateCardVisibility();
        UpdateStatisticsAsync(Archives?.CurrentItem).SafeForget();
    }

    private void OnIsEmptyOverviewVisibleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Value")
        {
            UpdateCardVisibility();
        }
    }

    private void UpdateCardVisibility()
    {
        if (Statistics is null)
        {
            return;
        }

        bool showEmpty = AppOptions.IsEmptyOverviewVisible.Value;

        IsAvatarWishCardVisible = !IsBeyondMode && (showEmpty || Statistics.AvatarWish.TotalCount > 0);
        IsWeaponWishCardVisible = !IsBeyondMode && (showEmpty || Statistics.WeaponWish.TotalCount > 0);
        IsStandardWishCardVisible = !IsBeyondMode && (showEmpty || Statistics.StandardWish.TotalCount > 0);
        IsChronicledWishCardVisible = !IsBeyondMode && (showEmpty || Statistics.ChronicledWish.TotalCount > 0);
        IsNoviceWishCardVisible = !IsBeyondMode && (showEmpty || Statistics.NoviceWish.TotalCount > 0);

        IsBeyondStandardWishCardVisible = IsBeyondMode && (showEmpty || Statistics.BeyondStandardWish.TotalCount > 0);
        IsBeyondEventWishCardVisible = IsBeyondMode && (showEmpty || Statistics.BeyondEventWish.TotalCount > 0);
    }

    private async ValueTask PrivateRefreshAsync(RefreshOptionKind optionKind)
    {
        GachaLogQuery query;
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            IGachaLogQueryProvider provider = scope.ServiceProvider.GetRequiredKeyedService<IGachaLogQueryProvider>(optionKind);
            (bool isOk, query) = await provider.GetQueryAsync(SelectedGame.Value).ConfigureAwait(false);

            if (!isOk)
            {
                if (!string.IsNullOrEmpty(query.Message))
                {
                    messenger.Send(InfoBarMessage.Warning(query.Message));
                }

                return;
            }
        }

        RefreshStrategyKind strategy = IsAggressiveRefresh ? RefreshStrategyKind.AggressiveMerge : RefreshStrategyKind.LazyMerge;

        GachaLogRefreshProgressDialog dialog;
        try
        {
            dialog = await contentDialogFactory.CreateInstanceAsync<GachaLogRefreshProgressDialog>(serviceProvider).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Previous query provider operation toke too long, and the service provider is disposed.
            // For example, the SToken query provider can take a long time to perform a network request.
            return;
        }

        BlockDeferral hideToken;
        try
        {
            hideToken = await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false);
        }
        catch (COMException ex)
        {
            if (ex.HResult is HRESULT.E_ASYNC_OPERATION_NOT_STARTED)
            {
                messenger.Send(InfoBarMessage.Error(ex));
                return;
            }

            throw;
        }

        IProgress<GachaLogFetchStatus> progress = progressFactory.CreateForMainThread<GachaLogFetchStatus>(dialog.OnReport);
        bool authkeyValid;

        try
        {
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                try
                {
                    try
                    {
                        suppressCurrentItemChangedHandling = true;
                        ArgumentNullException.ThrowIfNull(metadataContext);
                        
                        if (IsBeyondMode)
                        {
                            authkeyValid = await gachaLogService.RefreshBeyondGachaLogAsync(metadataContext, query, strategy, progress, CancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            authkeyValid = await gachaLogService.RefreshGachaLogAsync(metadataContext, query, strategy, progress, CancellationToken).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        suppressCurrentItemChangedHandling = false;
                        await UpdateStatisticsAsync(Archives?.CurrentItem).ConfigureAwait(false);
                    }
                }
                catch (EllenException ex)
                {
                    authkeyValid = false;
                    messenger.Send(InfoBarMessage.Error(ex));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // We set true here in order to hide the dialog.
            authkeyValid = true;
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelGachaLogRefreshOperationCancel));
        }

        await taskContext.SwitchToMainThreadAsync();
        if (authkeyValid)
        {
            hideToken.Dispose();
        }
        else
        {
            // User needs to manually close the dialog
            dialog.Title = SH.ViewModelGachaLogRefreshFail;
            dialog.PrimaryButtonText = SH.ContentDialogConfirmPrimaryButtonText;
            dialog.DefaultButton = ContentDialogButton.Primary;
        }
    }

    [Command("RemoveArchiveCommand")]
    private async Task RemoveArchiveAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Remove archive", "GachaLogViewModel.Command"));

        if (Archives?.CurrentItem is null)
        {
            return;
        }

        ContentDialogResult result = await contentDialogFactory
            .CreateForConfirmCancelAsync(
                SH.FormatViewModelGachaLogRemoveArchiveTitle(Archives.CurrentItem.Uid),
                SH.ViewModelGachaLogRemoveArchiveDescription)
            .ConfigureAwait(false);

        if (result is not ContentDialogResult.Primary)
        {
            return;
        }

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            await gachaLogService.RemoveArchiveAsync(Archives.CurrentItem).ConfigureAwait(false);
        }
    }

    [Command("ImportUIGFJsonCommand")]
    private async Task ImportUIGFJsonAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Import UIGF file", "GachaLogViewModel.Command"));

        FileSystemPickerOptions pickerOptions = new()
        {
            Title = SH.ViewModelGachaUIGFImportPickerTitile,
            FilterName = SH.ViewModelGachaLogExportFileType,
            FilterType = "*.json",
        };

        if (fileSystemPickerInteraction.PickFile(pickerOptions) is not (true, { HasValue: true } file))
        {
            return;
        }

        if (!uigfService.Parse(await File.ReadAllTextAsync(file).ConfigureAwait(false), out UIGF4? uigf))
        {
            messenger.Send(InfoBarMessage.Error(SH.ViewModelImportWarningTitle, SH.ViewModelImportWarningMessage));
            return;
        }

        bool hasGenshin = !uigf!.Hk4e.IsDefaultOrEmpty;
        bool hasZZZ = uigf is UIGF42 uigf42 && !uigf42.Nap.IsDefaultOrEmpty;

        // UIGF v4.2 的绝区零记录位于 nap 字段；原神记录位于 hk4e 字段。两者任一存在即可导入。
        if (!hasGenshin && !hasZZZ)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportNoHk4eEntry));
            return;
        }

        if (uigf.Hk4e.Select(entry => entry.Uid).ToHashSet().Count != uigf.Hk4e.Length)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportDuplicatedHk4eEntry));
            return;
        }

        UIGFImportDialog importDialog = await contentDialogFactory.CreateInstanceAsync<UIGFImportDialog>(serviceProvider, uigf).ConfigureAwait(false);
        if (await importDialog.GetSelectedUidsAsync().ConfigureAwait(false) is not (true, { } uids))
        {
            return;
        }

        if (uids is null or { Count: 0 })
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelUIGFImportNoSelectedEntry));
            return;
        }

        UIGFImportOptions options = new()
        {
            UIGF = uigf,
            GachaArchiveUids = uids,
        };

        ContentDialog dialog = await contentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ViewModelUIGFImportingProgressTitle)
            .ConfigureAwait(false);

        using (await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
        {
            try
            {
                await uigfService.ImportAsync(options).ConfigureAwait(false);
                messenger.Send(InfoBarMessage.Success(SH.ViewModelUIGFImportSuccess));
                await ReloadArchivesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                messenger.Send(InfoBarMessage.Error(SH.ViewModelUIGFImportError, ex));
            }
        }
    }

    [Command("ExportUIGFJsonCommand")]
    private async Task ExportUIGFJsonAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Export UIGF file", "GachaLogViewModel.Command"));

        FileSystemPickerOptions pickerOptions = new()
        {
            Title = SH.ViewModelGachaLogUIGFExportPickerTitle,
            DefaultFileName = $"Ellen Launcher {SelectedUIGFVersion}.json",
            FilterName = SH.ViewModelGachaLogExportFileType,
            FilterType = "*.json",
        };

        if (fileSystemPickerInteraction.SaveFile(pickerOptions) is not (true, { HasValue: true } file))
        {
            return;
        }

        ImmutableArray<uint> allUids = [.. gachaLogRepository.GetGachaArchiveUidImmutableArray().Select(uint.Parse)];
        bool isLegacyVersion = SelectedUIGFVersion is UIGFVersion.UIGF22 or UIGFVersion.UIGF23 or UIGFVersion.UIGF24 or UIGFVersion.UIGF30;
        UIGFExportDialog exportDialog = await contentDialogFactory.CreateInstanceAsync<UIGFExportDialog>(serviceProvider, allUids, isLegacyVersion).ConfigureAwait(false);
        if (await exportDialog.GetSelectedUidsAsync().ConfigureAwait(false) is not (true, { } uids))
        {
            return;
        }

        UIGFExportOptions options = new()
        {
            FilePath = file,
            GachaArchiveUids = uids,
            Version = SelectedUIGFVersion,
        };

        try
        {
            await uigfService.ExportAsync(options).ConfigureAwait(false);
            messenger.Send(InfoBarMessage.Success(SH.ViewModelUIGFExportSuccess));
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(SH.ViewModelUIGFExportError, ex));
        }
    }

    private async ValueTask ReloadArchivesAsync()
    {
        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            // UIGF 导入是通过独立作用域写入数据库的，缓存里的存档集合快照不会包含新行，
            // 因此这里必须强制重新查询，否则会出现「提示导入成功但列表仍为空」。
            IAdvancedDbCollectionView<GachaArchive> archives = await gachaLogService.ReloadArchiveCollectionAsync(SelectedGame.Value).ConfigureAwait(false);
            await taskContext.SwitchToMainThreadAsync();
            Archives = archives;
            Archives.MoveCurrentTo(Archives.Source.SelectedOrFirstOrDefault());
        }
    }

    private async ValueTask UpdateStatisticsAsync(GachaArchive? archive)
    {
        if (archive is null)
        {
            Statistics = default;
            await WishCountdownViewModel.UpdateAsync(null).ConfigureAwait(false);
            return;
        }

        if (archive.Game is GachaGameType.ZZZ)
        {
            // 绝区零：复用原神的 TypedWishSummaryBuilder 管线产出 GachaStatistics，
            // 这样总览页的 StatisticsCard 直接复用，无需另写 UI。
            // 映射：常驻→StandardWish, 独家+独家重映→AvatarWish, 音擎+音擎回响→WeaponWish, 邦布→ChronicledWish
            GachaStatistics zzzStatistics = await BuildZZZStatisticsAsync(archive).ConfigureAwait(false);
            await taskContext.SwitchToMainThreadAsync();
            Statistics = zzzStatistics;
            UpdateCardVisibility();
            IsInitialized = true;
            await WishCountdownViewModel.UpdateAsync(archive).ConfigureAwait(false);
            return;
        }

        try
        {
            ArgumentNullException.ThrowIfNull(metadataContext);
            
            GachaStatistics statistics;
            if (IsBeyondMode)
            {
                statistics = await gachaLogService.GetBeyondStatisticsAsync(metadataContext, archive).ConfigureAwait(false);
            }
            else
            {
                statistics = await gachaLogService.GetStatisticsAsync(metadataContext, archive).ConfigureAwait(false);
            }
            
            await taskContext.SwitchToMainThreadAsync();
            Statistics = statistics;
            IsInitialized = true;
        }
        catch (EllenException ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }

        await WishCountdownViewModel.UpdateAsync(archive).ConfigureAwait(false);
    }

    /// <summary>
    /// 用原神的 TypedWishSummaryBuilder 管线为绝区零产出 GachaStatistics，
    /// 这样总览页的 StatisticsCard 直接复用。ZZZ 物品不依赖原神元数据，
    /// 名称与稀有度从 GachaItem.Name/Quality 取，图标由随包物品元数据补齐。
    /// 映射：常驻频段→StandardWish, 独家频段+独家重映→AvatarWish,
    /// 音擎频段+音擎回响→WeaponWish, 邦布频段→ChronicledWish
    /// </summary>
    private async ValueTask<GachaStatistics> BuildZZZStatisticsAsync(GachaArchive archive)
    {
        await taskContext.SwitchToBackgroundAsync();

        ImmutableArray<GachaItem> items = gachaLogRepository.GetGachaItemImmutableArrayByArchiveId(archive.InnerId);
        HashSet<uint> standardItemIds = ZZZGachaUpHelper.GetStandardItemIds(items);

        TypedWishSummaryBuilderContext standardCtx = new(serviceProvider, SH.WebGachaConfigTypeZZZStandardChannel, 90, 10, 0,
            static type => type is GachaType.ZZZStandardChannel, GachaDistributionType.Standard);
        TypedWishSummaryBuilderContext avatarCtx = new(serviceProvider, SH.WebGachaConfigTypeZZZExclusiveChannel, 90, 10, 0,
            static type => type is GachaType.ZZZExclusiveChannel or GachaType.ZZZExclusiveRescreening, GachaDistributionType.AvatarEvent);
        TypedWishSummaryBuilderContext weaponCtx = new(serviceProvider, SH.WebGachaConfigTypeZZZWEngineChannel, 80, 10, 0,
            static type => type is GachaType.ZZZWEngineChannel or GachaType.ZZZWEngineReverberation, GachaDistributionType.WeaponEvent);
        TypedWishSummaryBuilderContext chronicledCtx = new(serviceProvider, SH.WebGachaConfigTypeZZZBangbooChannel, 90, 10, 0,
            static type => type is GachaType.ZZZBangbooChannel, GachaDistributionType.Chronicled);
        TypedWishSummaryBuilderContext noviceCtx = new(serviceProvider, string.Empty, 90, 10, 0,
            static _ => false, GachaDistributionType.Standard);
        TypedWishSummaryBuilderContext beyondStandardCtx = new(serviceProvider, string.Empty, int.MaxValue, 90, 10,
            static _ => false, GachaDistributionType.BeyondStandard, true, true);
        TypedWishSummaryBuilderContext beyondEventCtx = new(serviceProvider, string.Empty, 90, 10, 0,
            static _ => false, GachaDistributionType.BeyondEvent, true);

        TypedWishSummaryBuilder standardBuilder = standardCtx.CreateBuilder();
        TypedWishSummaryBuilder avatarBuilder = avatarCtx.CreateBuilder();
        TypedWishSummaryBuilder weaponBuilder = weaponCtx.CreateBuilder();
        TypedWishSummaryBuilder chronicledBuilder = chronicledCtx.CreateBuilder();
        TypedWishSummaryBuilder noviceBuilder = noviceCtx.CreateBuilder();
        TypedWishSummaryBuilder beyondStandardBuilder = beyondStandardCtx.CreateBuilder();
        TypedWishSummaryBuilder beyondEventBuilder = beyondEventCtx.CreateBuilder();

        // 统计页的「角色 / 武器」头像墙：按物品聚合获取次数。
        Dictionary<uint, StatisticsItem> agentItems = [];
        Dictionary<uint, StatisticsItem> otherItems = [];

        foreach (GachaItem item in items.OrderBy(static i => i.Id))
        {
            ZZZGachaItemInfo? info = zzzGachaItemMetadataService.GetById(item.ItemId);
            ZZZSummaryItemConvertible source = new(item, info);
            bool isUp = ZZZGachaUpHelper.IsUp(item, standardItemIds);

            standardBuilder.Track(item, source, isUp);
            avatarBuilder.Track(item, source, isUp);
            weaponBuilder.Track(item, source, isUp);
            chronicledBuilder.Track(item, source, isUp);
            noviceBuilder.Track(item, source, isUp);
            beyondStandardBuilder.Track(item, source, isUp);
            beyondEventBuilder.Track(item, source, isUp);

            TrackZZZStatisticsItem(item, info, agentItems, otherItems);
        }

        AsyncBarrier barrier = new(7);
        TypedWishSummary standardWish = standardBuilder.ToTypedWishSummary(barrier);
        TypedWishSummary avatarWish = avatarBuilder.ToTypedWishSummary(barrier);
        TypedWishSummary weaponWish = weaponBuilder.ToTypedWishSummary(barrier);
        TypedWishSummary chronicledWish = chronicledBuilder.ToTypedWishSummary(barrier);
        TypedWishSummary noviceWish = noviceBuilder.ToTypedWishSummary(barrier);
        TypedWishSummary beyondStandardWish = beyondStandardBuilder.ToTypedWishSummary(barrier);
        TypedWishSummary beyondEventWish = beyondEventBuilder.ToTypedWishSummary(barrier);

        ApplyZZZQualityLabels(standardWish);
        ApplyZZZQualityLabels(avatarWish);
        ApplyZZZQualityLabels(weaponWish);
        ApplyZZZQualityLabels(chronicledWish);
        ApplyZZZQualityLabels(noviceWish);
        ApplyZZZQualityLabels(beyondStandardWish);
        ApplyZZZQualityLabels(beyondEventWish);

        return new GachaStatistics
        {
            StandardWish = standardWish,
            AvatarWish = avatarWish,
            WeaponWish = weaponWish,
            ChronicledWish = chronicledWish,
            NoviceWish = noviceWish,
            BeyondStandardWish = beyondStandardWish,
            BeyondEventWish = beyondEventWish,
            HistoryWishes = BuildZZZHistoryWishes(items).AsAdvancedCollectionView(),
            OrangeAvatars = SelectZZZStatisticsItems(agentItems, QualityType.QUALITY_ORANGE),
            PurpleAvatars = SelectZZZStatisticsItems(agentItems, QualityType.QUALITY_PURPLE),
            OrangeWeapons = SelectZZZStatisticsItems(otherItems, QualityType.QUALITY_ORANGE),
            PurpleWeapons = SelectZZZStatisticsItems(otherItems, QualityType.QUALITY_PURPLE),
            BlueWeapons = SelectZZZStatisticsItems(otherItems, QualityType.QUALITY_BLUE),
        };
    }

    /// <summary>
    /// 按物品聚合绝区零调频记录，供统计页头像墙使用。
    /// 代理人（含常驻）进角色页，音擎与邦布进武器页。
    /// </summary>
    private static void TrackZZZStatisticsItem(GachaItem item, ZZZGachaItemInfo? info, Dictionary<uint, StatisticsItem> agents, Dictionary<uint, StatisticsItem> others)
    {
        QualityType quality = (QualityType)item.Quality;
        if (quality is QualityType.QUALITY_NONE)
        {
            quality = info?.Quality ?? QualityType.QUALITY_NONE;
        }

        if (quality is not (QualityType.QUALITY_ORANGE or QualityType.QUALITY_PURPLE or QualityType.QUALITY_BLUE))
        {
            return;
        }

        bool isAgent = info?.IsAgent ?? item.ItemId < 10000U;
        Dictionary<uint, StatisticsItem> target = isAgent ? agents : others;

        if (target.TryGetValue(item.ItemId, out StatisticsItem? existing))
        {
            ++existing.Count;
            return;
        }

        target[item.ItemId] = new StatisticsItem
        {
            Id = item.ItemId,
            Name = item.Name ?? info?.Name ?? string.Empty,
            Icon = info?.IconUri ?? StaticResourcesEndpoints.UIIconNone,
            Quality = quality,
            Count = 1,
        };
    }

    private static ImmutableArray<StatisticsItem> SelectZZZStatisticsItems(Dictionary<uint, StatisticsItem> source, QualityType quality)
    {
        return
        [
            .. source.Values
                .Where(item => item.Quality == quality)
                .OrderByDescending(static item => item.Count)
                .ThenBy(static item => item.Name, StringComparer.Ordinal),
        ];
    }

    private static void ApplyZZZQualityLabels(TypedWishSummary summary)
    {
        summary.OrangeQualityText = SH.ViewControlStatisticsCardOrangeQualityZZZ;
        summary.PurpleQualityText = SH.ViewControlStatisticsCardPurpleQualityZZZ;
        summary.BlueQualityText = SH.ViewControlStatisticsCardBlueQualityZZZ;
    }

    /// <summary>
    /// 用随包档期快照把调频记录按卡池分组，产出与「历史」页签相同结构的 HistoryWish。
    /// 快照里每个条目就是一个独立卡池，同期开放的两个频段各自成组（与原神一致）。
    /// 时间比较使用墙钟（<see cref="DateTimeOffset.DateTime"/>），
    /// 避免 UIGF 导入记录（+00:00）与实抓记录（+08:00）的偏移差异把记录分到错误的卡池。
    /// </summary>
    private ImmutableArray<HistoryWish> BuildZZZHistoryWishes(ImmutableArray<GachaItem> items)
    {
        ImmutableArray<ZZZGachaBanner> banners = zzzGachaBannerService.Banners;
        if (banners.IsDefaultOrEmpty || items.IsDefaultOrEmpty)
        {
            return [];
        }

        bool showEmpty = AppOptions.IsEmptyHistoryWishVisible.Value;
        HashSet<Guid> matchedItemIds = [];
        List<HistoryWish> histories = [];

        foreach (IGrouping<int, GachaItem> group in items.GroupBy(static item => (int)item.GachaType))
        {
            // 历史页只保留限定卡池（独家 / 独家重映 / 音擎 / 音擎回响），
            // 常驻与邦布不进入历史。
            if (group.Key is 1 or 5)
            {
                continue;
            }

            ZZZGachaBanner[] typeBanners =
            [
                .. banners
                    .Where(banner => banner.Type == group.Key)
                    .OrderBy(static banner => banner.Start),
            ];

            foreach ((ZZZGachaBanner banner, List<GachaItem> records) in AssignZZZRecords(typeBanners, group))
            {
                foreach (GachaItem item in records)
                {
                    matchedItemIds.Add(item.InnerId);
                }

                if (records.Count == 0 && !showEmpty)
                {
                    continue;
                }

                histories.Add(BuildZZZHistoryWish(banner, records));
            }

            // 快照未覆盖的记录（例如新版本刚上线的卡池）单独成组，避免历史页丢数据。
            List<GachaItem> unmatched = [.. group.Where(item => !matchedItemIds.Contains(item.InnerId))];
            if (unmatched.Count > 0)
            {
                ZZZGachaBanner fallback = new()
                {
                    Type = group.Key,
                    Start = unmatched.Min(static item => item.Time),
                    End = unmatched.Max(static item => item.Time),
                };

                histories.Add(BuildZZZHistoryWish(fallback, unmatched));
            }
        }

        return
        [
            .. histories
                .OrderByDescending(static history => history.From)
                .ThenBy(static history => history.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// 把同一频段的记录分配到具体卡池。同期开放的多个卡池共用调频次数，
    /// 记录本身无法区分归属，因此以「抽到的 S 级 UP 物品」为锚点：
    /// 把两次 S 之间的记录归给该 S 所属的卡池；判断不出时归给同期第一个卡池。
    /// </summary>
    private List<(ZZZGachaBanner Banner, List<GachaItem> Records)> AssignZZZRecords(ZZZGachaBanner[] banners, IEnumerable<GachaItem> items)
    {
        Dictionary<ZZZGachaBanner, List<GachaItem>> assigned = [];
        foreach (ZZZGachaBanner banner in banners)
        {
            assigned[banner] = [];
        }

        List<(GachaItem Item, ZZZGachaBanner[] Candidates)> pending = [];

        foreach (GachaItem item in items.OrderBy(static item => item.Id))
        {
            ZZZGachaBanner[] candidates = [.. banners.Where(banner => IsInBanner(item.Time, banner))];
            if (candidates.Length == 0)
            {
                // 落在所有档期之外的记录没有归属卡池，交给调用方的兜底分组。
                continue;
            }

            if (candidates.Length > 1)
            {
                QualityType quality = GetZZZItemQuality(item, zzzGachaItemMetadataService.GetById(item.ItemId));
                if (quality is not QualityType.QUALITY_ORANGE)
                {
                    pending.Add((item, candidates));
                    continue;
                }

                ZZZGachaBanner[] owners = [.. candidates.Where(banner => banner.Up.Contains(item.ItemId))];
                ZZZGachaBanner target = owners.Length is 1 ? owners[0] : candidates[0];
                foreach ((GachaItem pendingItem, ZZZGachaBanner[] pendingCandidates) in pending)
                {
                    assigned[pendingCandidates.Contains(target) ? target : pendingCandidates[0]].Add(pendingItem);
                }

                pending.Clear();
                assigned[target].Add(item);
                continue;
            }

            assigned[candidates[0]].Add(item);
        }

        foreach ((GachaItem item, ZZZGachaBanner[] candidates) in pending)
        {
            assigned[candidates[0]].Add(item);
        }

        return [.. assigned.Select(static pair => (pair.Key, pair.Value))];
    }

    private static bool IsInBanner(DateTimeOffset time, ZZZGachaBanner banner)
    {
        if (banner.End.Year is 9999)
        {
            return true;
        }

        DateTime wallClock = time.DateTime;
        return wallClock >= banner.Start.DateTime && wallClock <= banner.End.DateTime;
    }

    private HistoryWish BuildZZZHistoryWish(ZZZGachaBanner banner, List<GachaItem> records)
    {
        Dictionary<uint, StatisticsItem> orangeUp = [];
        Dictionary<uint, StatisticsItem> purpleUp = [];
        List<StatisticsItem> orangeObtained = [];
        List<StatisticsItem> purpleObtained = [];
        List<StatisticsItem> blueObtained = [];

        foreach (uint itemId in banner.Up)
        {
            if (zzzGachaItemMetadataService.GetById(itemId) is { } info)
            {
                orangeUp[itemId] = CreateZZZStatisticsItem(info, QualityType.QUALITY_ORANGE, count: 0);
            }
        }

        foreach (uint itemId in banner.UpPurple)
        {
            if (zzzGachaItemMetadataService.GetById(itemId) is { } info)
            {
                purpleUp[itemId] = CreateZZZStatisticsItem(info, QualityType.QUALITY_PURPLE, count: 0);
            }
        }

        int totalCount = 0;
        int lastOrangePull = 0;
        int lastPurplePull = 0;
        int lastBluePull = 0;

        foreach (GachaItem item in records.OrderBy(static item => item.Id))
        {
            ZZZGachaItemInfo? info = zzzGachaItemMetadataService.GetById(item.ItemId);
            QualityType quality = GetZZZItemQuality(item, info);
            ++totalCount;

            switch (quality)
            {
                case QualityType.QUALITY_ORANGE:
                    {
                        StatisticsItem statisticsItem = CreateZZZStatisticsItem(item, info, quality, count: 1);
                        statisticsItem.PullCount = totalCount - lastOrangePull;
                        lastOrangePull = totalCount;
                        orangeObtained.Add(statisticsItem);
                        if (orangeUp.TryGetValue(item.ItemId, out StatisticsItem? up))
                        {
                            ++up.Count;
                        }

                        break;
                    }

                case QualityType.QUALITY_PURPLE:
                    {
                        StatisticsItem statisticsItem = CreateZZZStatisticsItem(item, info, quality, count: 1);
                        statisticsItem.PullCount = totalCount - lastPurplePull;
                        lastPurplePull = totalCount;
                        purpleObtained.Add(statisticsItem);
                        if (purpleUp.TryGetValue(item.ItemId, out StatisticsItem? up))
                        {
                            ++up.Count;
                        }

                        break;
                    }

                case QualityType.QUALITY_BLUE:
                    {
                        StatisticsItem statisticsItem = CreateZZZStatisticsItem(item, info, quality, count: 1);
                        statisticsItem.PullCount = totalCount - lastBluePull;
                        lastBluePull = totalCount;
                        blueObtained.Add(statisticsItem);
                        break;
                    }
            }
        }

        DateTimeOffset from = banner.Start;
        DateTimeOffset to = banner.End;
        if (banner.End.Year is 9999)
        {
            from = records.Min(static item => item.Time);
            to = records.Max(static item => item.Time);
        }

        Uri bannerImage = orangeUp.Values.FirstOrDefault()?.Icon
            ?? purpleUp.Values.FirstOrDefault()?.Icon
            ?? StaticResourcesEndpoints.UIIconNone;

        bool hasBannerImage = !string.IsNullOrEmpty(banner.Image);

        return new HistoryWish
        {
            Name = string.IsNullOrEmpty(banner.Name) ? GetZZZChannelName(banner.Type) : banner.Name,
            Version = banner.Version,
            TotalCount = totalCount,
            From = from,
            To = to,
            BannerImage = hasBannerImage ? banner.Image.ToUri() : bannerImage,
            HasBannerImage = hasBannerImage,
            OrangeUpList = [.. orangeUp.Values.OrderByDescending(static item => item.Count).ThenBy(static item => item.Name, StringComparer.Ordinal)],
            PurpleUpList = [.. purpleUp.Values.OrderByDescending(static item => item.Count).ThenBy(static item => item.Name, StringComparer.Ordinal)],
            SummaryList = [.. orangeObtained],
            PurpleList = [.. purpleObtained],
            BlueList = [.. blueObtained],
        };
    }

    private static QualityType GetZZZItemQuality(GachaItem item, ZZZGachaItemInfo? info)
    {
        QualityType quality = (QualityType)item.Quality;
        return quality is QualityType.QUALITY_NONE ? info?.Quality ?? QualityType.QUALITY_NONE : quality;
    }

    private static StatisticsItem CreateZZZStatisticsItem(GachaItem item, ZZZGachaItemInfo? info, QualityType quality, int count)
    {
        return new StatisticsItem
        {
            Id = item.ItemId,
            Name = item.Name ?? info?.Name ?? string.Empty,
            Icon = info?.IconUri ?? StaticResourcesEndpoints.UIIconNone,
            Quality = quality,
            Count = count,
        };
    }

    private static StatisticsItem CreateZZZStatisticsItem(ZZZGachaItemInfo info, QualityType quality, int count)
    {
        return new StatisticsItem
        {
            Id = info.Id,
            Name = info.Name,
            Icon = info.IconUri ?? StaticResourcesEndpoints.UIIconNone,
            Quality = quality,
            Count = count,
        };
    }

    private static string GetZZZChannelName(int gachaType)
    {
        return gachaType switch
        {
            1 => SH.WebGachaConfigTypeZZZStandardChannel,
            2 => SH.WebGachaConfigTypeZZZExclusiveChannel,
            3 => SH.WebGachaConfigTypeZZZWEngineChannel,
            5 => SH.WebGachaConfigTypeZZZBangbooChannel,
            102 => SH.WebGachaConfigTypeZZZExclusiveRescreening,
            103 => SH.WebGachaConfigTypeZZZWEngineReverberation,
            _ => gachaType.ToString(CultureInfo.InvariantCulture),
        };
    }
}
