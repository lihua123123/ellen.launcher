// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Collections;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Calculable;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.ZZZ;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.AvatarInfo;
using Ellen.Launcher.Service.AvatarInfo.Factory;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.Service.Cultivation.Consumption;
using Ellen.Launcher.Service.Cultivation.Offline;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Endpoint.Upstream;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;
using Ellen.Launcher.Web.Response;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CalculatorAvatarPromotionDelta = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.AvatarPromotionDelta;
using CalculatorBatchConsumption = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.BatchConsumption;
using CalculatorConsumption = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.Consumption;
using CalculatorItemHelper = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.ItemHelper;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class AvatarPropertyViewModel : Abstraction.ViewModel, IRecipient<UserAndUidChangedMessage>, IDisposable
{
    private readonly ExclusiveTokenProvider refreshTokenProvider = new();
    private readonly AvatarPropertyViewModelScopeContext scopeContext;

    private SummaryFactoryMetadataContext? metadataContext;
    private bool suppressZZZRoleChanged;

    [GeneratedConstructor]
    public partial AvatarPropertyViewModel(IServiceProvider serviceProvider);

    [ObservableProperty]
    public partial Summary? Summary { get; set; }

    [ObservableProperty]
    public partial SearchData? SearchData { get; set; }

    /// <summary>
    /// 可切换的游戏（原神 / 绝区零）
    /// </summary>
    public ImmutableArray<NameValue<GachaGameType>> Games { get; } =
    [
        new(SH.ViewPageGachaLogGameGenshin, GachaGameType.Genshin),
        new(SH.ViewPageGachaLogGameZZZ, GachaGameType.ZZZ),
    ];

    [ObservableProperty]
    public partial NameValue<GachaGameType> SelectedGame { get; set; } = new(SH.ViewPageGachaLogGameGenshin, GachaGameType.Genshin);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PropertyHeader))]
    public partial bool IsZZZMode { get; set; }

    public string PropertyHeader => IsZZZMode
        ? SH.ViewPageAvatarPropertyHeaderZZZ
        : SH.ViewPageAvatarPropertyHeader;

    /// <summary>
    /// 米游社绑定的绝区零角色（区服为 prod_gf_cn 等字符串，不能复用原神 Region）。
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<ZZZUserGameRole> ZZZRoles { get; set; } = [];

    [ObservableProperty]
    public partial ZZZUserGameRole? SelectedZZZRole { get; set; }

    [ObservableProperty]
    public partial bool IsZZZLoading { get; set; }

    private Summary? genshinSummary;

    public string FormattedTotalAvatarCount { get => SH.FormatViewModelAvatarPropertyTotalAvatarCountHint(Summary?.Avatars.Count ?? 0); }

    public ImmutableArray<NameValue<AvatarPropertySortDescriptionKind>> SortDescriptionKinds { get; } = ImmutableCollectionsNameValue.FromEnum<AvatarPropertySortDescriptionKind>(static type => type.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture) ?? string.Empty);

    // TODO: Replace with IObservableProperty
    public NameValue<AvatarPropertySortDescriptionKind>? SortDescriptionKind
    {
        get => field ??= Selection.Initialize(SortDescriptionKinds, UnsafeLocalSetting.Get(SettingKeys.AvatarPropertySortDescriptionKind, AvatarPropertySortDescriptionKind.Default));
        set
        {
            if (value is not null && SetProperty(ref field, value))
            {
                UnsafeLocalSetting.Set(SettingKeys.AvatarPropertySortDescriptionKind, value.Value);
                PrivateSortAvatars();
            }
        }
    }

    partial void OnSelectedGameChanged(NameValue<GachaGameType> value)
    {
        SwitchGameAsync(value.Value).SafeForget();
    }

    partial void OnSelectedZZZRoleChanged(ZZZUserGameRole? value)
    {
        if (!suppressZZZRoleChanged && IsZZZMode && value is not null)
        {
            LoadZZZAgentsAsync(value).SafeForget();
        }
    }

    public void Receive(UserAndUidChangedMessage message)
    {
        if (message.UserAndUid is not { } userAndUid)
        {
            return;
        }

        CancellationToken token = refreshTokenProvider.GetNewToken();
        WeakReference<AvatarPropertyViewModel> weakThis = new(this);

        // 1. We need to wait for the view initialization (mainly for metadata context).
        // 2. We need to refresh summary data. otherwise, the view can be un-synced.
        Initialization.Task.ContinueWith(
            static (init, state) =>
            {
                if (!init.Result)
                {
                    return;
                }

                (UserAndUid userAndUid, WeakReference<AvatarPropertyViewModel> weakThis) = Unsafe.Unbox<(UserAndUid, WeakReference<AvatarPropertyViewModel>)>(state!);
                if (weakThis.TryGetTarget(out AvatarPropertyViewModel? viewModel) && !viewModel.IsViewUnloaded.Value)
                {
                    if (viewModel.IsZZZMode)
                    {
                        viewModel.LoadZZZRolesAsync().SafeForget();
                    }
                    else
                    {
                        viewModel.PrivateRefreshAsync(userAndUid, RefreshOptionKind.None, viewModel.CancellationToken).SafeForget();
                    }
                }
            },
            (userAndUid, weakThis),
            token,
            TaskContinuationOptions.None,
            TaskScheduler.Current);
    }

    public override void Dispose()
    {
        refreshTokenProvider.Dispose();
        base.Dispose();
    }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        if (!await scopeContext.MetadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        metadataContext = await scopeContext.MetadataService.GetContextAsync<SummaryFactoryMetadataContext>(token).ConfigureAwait(false);
        SearchData searchData = SearchData.CreateForAvatarProperty();

        if (await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, refreshTokenProvider.GetNewToken());
            await PrivateRefreshAsync(userAndUid, RefreshOptionKind.None, linkedCts.Token).ConfigureAwait(false);
        }

        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        SearchData = searchData;

        return true;
    }

    private async Task SwitchGameAsync(GachaGameType game)
    {
        IsZZZMode = game is GachaGameType.ZZZ;

        if (!IsZZZMode)
        {
            Summary = genshinSummary;
            IsInitialized = true;
            return;
        }

        genshinSummary ??= Summary;
        Summary = null;
        IsInitialized = false;
        IsZZZLoading = true;
        try
        {
            await LoadZZZRolesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Summary = null;
            scopeContext.Messenger.Send(InfoBarMessage.Error(ex));
            IsInitialized = true;
        }
        finally
        {
            IsZZZLoading = false;
        }
    }

    private async Task LoadZZZRolesAsync()
    {
        UserAndUid? userAndUid = await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false);
        if (userAndUid is null)
        {
            Summary = null;
            IsInitialized = true;
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageAvatarPropertyZZZNeedLogin));
            return;
        }

        await scopeContext.TaskContext.SwitchToBackgroundAsync();
        Response<ListWrapper<ZZZUserGameRole>> response = await scopeContext.BindingClient
            .GetZZZUserGameRolesAsync(userAndUid.User, CancellationToken)
            .ConfigureAwait(false);

        if (!ResponseValidator.TryValidate(response, scopeContext.ServiceProvider, out ListWrapper<ZZZUserGameRole>? roleWrapper))
        {
            Summary = null;
            IsInitialized = true;
            return;
        }

        ZZZUserGameRole[] roles = [.. roleWrapper.List];
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        ZZZRoles = new ObservableCollection<ZZZUserGameRole>(roles);

        ZZZUserGameRole? selected = roles.FirstOrDefault();
        suppressZZZRoleChanged = true;
        SelectedZZZRole = selected;
        suppressZZZRoleChanged = false;

        if (selected is null)
        {
            Summary = null;
            IsInitialized = true;
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageAvatarPropertyZZZNoRole));
            return;
        }

        await LoadZZZAgentsAsync(userAndUid, selected).ConfigureAwait(false);
    }

    private async Task LoadZZZAgentsAsync(ZZZUserGameRole role)
    {
        UserAndUid? userAndUid = await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false);
        if (userAndUid is null)
        {
            IsInitialized = true;
            return;
        }

        await LoadZZZAgentsAsync(userAndUid, role).ConfigureAwait(false);
    }

    private static Uri? SelectCinemaImage(ImmutableArray<Uri> images, int rank)
    {
        if (images.IsDefaultOrEmpty)
        {
            return null;
        }

        int index = rank <= 2 ? 0 : rank <= 5 ? 1 : 2;
        return images[Math.Min(index, images.Length - 1)];
    }

    private async Task LoadZZZAgentsAsync(UserAndUid userAndUid, ZZZUserGameRole role, bool forceRefresh = false)
    {
        IsZZZLoading = true;
        IsInitialized = false;
        ZZZAvatarCache? cache = null;
        try
        {
            await scopeContext.TaskContext.SwitchToBackgroundAsync();
            cache = scopeContext.ZZZAvatarCacheService.Load(role.GameUid, role.Region);
            if (!forceRefresh
                && cache is not null
                && DateTimeOffset.UtcNow - cache.UpdatedAt <= scopeContext.ZZZAvatarCacheService.MaxAge)
            {
                await ApplyZZZCacheAsync(cache).ConfigureAwait(false);
                return;
            }

            Response<ZZZAvatarListWrapper<ZZZAvatarBasic>> response = await scopeContext.ZZZGameRecordClient
                .GetZZZAgentsAsync(userAndUid.User, role.GameUid, role.Region, CancellationToken)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(response, scopeContext.ServiceProvider, out ZZZAvatarListWrapper<ZZZAvatarBasic>? basicWrapper))
            {
                if (cache is not null)
                {
                    await ApplyZZZCacheAsync(cache).ConfigureAwait(false);
                }
                else
                {
                    Summary = null;
                }

                return;
            }

            ZZZAvatarBasic[] basics = [.. basicWrapper.List];
            ConcurrentDictionary<int, string> detailsJson = [];
            ConcurrentDictionary<string, ZZZWikiAssetCache> wikiAssets = new(StringComparer.Ordinal);
            AvatarView[] views = new AvatarView[basics.Length];
            using SemaphoreSlim gate = new(4);
            await Task.WhenAll(basics.Select(async (basic, index) =>
            {
                await gate.WaitAsync(CancellationToken).ConfigureAwait(false);
                try
                {
                    Response<JsonElement> detailResponse = await scopeContext.ZZZGameRecordClient
                        .GetZZZAgentInfoAsync(userAndUid.User, role.GameUid, role.Region, basic.Id, CancellationToken)
                        .ConfigureAwait(false);

                    if (detailResponse.ReturnCode is 0 && detailResponse.Data.ValueKind is JsonValueKind.Object)
                    {
                        try
                        {
                            ZZZAvatarListWrapper<ZZZFullAgent>? wrapper = detailResponse.Data.Deserialize<ZZZAvatarListWrapper<ZZZFullAgent>>();
                            if (wrapper?.List.FirstOrDefault() is { } fullAgent)
                            {
                                ZZZAgentWikiAssets assets = await scopeContext.ZZZCinemaImageService
                                    .GetAgentAssetsAsync(basic.Name, CancellationToken)
                                    .ConfigureAwait(false);
                                SaveWikiAssets(wikiAssets, basic.Name, assets);
                                detailsJson[basic.Id] = detailResponse.Data.GetRawText();
                                AvatarView fullView = ZZZAvatarViewFactory.Create(fullAgent, assets.SkillIcons);
                                if (SelectCinemaImage(assets.CinemaImages, fullAgent.Rank) is { } cinema)
                                {
                                    fullView.NameCard = cinema;
                                }

                                views[index] = fullView;
                                return;
                            }
                        }
                        catch
                        {
                            // 单条详情解析失败时退回简略数据，不影响整个列表。
                        }
                    }

                    AvatarView basicView = ZZZAvatarViewFactory.CreateBasic(basic);
                    ZZZAgentWikiAssets basicAssets = await scopeContext.ZZZCinemaImageService
                        .GetAgentAssetsAsync(basic.Name, CancellationToken)
                        .ConfigureAwait(false);
                    SaveWikiAssets(wikiAssets, basic.Name, basicAssets);
                    if (SelectCinemaImage(basicAssets.CinemaImages, basic.Rank) is { } basicCinema)
                    {
                        basicView.NameCard = basicCinema;
                    }

                    views[index] = basicView;
                }
                catch
                {
                    views[index] = ZZZAvatarViewFactory.CreateBasic(basic);
                }
                finally
                {
                    gate.Release();
                }
            })).ConfigureAwait(false);

            await scopeContext.TaskContext.SwitchToMainThreadAsync();
            Summary summary = Summary.Create(views.Where(static view => view is not null).ToImmutableArray().AsAdvancedCollectionView());
            Summary = summary;
            summary.Avatars.MoveCurrentToFirst();
            PrivateSortAvatars();
            IsInitialized = true;

            scopeContext.ZZZAvatarCacheService.Save(new ZZZAvatarCache
            {
                GameUid = role.GameUid,
                Region = role.Region,
                UpdatedAt = DateTimeOffset.UtcNow,
                Agents = [.. basics],
                DetailsJson = detailsJson.ToDictionary(static pair => pair.Key, static pair => pair.Value),
                WikiAssets = wikiAssets.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
            });
        }
        catch (Exception ex)
        {
            if (cache is not null)
            {
                await ApplyZZZCacheAsync(cache).ConfigureAwait(false);
            }
            else
            {
                Summary = null;
                IsInitialized = true;
                scopeContext.Messenger.Send(InfoBarMessage.Error(ex));
            }
        }
        finally
        {
            IsZZZLoading = false;
        }
    }

    private async Task ApplyZZZCacheAsync(ZZZAvatarCache cache)
    {
        AvatarView[] views = [.. cache.Agents.Select(basic => CreateViewFromCache(basic, cache))];
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        Summary summary = Summary.Create(views.Where(static view => view is not null).ToImmutableArray().AsAdvancedCollectionView());
        Summary = summary;
        summary.Avatars.MoveCurrentToFirst();
        PrivateSortAvatars();
        IsInitialized = true;
    }

    private static AvatarView CreateViewFromCache(ZZZAvatarBasic basic, ZZZAvatarCache cache)
    {
        cache.WikiAssets.TryGetValue(basic.Name, out ZZZWikiAssetCache? assets);
        ImmutableArray<Uri> skillIcons = ToUris(assets?.SkillIcons);
        ImmutableArray<Uri> cinemaImages = ToUris(assets?.CinemaImages);

        if (cache.DetailsJson.TryGetValue(basic.Id, out string? detailJson))
        {
            try
            {
                ZZZAvatarListWrapper<ZZZFullAgent>? wrapper = JsonSerializer.Deserialize<ZZZAvatarListWrapper<ZZZFullAgent>>(detailJson);
                if (wrapper?.List.FirstOrDefault() is { } fullAgent)
                {
                    AvatarView fullView = ZZZAvatarViewFactory.Create(fullAgent, skillIcons);
                    if (SelectCinemaImage(cinemaImages, fullAgent.Rank) is { } cinema)
                    {
                        fullView.NameCard = cinema;
                    }

                    return fullView;
                }
            }
            catch
            {
                // 缓存中的单条详情损坏时退回简略视图。
            }
        }

        AvatarView basicView = ZZZAvatarViewFactory.CreateBasic(basic);
        if (SelectCinemaImage(cinemaImages, basic.Rank) is { } basicCinema)
        {
            basicView.NameCard = basicCinema;
        }

        return basicView;
    }

    private static ImmutableArray<Uri> ToUris(string[]? values)
    {
        if (values is not { Length: > 0 })
        {
            return [];
        }

        ImmutableArray<Uri>.Builder builder = ImmutableArray.CreateBuilder<Uri>(values.Length);
        foreach (string value in values)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            {
                builder.Add(uri);
            }
        }

        return builder.ToImmutable();
    }

    private static void SaveWikiAssets(ConcurrentDictionary<string, ZZZWikiAssetCache> target, string agentName, ZZZAgentWikiAssets assets)
    {
        target[agentName] = new ZZZWikiAssetCache
        {
            CinemaImages = [.. assets.CinemaImages.Select(static uri => uri.ToString())],
            SkillIcons = [.. assets.SkillIcons.Select(static uri => uri.ToString())],
        };
    }

    [Command("RefreshFromHoyolabGameRecordCommand")]
    private async Task RefreshByHoyolabGameRecordAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Refresh", "AvatarPropertyViewModel.Command"));

        if (IsZZZMode)
        {
            if (SelectedZZZRole is { } role
                && await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } zzzUserAndUid)
            {
                await LoadZZZAgentsAsync(zzzUserAndUid, role, forceRefresh: true).ConfigureAwait(false);
            }
            else
            {
                await LoadZZZRolesAsync().ConfigureAwait(false);
            }

            return;
        }

        if (await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, refreshTokenProvider.GetNewToken());
            await PrivateRefreshAsync(userAndUid, RefreshOptionKind.RequestFromHoyolabGameRecord, linkedCts.Token).ConfigureAwait(false);
        }
    }

    [SuppressMessage("", "SH003")]
    private async Task PrivateRefreshAsync(UserAndUid userAndUid, RefreshOptionKind optionKind, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(metadataContext);

        try
        {
            Summary? summary;
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                ContentDialog dialog = await scopeContext.ContentDialogFactory
                    .CreateForIndeterminateProgressAsync(SH.ViewModelAvatarPropertyFetch)
                    .ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                using (await scopeContext.ContentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
                {
                    token.ThrowIfCancellationRequested();
                    summary = await scopeContext.AvatarInfoService
                        .GetSummaryAsync(metadataContext, userAndUid, optionKind, token)
                        .ConfigureAwait(false);
                }
            }

            await scopeContext.TaskContext.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();
            genshinSummary = summary;
            if (!IsZZZMode)
            {
                Summary = summary;
                PrivateSortAvatars();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void PrivateSortAvatars()
    {
        ArgumentNullException.ThrowIfNull(SortDescriptionKind);
        if (Summary?.Avatars is not { } avatars)
        {
            return;
        }

        using (avatars.DeferRefresh())
        {
            avatars.SortDescriptions.Clear();
            foreach (ref readonly SortDescription sd in AvatarPropertySortDescriptions.Get(SortDescriptionKind.Value).AsSpan())
            {
                avatars.SortDescriptions.Add(sd);
            }
        }

        avatars.MoveCurrentToFirst();
    }

    [Command("CultivateCommand")]
    private async Task CultivateAsync(AvatarView? avatar)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Cultivate", "AvatarPropertyViewModel.Command"));

        if (avatar is null)
        {
            return;
        }

        if (avatar.IsMetadataSynthesized)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelAvatarPropertyPlayerCultivateUnsupported));
            return;
        }

        if (avatar.Weapon is null)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelAvatarPropertyCalculateWeaponNull));
            return;
        }

        CalculableOptions options = new(avatar, avatar.Weapon);
        CultivatePromotionDeltaDialog dialog = await scopeContext.ContentDialogFactory
            .CreateInstanceAsync<CultivatePromotionDeltaDialog>(scopeContext.ServiceProvider, options)
            .ConfigureAwait(false);

        if (await dialog.GetPromotionDeltaAsync().ConfigureAwait(false) is not (true, { } deltaOptions))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(metadataContext);
        CalculatorBatchConsumption batchConsumption = OfflineCalculator.CalculateBatchConsumption(deltaOptions.Delta, metadataContext);

        if (!await SaveCultivationAsync(batchConsumption.Items.Single(), deltaOptions).ConfigureAwait(false))
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning));
        }
    }

    [Command("AddAvatarToPlanCommand")]
    private async Task AddAvatarToPlanAsync(AvatarView? avatar)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("AddAvatarToPlan", "AvatarPropertyViewModel.Command"));

        if (avatar is null)
        {
            return;
        }

        if (avatar.IsMetadataSynthesized)
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelAvatarPropertyPlayerCultivateUnsupported));
            return;
        }

        CalculableOptions options = new(avatar, null);
        CultivatePromotionDeltaDialog dialog = await scopeContext.ContentDialogFactory
            .CreateInstanceAsync<CultivatePromotionDeltaDialog>(scopeContext.ServiceProvider, options)
            .ConfigureAwait(false);

        if (await dialog.GetPromotionDeltaAsync().ConfigureAwait(false) is not (true, { } deltaOptions))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(metadataContext);
        ArgumentNullException.ThrowIfNull(avatar.Weapon);

        ICalculable weapon = avatar.Weapon.ToCalculable();
        deltaOptions.Delta.Weapon = new()
        {
            Id = avatar.Weapon!.Id,
            LevelCurrent = weapon.LevelCurrent,
            LevelTarget = weapon.LevelCurrent,
        };

        CalculatorBatchConsumption batchConsumption = OfflineCalculator.CalculateBatchConsumption(deltaOptions.Delta, metadataContext);

        if (!await SaveAvatarOnlyCultivationAsync(batchConsumption.Items.Single(), deltaOptions).ConfigureAwait(false))
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning));
        }
    }

    [Command("AddWeaponToPlanCommand")]
    private async Task AddWeaponToPlanAsync(AvatarView? avatar)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("AddWeaponToPlan", "AvatarPropertyViewModel.Command"));

        if (avatar is null || avatar.Weapon is null)
        {
            if (avatar is null) return;
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelAvatarPropertyCalculateWeaponNull));
            return;
        }

        CalculableOptions options = new(null, avatar.Weapon);
        CultivatePromotionDeltaDialog dialog = await scopeContext.ContentDialogFactory
            .CreateInstanceAsync<CultivatePromotionDeltaDialog>(scopeContext.ServiceProvider, options)
            .ConfigureAwait(false);

        if (await dialog.GetPromotionDeltaAsync().ConfigureAwait(false) is not (true, { } deltaOptions))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(metadataContext);

        deltaOptions.Delta.AvatarId = avatar.Id;
        deltaOptions.Delta.AvatarLevelCurrent = avatar.LevelNumber;
        deltaOptions.Delta.AvatarLevelTarget = avatar.LevelNumber;
        deltaOptions.Delta.SkillList = [];

        CalculatorBatchConsumption batchConsumption = OfflineCalculator.CalculateBatchConsumption(deltaOptions.Delta, metadataContext);

        if (!await SaveWeaponOnlyCultivationAsync(batchConsumption.Items.Single(), deltaOptions).ConfigureAwait(false))
        {
            scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning));
        }
    }
    [Command("BatchCultivateCommand")]
    private async Task BatchCultivateAsync(bool full)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI($"Batch cultivate, full: {full}", "AvatarPropertyViewModel.Command"));

        if (Summary is not { Avatars: { } avatars })
        {
            return;
        }

        ImmutableArray<AvatarView> targetAvatars;

        if (!full)
        {
            AvatarPropertyMultiAvatarCultivateSelectDialog selectDialog = await scopeContext.ContentDialogFactory
                .CreateInstanceAsync<AvatarPropertyMultiAvatarCultivateSelectDialog>(scopeContext.ServiceProvider)
                .ConfigureAwait(false);

            await scopeContext.TaskContext.SwitchToMainThreadAsync();
            selectDialog.Avatars = avatars;
            if (!await selectDialog.SelectAvatarsAsync().ConfigureAwait(false))
            {
                return;
            }

            if (!selectDialog.SelectedAvatars.Any())
            {
                scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewModelAvatarPropertyBatchCultivateNoSelectedAvatar));
                return;
            }

            targetAvatars = selectDialog.SelectedAvatars;
        }
        else
        {
            targetAvatars = [.. avatars.Source];
        }

        CultivatePromotionDeltaBatchDialog dialog = await scopeContext.ContentDialogFactory
            .CreateInstanceAsync<CultivatePromotionDeltaBatchDialog>(scopeContext.ServiceProvider)
            .ConfigureAwait(false);

        if (await dialog.GetPromotionDeltaBaselineAsync().ConfigureAwait(false) is not (true, { } baseline))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(baseline.Delta.Weapon);

        ContentDialog progressDialog = await scopeContext.ContentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ViewModelAvatarPropertyBatchCultivateProgressTitle)
            .ConfigureAwait(false);

        BatchCultivateResult result = default;
        using (await scopeContext.ContentDialogFactory.BlockAsync(progressDialog).ConfigureAwait(false))
        {
            ImmutableArray<CalculatorAvatarPromotionDelta>.Builder deltasBuilder = ImmutableArray.CreateBuilder<CalculatorAvatarPromotionDelta>();
            foreach (AvatarView avatar in targetAvatars)
            {
                if (avatar.IsMetadataSynthesized)
                {
                    ++result.SkippedCount;
                    continue;
                }

                if (!baseline.Delta.TryGetNonErrorCopy(avatar, out CalculatorAvatarPromotionDelta? copy))
                {
                    ++result.SkippedCount;
                    continue;
                }

                deltasBuilder.Add(copy);
            }

            ImmutableArray<CalculatorAvatarPromotionDelta> deltas = deltasBuilder.ToImmutable();

            ArgumentNullException.ThrowIfNull(metadataContext);
            CalculatorBatchConsumption batchConsumption = OfflineCalculator.CalculateBatchConsumption(deltas, metadataContext);

            foreach ((CalculatorConsumption consumption, CalculatorAvatarPromotionDelta delta) in batchConsumption.Items.Zip(deltas))
            {
                if (!await SaveCultivationAsync(consumption, new(delta, baseline.Strategy), true).ConfigureAwait(false))
                {
                    break;
                }

                ++result.SucceedCount;
            }
        }

        InfoBarMessage message = result.SkippedCount > 0
            ? InfoBarMessage.Warning(SH.FormatViewModelCultivationBatchAddIncompleted(result.SucceedCount, result.SkippedCount))
            : InfoBarMessage.Success(SH.FormatViewModelCultivationBatchAddCompleted(result.SucceedCount, result.SkippedCount));

        scopeContext.Messenger.Send(message);
    }

    private async ValueTask<bool> SaveAvatarOnlyCultivationAsync(CalculatorConsumption consumption, CultivatePromotionDeltaOptions options, bool isBatch = false)
    {
        LevelInformation levelInformation = LevelInformation.From(options.Delta);

        InputConsumption avatarInput = new()
        {
            Type = CultivateType.AvatarAndSkill,
            ItemId = options.Delta.AvatarId,
            Items = CalculatorItemHelper.Merge(consumption.AvatarConsume, consumption.AvatarSkillConsume),
            LevelInformation = levelInformation,
            Strategy = options.Strategy,
        };

        ConsumptionSaveResultKind avatarSaveKind = await scopeContext.CultivationService.SaveConsumptionAsync(avatarInput).ConfigureAwait(false);

        InfoBarMessage? avatarMessage = avatarSaveKind switch
        {
            ConsumptionSaveResultKind.NoProject => InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning),
            ConsumptionSaveResultKind.Skipped => isBatch ? default : InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveSkippedHint),
            ConsumptionSaveResultKind.NoItem => isBatch ? default : InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveNoItemHint),
            ConsumptionSaveResultKind.Added => isBatch ? default : InfoBarMessage.Success(SH.ViewModelCultivationEntryAddSuccess),
            _ => default,
        };

        if (avatarMessage is not null)
        {
            scopeContext.Messenger.Send(avatarMessage);
        }

        return avatarSaveKind is not ConsumptionSaveResultKind.NoProject;
    }

    private async ValueTask<bool> SaveWeaponOnlyCultivationAsync(CalculatorConsumption consumption, CultivatePromotionDeltaOptions options, bool isBatch = false)
    {
        LevelInformation levelInformation = LevelInformation.From(options.Delta);

        ArgumentNullException.ThrowIfNull(options.Delta.Weapon);

        InputConsumption weaponInput = new()
        {
            Type = CultivateType.Weapon,
            ItemId = options.Delta.Weapon.Id,
            Items = consumption.WeaponConsume,
            LevelInformation = levelInformation,
            Strategy = options.Strategy,
        };

        ConsumptionSaveResultKind weaponSaveKind = await scopeContext.CultivationService.SaveConsumptionAsync(weaponInput).ConfigureAwait(false);
        InfoBarMessage? weaponMessage = weaponSaveKind switch
        {
            ConsumptionSaveResultKind.NoProject => InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning),
            ConsumptionSaveResultKind.Skipped => isBatch ? default : InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveSkippedHint),
            ConsumptionSaveResultKind.NoItem => isBatch ? default : InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveNoItemHint),
            ConsumptionSaveResultKind.Added => isBatch ? default : InfoBarMessage.Success(SH.ViewModelCultivationEntryAddSuccess),
            _ => default,
        };

        if (weaponMessage is not null)
        {
            scopeContext.Messenger.Send(weaponMessage);
        }

        return weaponSaveKind is not ConsumptionSaveResultKind.NoProject;
    }
    /// <returns><see langword="true"/> if we can continue saving consumptions, otherwise <see langword="false"/>.</returns>
    private async ValueTask<bool> SaveCultivationAsync(CalculatorConsumption consumption, CultivatePromotionDeltaOptions options, bool isBatch = false)
    {
        LevelInformation levelInformation = LevelInformation.From(options.Delta);

        InputConsumption avatarInput = new()
        {
            Type = CultivateType.AvatarAndSkill,
            ItemId = options.Delta.AvatarId,
            Items = CalculatorItemHelper.Merge(consumption.AvatarConsume, consumption.AvatarSkillConsume),
            LevelInformation = levelInformation,
            Strategy = options.Strategy,
        };

        ConsumptionSaveResultKind avatarSaveKind = await scopeContext.CultivationService.SaveConsumptionAsync(avatarInput).ConfigureAwait(false);

        InfoBarMessage? avatarMessage = avatarSaveKind switch
        {
            ConsumptionSaveResultKind.NoProject => InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning),
            ConsumptionSaveResultKind.Skipped => isBatch ? default : InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveSkippedHint),
            ConsumptionSaveResultKind.NoItem => isBatch ? default : InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveNoItemHint),
            ConsumptionSaveResultKind.Added => isBatch ? default : InfoBarMessage.Success(SH.ViewModelCultivationEntryAddSuccess),
            _ => default,
        };

        if (avatarMessage is not null)
        {
            scopeContext.Messenger.Send(avatarMessage);
        }

        if (avatarSaveKind is ConsumptionSaveResultKind.NoProject)
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(options.Delta.Weapon);

        InputConsumption weaponInput = new()
        {
            Type = CultivateType.Weapon,
            ItemId = options.Delta.Weapon.Id,
            Items = consumption.WeaponConsume,
            LevelInformation = levelInformation,
            Strategy = options.Strategy,
        };

        ConsumptionSaveResultKind weaponSaveKind = await scopeContext.CultivationService.SaveConsumptionAsync(weaponInput).ConfigureAwait(false);
        InfoBarMessage? weaponMessage = weaponSaveKind switch
        {
            ConsumptionSaveResultKind.NoProject => InfoBarMessage.Warning(SH.ViewModelCultivationEntryAddWarning),
            ConsumptionSaveResultKind.Skipped => isBatch ? default : InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveSkippedHint),
            ConsumptionSaveResultKind.NoItem => isBatch ? default : InfoBarMessage.Information(SH.ViewModelCultivationConsumptionSaveNoItemHint),
            ConsumptionSaveResultKind.Added => isBatch ? default : InfoBarMessage.Success(SH.ViewModelCultivationEntryAddSuccess),
            _ => default,
        };

        if (weaponMessage is not null)
        {
            scopeContext.Messenger.Send(weaponMessage);
        }

        return weaponSaveKind is not ConsumptionSaveResultKind.NoProject;
    }

    [Command("ExportToTextCommand")]
    private async Task ExportToTextAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Export as text to ClipBoard", "AvatarPropertyViewModel.Command"));

        if (Summary is not { Avatars.CurrentItem: { } avatar })
        {
            return;
        }

        InfoBarMessage message = await scopeContext.ClipboardProvider.SetTextAsync(AvatarViewTextTemplating.GetTemplatedText(avatar)).ConfigureAwait(false)
            ? InfoBarMessage.Success(SH.ViewModelAvatatPropertyExportTextSuccess)
            : InfoBarMessage.Warning(SH.ViewModelAvatatPropertyExportTextError);

        scopeContext.Messenger.Send(message);
    }

    [Command("ExportAllToTextCommand")]
    private async Task ExportAllToTextAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Export all as text to ClipBoard", "AvatarPropertyViewModel.Command"));

        if (Summary is not { Avatars: { } avatars })
        {
            return;
        }

        StringBuilder stringBuilder = new();

        foreach (AvatarView avatar in avatars)
        {
            stringBuilder.AppendLine(AvatarViewTextTemplating.GetTemplatedText(avatar));
        }

        InfoBarMessage message = await scopeContext.ClipboardProvider.SetTextAsync(stringBuilder.ToString()).ConfigureAwait(false)
            ? InfoBarMessage.Success(SH.ViewModelAvatatPropertyExportTextSuccess)
            : InfoBarMessage.Warning(SH.ViewModelAvatatPropertyExportTextError);

        scopeContext.Messenger.Send(message);
    }

    [Command("FilterCommand")]
    private void ApplyFilter()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Filter", "AvatarPropertyViewModel.Command"));

        if (Summary is null)
        {
            return;
        }

        Summary.Avatars.Filter = AvatarViewFilter.Compile(SearchData);
        OnPropertyChanged(nameof(FormattedTotalAvatarCount));

        if (Summary.Avatars.CurrentItem is null)
        {
            Summary.Avatars.MoveCurrentToFirst();
        }
    }
}