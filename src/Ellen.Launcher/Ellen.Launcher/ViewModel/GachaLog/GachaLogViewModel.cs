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
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.GachaLog.QueryProvider;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Navigation;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.UIGF;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.UI.Xaml.View.Page;
using Ellen.Launcher.ViewModel.Setting;
using Ellen.Launcher.Win32.Foundation;
using System.Collections.Immutable;
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

    private bool suppressCurrentItemChangedHandling;
    private GachaLogServiceMetadataContext? metadataContext;
    private AppOptions? appOptions;

    [GeneratedConstructor]
    public partial GachaLogViewModel(IServiceProvider serviceProvider);

    public partial WishCountdownViewModel WishCountdownViewModel { get; }

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
                IAdvancedDbCollectionView<GachaArchive> archives = await gachaLogService.GetArchiveCollectionAsync().ConfigureAwait(false);
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
            (bool isOk, query) = await provider.GetQueryAsync().ConfigureAwait(false);

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

        if (uigf!.Hk4e.IsDefaultOrEmpty)
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
            IAdvancedDbCollectionView<GachaArchive> archives = await gachaLogService.ReloadArchiveCollectionAsync().ConfigureAwait(false);
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
    }
}
