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
    private readonly ILogger<AvatarPropertyViewModel> logger;

    private readonly List<SearchToken> genshinFilterTokens = [];
    private readonly List<SearchToken> zzzFilterTokens = [];
    private SummaryFactoryMetadataContext? metadataContext;
    private SearchData? zzzEmptySearchData;
    private bool suppressZZZRoleChanged;
    private bool wasZZZSummaryVisible;
    private int zzzLoadGeneration;
    private ImmutableArray<NameValue<AvatarPropertySortDescriptionKind>>? genshinSortDescriptionKinds;
    private ImmutableArray<NameValue<AvatarPropertySortDescriptionKind>>? zzzSortDescriptionKinds;
    private NameValue<AvatarPropertySortDescriptionKind>? sortDescriptionKind;

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

    /// <summary>
    /// ZZZ 无数据时，把同一个搜索/筛选 token 框放到页面上方，保证空状态入口不丢。
    /// </summary>
    public bool IsZZZEmptySearchVisible => IsZZZMode && Summary is null;

    public SearchData? ZZZEmptySearchData => zzzEmptySearchData;

    public ObservableCollection<SearchToken>? ZZZEmptyFilterTokens => IsZZZMode && Summary is null ? zzzEmptySearchData?.FilterTokens : null;

    public ImmutableArray<NameValue<AvatarPropertySortDescriptionKind>> SortDescriptionKinds
    {
        get => IsZZZMode
            ? zzzSortDescriptionKinds ??= CreateZZZSortDescriptionKinds()
            : genshinSortDescriptionKinds ??= CreateGenshinSortDescriptionKinds();
    }

    private string CurrentSortDescriptionSettingKey => IsZZZMode
        ? SettingKeys.AvatarPropertySortDescriptionKindZZZ
        : SettingKeys.AvatarPropertySortDescriptionKind;

    // TODO: Replace with IObservableProperty
    public NameValue<AvatarPropertySortDescriptionKind>? SortDescriptionKind
    {
        get
        {
            if (sortDescriptionKind is null)
            {
                AvatarPropertySortDescriptionKind saved = UnsafeLocalSetting.Get(CurrentSortDescriptionSettingKey, AvatarPropertySortDescriptionKind.Default);
                sortDescriptionKind = Selection.Initialize(SortDescriptionKinds, saved) ?? SortDescriptionKinds[0];
            }

            return sortDescriptionKind;
        }

        set
        {
            if (value is not null && SetProperty(ref sortDescriptionKind, value))
            {
                UnsafeLocalSetting.Set(CurrentSortDescriptionSettingKey, value.Value);
                PrivateSortAvatars();
            }
        }
    }

    partial void OnSelectedGameChanged(NameValue<GachaGameType> value)
    {
        SwitchGameAsync(value.Value).SafeForget();
    }

    partial void OnIsZZZModeChanged(bool value)
    {
        // 原神/绝区零使用各自的排序项与设置键，切换时丢弃缓存的选择，避免把
        // 绝区零的「影画解锁层数」或已删除的「好感度」带到另一边。
        sortDescriptionKind = null;
        OnPropertyChanged(nameof(SortDescriptionKinds));
        OnPropertyChanged(nameof(SortDescriptionKind));
        OnPropertyChanged(nameof(IsZZZEmptySearchVisible));
        OnPropertyChanged(nameof(ZZZEmptySearchData));
        OnPropertyChanged(nameof(ZZZEmptyFilterTokens));
    }

    partial void OnSummaryChanged(Summary? value)
    {
        bool hasSummary = value is not null;
        OnPropertyChanged(nameof(IsZZZEmptySearchVisible));
        OnPropertyChanged(nameof(ZZZEmptyFilterTokens));

        if (IsZZZMode && zzzEmptySearchData is not null && SearchData is not null)
        {
            // 空状态与内容区是两个 token 框，各自包装自己的集合；只在 null ↔ 有内容
            // 的边界搬运已选 token，普通刷新（有内容 → 有内容）不能清掉用户筛选。
            // 规则：当前可见框里已有 token 时以它为准；只有它是空的时候才从隐藏框补。
            if (!hasSummary && wasZZZSummaryVisible)
            {
                if (zzzEmptySearchData.FilterTokens.Count is 0)
                {
                    ReplaceFilterTokens(zzzEmptySearchData.FilterTokens, SearchData.FilterTokens);
                }

                SearchData.FilterToken = null;
                RemoveAllFilterTokens(SearchData.FilterTokens);
            }
            else if (hasSummary && !wasZZZSummaryVisible)
            {
                if (zzzEmptySearchData.FilterTokens.Count > 0)
                {
                    ReplaceFilterTokens(SearchData.FilterTokens, zzzEmptySearchData.FilterTokens);
                    RemoveAllFilterTokens(zzzEmptySearchData.FilterTokens);
                }

                zzzEmptySearchData.FilterToken = null;
            }
        }

        wasZZZSummaryVisible = hasSummary;
    }

    private static void ReplaceFilterTokens(ObservableCollection<SearchToken> target, IEnumerable<SearchToken> source)
    {
        if (ReferenceEquals(target, source))
        {
            return;
        }

        RemoveAllFilterTokens(target);
        foreach (SearchToken token in source)
        {
            if (!target.Contains(token))
            {
                target.Add(token);
            }
        }
    }

    private static void RemoveAllFilterTokens(ObservableCollection<SearchToken> target)
    {
        while (target.Count > 0)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static ImmutableArray<NameValue<AvatarPropertySortDescriptionKind>> CreateGenshinSortDescriptionKinds()
    {
        return
        [
            .. ImmutableCollectionsNameValue.FromEnum<AvatarPropertySortDescriptionKind>(
                    static type => type.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture) ?? string.Empty)
                .Where(static option => option.Value is not (
                    AvatarPropertySortDescriptionKind.ZZZActivatedConstellationCount
                    or AvatarPropertySortDescriptionKind.ZZZElementMastery)),
        ];
    }

    private static ImmutableArray<NameValue<AvatarPropertySortDescriptionKind>> CreateZZZSortDescriptionKinds()
    {
        ImmutableArray<NameValue<AvatarPropertySortDescriptionKind>> all = ImmutableCollectionsNameValue.FromEnum<AvatarPropertySortDescriptionKind>(
            static type => type.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture) ?? string.Empty);

        AvatarPropertySortDescriptionKind[] order =
        [
            AvatarPropertySortDescriptionKind.Default,
            AvatarPropertySortDescriptionKind.LevelNumber,
            AvatarPropertySortDescriptionKind.Quality,
            AvatarPropertySortDescriptionKind.ZZZActivatedConstellationCount,
            AvatarPropertySortDescriptionKind.MaxHp,
            AvatarPropertySortDescriptionKind.CurAttack,
            AvatarPropertySortDescriptionKind.CurDefense,
            AvatarPropertySortDescriptionKind.ZZZElementMastery,
        ];

        return [.. order.Select(kind => all.Single(option => option.Value == kind))];
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

        if (await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, refreshTokenProvider.GetNewToken());
            await PrivateRefreshAsync(userAndUid, RefreshOptionKind.None, linkedCts.Token).ConfigureAwait(false);
        }

        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        SearchData ??= SearchData.CreateForAvatarProperty();

        return true;
    }

    private async Task SwitchGameAsync(GachaGameType game)
    {
        bool isZZZ = game is GachaGameType.ZZZ;
        SearchData ??= SearchData.CreateForAvatarProperty();
        if (isZZZ)
        {
            zzzEmptySearchData ??= SearchData.CreateForZZZAvatarProperty();
        }

        // SearchData/FilterTokens 始终是同一个对象：AutoSuggestTokenBox 包装过 ItemsSource 后
        // 再替换整个 SearchData 不会重新绑定，所以这里只原地换 token 候选与已选 token。
        List<SearchToken> currentTokens = IsZZZMode ? zzzFilterTokens : genshinFilterTokens;
        currentTokens.Clear();
        currentTokens.AddRange(SearchData.FilterTokens);

        List<SearchToken> targetTokens = isZZZ ? zzzFilterTokens : genshinFilterTokens;
        SearchData.SetAvailableTokens(isZZZ ? SearchTokens.GetForZZZAvatarProperty() : SearchTokens.GetForAvatarProperty());

        // 不要用 Clear()：它会发 Reset，AutoSuggestTokenBox 的 InterspersedObservableCollection
        // 对 Reset 有已知的 ListView 视觉树重复异常；逐项 Remove 走 Add/Remove 通知更安全。
        while (SearchData.FilterTokens.Count > 0)
        {
            SearchData.FilterTokens.RemoveAt(SearchData.FilterTokens.Count - 1);
        }

        foreach (SearchToken token in targetTokens)
        {
            SearchData.FilterTokens.Add(token);
        }

        SearchData.FilterToken = null;
        IsZZZMode = isZZZ;

        if (!isZZZ)
        {
            // 让还在后台跑的 ZZZ 请求作废，避免它把原神的 Summary 覆盖掉。
            Interlocked.Increment(ref zzzLoadGeneration);
            IsZZZLoading = false;
            Summary = genshinSummary;
            IsInitialized = true;
            ApplyFilter();
            return;
        }

        genshinSummary ??= Summary;
        if (Summary is null && zzzEmptySearchData is not null)
        {
            // Summary 本来就是 null 时不会触发 OnSummaryChanged，这里补一次搬运。
            // 空状态框是当前可见的权威来源：它已有 token 时不要用隐藏的主框覆盖。
            if (zzzEmptySearchData.FilterTokens.Count is 0)
            {
                ReplaceFilterTokens(zzzEmptySearchData.FilterTokens, SearchData.FilterTokens);
            }

            RemoveAllFilterTokens(SearchData.FilterTokens);
        }

        Summary = null;
        IsInitialized = false;
        await LoadZZZRolesAsync().ConfigureAwait(false);
    }

    private async Task LoadZZZRolesAsync(bool forceRefresh = false)
    {
        int generation = Interlocked.Increment(ref zzzLoadGeneration);
        await SetZZZLoadingAsync(true).ConfigureAwait(false);

        try
        {
            UserAndUid? userAndUid = await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false);
            if (userAndUid is null)
            {
                await ApplyZZZEmptyStateAsync(generation, InfoBarMessage.Warning(SH.ViewPageAvatarPropertyZZZNeedLogin)).ConfigureAwait(false);
                return;
            }

            await scopeContext.TaskContext.SwitchToBackgroundAsync();
            ZZZRoleCache? roleCache = scopeContext.ZZZAvatarCacheService.LoadRoles(userAndUid.User.InnerId);

            // 角色列表也走缓存优先：断网/重启时仍能回到上次的角色与代理人缓存。
            if (!forceRefresh && roleCache is { Roles.Count: > 0 })
            {
                ZZZUserGameRole cachedSelected = roleCache.Roles.FirstOrDefault(role => role.GameUid == roleCache.SelectedGameUid) ?? roleCache.Roles[0];
                if (!await ApplyZZZRolesAsync(roleCache.Roles, cachedSelected, generation).ConfigureAwait(false))
                {
                    return;
                }

                await LoadZZZAgentsAsync(userAndUid, cachedSelected, forceRefresh: false, generation).ConfigureAwait(false);
                return;
            }

            Response<ListWrapper<ZZZUserGameRole>> response = await scopeContext.BindingClient
                .GetZZZUserGameRolesAsync(userAndUid.User, CancellationToken)
                .ConfigureAwait(false);

            if (!IsCurrentZZZLoad(generation))
            {
                return;
            }

            if (!ResponseValidator.TryValidateWithoutUINotification(response, scopeContext.ServiceProvider, out ListWrapper<ZZZUserGameRole>? roleWrapper))
            {
                logger.LogWarning("Failed to refresh ZZZ roles: {Code} {Message}", response.ReturnCode, response.Message);

                if (roleCache is { Roles.Count: > 0 })
                {
                    ZZZUserGameRole cachedSelected = roleCache.Roles.FirstOrDefault(role => role.GameUid == roleCache.SelectedGameUid) ?? roleCache.Roles[0];
                    if (!await ApplyZZZRolesAsync(roleCache.Roles, cachedSelected, generation).ConfigureAwait(false))
                    {
                        return;
                    }

                    await LoadZZZAgentsAsync(userAndUid, cachedSelected, forceRefresh, generation).ConfigureAwait(false);
                }
                else
                {
                    await ApplyZZZEmptyStateAsync(generation, InfoBarMessage.Error(SH.ViewPageAvatarPropertyZZZLoadFailed), preserveSummary: true).ConfigureAwait(false);
                }

                return;
            }

            ZZZUserGameRole[] roles = [.. roleWrapper.List];
            string? preferredUid = SelectedZZZRole?.GameUid;
            ZZZUserGameRole? selected = roles.FirstOrDefault(role => role.GameUid == preferredUid) ?? roles.FirstOrDefault();

            if (!await ApplyZZZRolesAsync(roles, selected, generation).ConfigureAwait(false))
            {
                return;
            }

            if (selected is null)
            {
                await ApplyZZZEmptyStateAsync(generation, InfoBarMessage.Warning(SH.ViewPageAvatarPropertyZZZNoRole)).ConfigureAwait(false);
                return;
            }

            await scopeContext.TaskContext.SwitchToBackgroundAsync();
            if (!IsCurrentZZZLoad(generation))
            {
                return;
            }

            scopeContext.ZZZAvatarCacheService.SaveRoles(new ZZZRoleCache
            {
                UserInnerId = userAndUid.User.InnerId,
                UpdatedAt = DateTimeOffset.UtcNow,
                Roles = [.. roles],
                SelectedGameUid = selected.GameUid,
            });

            await LoadZZZAgentsAsync(userAndUid, selected, forceRefresh, generation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load ZZZ roles.");
            await ApplyZZZEmptyStateAsync(generation, InfoBarMessage.Error(ex), preserveSummary: true).ConfigureAwait(false);
        }
        finally
        {
            if (IsCurrentZZZLoad(generation))
            {
                await SetZZZLoadingAsync(false).ConfigureAwait(false);
            }
        }
    }

    private async Task LoadZZZAgentsAsync(ZZZUserGameRole role)
    {
        int generation = Interlocked.Increment(ref zzzLoadGeneration);
        UserAndUid? userAndUid = await scopeContext.UserService.GetCurrentUserAndUidAsync().ConfigureAwait(false);
        if (userAndUid is null)
        {
            await ApplyZZZEmptyStateAsync(generation, InfoBarMessage.Warning(SH.ViewPageAvatarPropertyZZZNeedLogin)).ConfigureAwait(false);
            return;
        }

        // 记住用户手动选择的角色，避免下次启动又跳回第一个。
        await scopeContext.TaskContext.SwitchToBackgroundAsync();
        ZZZRoleCache? roleCache = scopeContext.ZZZAvatarCacheService.LoadRoles(userAndUid.User.InnerId);
        if (roleCache is not null && !string.Equals(roleCache.SelectedGameUid, role.GameUid, StringComparison.Ordinal))
        {
            roleCache.SelectedGameUid = role.GameUid;
            scopeContext.ZZZAvatarCacheService.SaveRoles(roleCache);
        }

        await LoadZZZAgentsAsync(userAndUid, role, forceRefresh: false, generation).ConfigureAwait(false);
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

    private async Task LoadZZZAgentsAsync(UserAndUid userAndUid, ZZZUserGameRole role, bool forceRefresh = false, int? generation = null)
    {
        int loadGeneration = generation ?? Interlocked.Increment(ref zzzLoadGeneration);
        await SetZZZLoadingAsync(true).ConfigureAwait(false);
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        if (!IsCurrentZZZLoad(loadGeneration))
        {
            return;
        }

        // 已有缓存/旧角色时保持内容可见，避免「同步角色信息」造成整页闪一下。
        if (Summary is null)
        {
            IsInitialized = false;
        }

        ZZZAvatarCache? cache = null;
        bool cacheApplied = false;

        try
        {
            await scopeContext.TaskContext.SwitchToBackgroundAsync();
            cache = scopeContext.ZZZAvatarCacheService.Load(role.GameUid, role.Region);
            if (cache is not null)
            {
                cacheApplied = await ApplyZZZCacheAsync(cache, loadGeneration).ConfigureAwait(false);
            }

            if (!IsCurrentZZZLoad(loadGeneration))
            {
                return;
            }

            // 缓存是持久数据：有缓存就直接显示并停止自动联网，
            // 只有点击「同步角色信息」才会带 forceRefresh 强制更新。
            if (!forceRefresh && cacheApplied)
            {
                return;
            }

            Response<ZZZAvatarListWrapper<ZZZAvatarBasic>> response = await scopeContext.ZZZGameRecordClient
                .GetZZZAgentsAsync(userAndUid.User, role.GameUid, role.Region, CancellationToken)
                .ConfigureAwait(false);

            if (!IsCurrentZZZLoad(loadGeneration))
            {
                return;
            }

            if (!ResponseValidator.TryValidateWithoutUINotification(response, scopeContext.ServiceProvider, out ZZZAvatarListWrapper<ZZZAvatarBasic>? basicWrapper))
            {
                logger.LogWarning("Failed to refresh ZZZ agents: {Code} {Message}", response.ReturnCode, response.Message);

                if (!cacheApplied)
                {
                    await ApplyZZZEmptyStateAsync(loadGeneration, InfoBarMessage.Error(SH.ViewPageAvatarPropertyZZZLoadFailed), preserveSummary: true).ConfigureAwait(false);
                }
                else if (forceRefresh)
                {
                    await ShowZZZRefreshFailedUsingCacheAsync(loadGeneration).ConfigureAwait(false);
                }

                return;
            }

            ZZZAvatarBasic[] basics = [.. basicWrapper.List];
            if (basics.Length is 0)
            {
                if (!cacheApplied)
                {
                    await ApplyZZZEmptyStateAsync(loadGeneration, InfoBarMessage.Warning(SH.ViewPageAvatarPropertyZZZNoRole)).ConfigureAwait(false);
                }

                return;
            }

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

            if (!IsCurrentZZZLoad(loadGeneration))
            {
                return;
            }

            if (!await ApplyZZZViewsAsync(views, loadGeneration).ConfigureAwait(false))
            {
                return;
            }

            await scopeContext.TaskContext.SwitchToBackgroundAsync();
            if (!IsCurrentZZZLoad(loadGeneration))
            {
                return;
            }

            SaveMergedZZZCache(cache, role, basics, detailsJson, wikiAssets);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load ZZZ agents for {Uid}.", role.GameUid);

            if (!cacheApplied && cache is not null)
            {
                cacheApplied = await ApplyZZZCacheAsync(cache, loadGeneration).ConfigureAwait(false);
            }

            if (!cacheApplied)
            {
                await ApplyZZZEmptyStateAsync(loadGeneration, InfoBarMessage.Error(ex), preserveSummary: true).ConfigureAwait(false);
            }
            else if (forceRefresh)
            {
                await ShowZZZRefreshFailedUsingCacheAsync(loadGeneration).ConfigureAwait(false);
            }
        }
        finally
        {
            if (IsCurrentZZZLoad(loadGeneration))
            {
                await SetZZZLoadingAsync(false).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> ApplyZZZCacheAsync(ZZZAvatarCache cache, int generation)
    {
        AvatarView[] views = [.. cache.Agents.Select(basic => CreateViewFromCache(basic, cache))];
        return await ApplyZZZViewsAsync(views, generation).ConfigureAwait(false);
    }

    private async Task<bool> ApplyZZZViewsAsync(AvatarView[] views, int generation)
    {
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        if (!IsCurrentZZZLoad(generation))
        {
            return false;
        }

        Summary summary = Summary.Create(views.Where(static view => view is not null).ToImmutableArray().AsAdvancedCollectionView());
        Summary = summary;
        PrivateSortAvatars();
        ApplyFilter();
        summary.Avatars.MoveCurrentToFirst();
        IsInitialized = true;
        return true;
    }

    private void SaveMergedZZZCache(
        ZZZAvatarCache? previous,
        ZZZUserGameRole role,
        ZZZAvatarBasic[] basics,
        ConcurrentDictionary<int, string> detailsJson,
        ConcurrentDictionary<string, ZZZWikiAssetCache> wikiAssets)
    {
        Dictionary<int, string> mergedDetails = previous?.DetailsJson is { } oldDetails ? new(oldDetails) : [];
        foreach ((int id, string json) in detailsJson)
        {
            mergedDetails[id] = json;
        }

        Dictionary<string, ZZZWikiAssetCache> mergedWikiAssets = previous?.WikiAssets is { } oldAssets
            ? new(oldAssets, StringComparer.Ordinal)
            : new(StringComparer.Ordinal);

        foreach ((string name, ZZZWikiAssetCache assets) in wikiAssets)
        {
            mergedWikiAssets[name] = assets;
        }

        scopeContext.ZZZAvatarCacheService.Save(new ZZZAvatarCache
        {
            GameUid = role.GameUid,
            Region = role.Region,
            UpdatedAt = DateTimeOffset.UtcNow,
            Agents = [.. basics],
            DetailsJson = mergedDetails,
            WikiAssets = mergedWikiAssets,
        });
    }

    private async Task<bool> ApplyZZZRolesAsync(IReadOnlyList<ZZZUserGameRole> roles, ZZZUserGameRole? selected, int generation)
    {
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        if (!IsCurrentZZZLoad(generation))
        {
            return false;
        }

        ZZZRoles = new ObservableCollection<ZZZUserGameRole>(roles);
        suppressZZZRoleChanged = true;
        try
        {
            SelectedZZZRole = selected;
        }
        finally
        {
            suppressZZZRoleChanged = false;
        }

        return true;
    }

    private async Task ApplyZZZEmptyStateAsync(int generation, InfoBarMessage? message, bool preserveSummary = false)
    {
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        if (!IsCurrentZZZLoad(generation))
        {
            return;
        }

        if (!preserveSummary)
        {
            Summary = null;
        }

        IsInitialized = true;
        if (message is not null)
        {
            scopeContext.Messenger.Send(message);
        }
    }

    private async Task ShowZZZRefreshFailedUsingCacheAsync(int generation)
    {
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        if (!IsCurrentZZZLoad(generation))
        {
            return;
        }

        scopeContext.Messenger.Send(InfoBarMessage.Warning(SH.ViewPageAvatarPropertyZZZRefreshFailedUsingCache));
    }

    private async Task SetZZZLoadingAsync(bool value)
    {
        await scopeContext.TaskContext.SwitchToMainThreadAsync();
        IsZZZLoading = value;
    }

    private bool IsCurrentZZZLoad(int generation)
    {
        return IsZZZMode && generation == Volatile.Read(ref zzzLoadGeneration);
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
            // 同步同时刷新角色列表与代理人；LoadZZZRolesAsync 内部会用 forceRefresh 传给代理人。
            await LoadZZZRolesAsync(forceRefresh: true).ConfigureAwait(false);
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

        if (Summary is not { Avatars: { } avatars })
        {
            return;
        }

        // 原神/绝区零共用同一个 AutoSuggestTokenBox；SearchData 按游戏切换，
        // 绝区零的“职业/属性”就是普通搜索 token，和角色名关键词一起生效。
        avatars.Filter = AvatarViewFilter.Compile(SearchData);

        OnPropertyChanged(nameof(FormattedTotalAvatarCount));

        if (avatars.CurrentItem is null)
        {
            avatars.MoveCurrentToFirst();
        }
    }
}