// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Ellen.Launcher.Model.Metadata.ZZZ;
using Ellen.Launcher.Service.Game;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.QuickLaunch;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.SignIn;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using System.Collections.Immutable;
using BindingUser = Ellen.Launcher.ViewModel.User.User;

namespace Ellen.Launcher.ViewModel.Home;

/// <summary>
/// 首页「游戏启动」卡片：展示当前登录账号，并提供《原神》/《绝区零》的一键启动入口。
/// 路径识别、账号读取与进程启动的实现参考 Starward。
/// </summary>
[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Transient)]
[SuppressMessage("", "CA1001", Justification = "角色查询自行释放令牌源，卡片卸载时取消查询。")]
public sealed partial class LaunchGameCardViewModel : Abstraction.ViewModelSlim, IRecipient<UserAndUidChangedMessage>
{
    private const string GenshinFallbackIconUri = "ms-appx:///Resource/Icon/UI_Icon_Paimon.png";

    /// <summary>
    /// 未检测到《绝区零》安装位置时显示的图标。
    /// 这里必须是游戏图标本身，不能拿元素图标顶替 —— ZZZIcon 目录下是战斗属性
    /// （物理/火/冰/雷/以太…）与职业图标，用来当游戏卡片封面会显示成一颗紫色星星。
    /// </summary>
    private const string ZenlessZoneZeroFallbackIconUri = "ms-appx:///Resource/Icon/UI_GameIcon_Zenless.png";

    /// <summary>
    /// 鼠标移入会触发刷新，避免频繁探测注册表与进程。
    /// </summary>
    private const long RefreshThrottleMilliseconds = 3000;

    /// <summary>
    /// 游戏退出后要把磁贴从「运行中」恢复成「启动」，轮询间隔取 2 秒。
    /// </summary>
    private static readonly TimeSpan RunningStatePollingInterval = TimeSpan.FromSeconds(2);

    private readonly IUserService userService;
    private readonly IGameLaunchService gameLaunchService;
    private readonly IGameInstallationLocator installationLocator;
    private readonly IZZZAvatarCacheService zzzAvatarCacheService;
    private readonly IGameRoleCollectionService gameRoleCollectionService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    private DispatcherQueueTimer? runningStateTimer;
    private bool isPollingRunningState;
    private long lastRefreshTimestamp;
    private long accountMessageVersion;
    private Guid? displayedAccountId;
    private CancellationTokenSource? zenlessRoleRequest;

    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial LaunchGameCardViewModel(IServiceProvider serviceProvider);

    [ObservableProperty]
    public partial GameLaunchEntry GenshinEntry { get; set; } = GameLaunchEntry.Create(GameLaunchKind.Genshin, SH.ViewCardLaunchGameGenshinTitle);

    [ObservableProperty]
    public partial GameLaunchEntry ZenlessZoneZeroEntry { get; set; } = GameLaunchEntry.Create(GameLaunchKind.ZenlessZoneZero, SH.ViewCardLaunchGameZenlessZoneZeroTitle);

    [ObservableProperty]
    public partial bool IsLoggedIn { get; set; }

    [ObservableProperty]
    public partial string? AccountNickname { get; set; }

    /// <summary>
    /// 「天空岛 | Lv.60」
    /// </summary>
    [ObservableProperty]
    public partial string? AccountAttribution { get; set; }

    [ObservableProperty]
    public partial string? AccountUidText { get; set; }

    /// <summary>
    /// 米游社账号头像（远程图片地址），不是游戏角色的元素头像。
    /// </summary>
    [ObservableProperty]
    public partial Uri? AccountAvatarUri { get; set; }

    [ObservableProperty]
    public partial string? SummaryText { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    /// <summary>
    /// 切换账号时由消息驱动：先用消息里已有的账号信息立即刷新，再异步补齐绝区零角色与运行状态。
    /// </summary>
    public void Receive(UserAndUidChangedMessage message)
    {
        taskContext.InvokeOnMainThread(() =>
        {
            if (IsViewUnloaded.Value)
            {
                return;
            }

            accountMessageVersion++;
            ApplyAccount(message.User);
            UpdateRunningStatesAsync().SafeForget();
        });
    }

    /// <inheritdoc/>
    protected override async Task LoadAsync()
    {
        try
        {
            await taskContext.SwitchToMainThreadAsync();
            EnsureFallbackIcons();

            await RefreshAsync(refreshAccount: true, refreshInstallations: true).ConfigureAwait(false);

            await taskContext.SwitchToMainThreadAsync();
            if (!IsViewUnloaded.Value)
            {
                StartRunningStatePolling();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await taskContext.SwitchToMainThreadAsync();
            messenger.Send(InfoBarMessage.Error(ex));
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsInitialized = true;
        }
    }

    [Command("RefreshCommand")]
    private async Task RefreshAsync()
    {
        if (IsRefreshing || Environment.TickCount64 - lastRefreshTimestamp < RefreshThrottleMilliseconds)
        {
            return;
        }

        // 鼠标移入只需要账号 + 运行状态，安装路径扫描留给加载与设置路径之后
        await RefreshAsync(refreshAccount: true, refreshInstallations: false).ConfigureAwait(false);
    }

    /// <summary>
    /// 点击磁贴：已安装则启动。
    /// 运行中也可以点：会重新检测进程，游戏已经退出就立刻启动，不用等轮询。
    /// 未安装时不在这里选路径 —— 安装目录统一在设置页维护，卡片只提示去设置页添加。
    /// </summary>
    [Command("LaunchCommand")]
    private async Task LaunchAsync(GameLaunchEntry? entry)
    {
        if (entry is null || entry.IsLaunching)
        {
            return;
        }

        if (!entry.IsInstalled)
        {
            messenger.Send(InfoBarMessage.Warning(SH.ViewCardLaunchGameStartedTitle, SH.ViewCardLaunchGamePathHintInSettings));
            return;
        }

        entry.IsLaunching = true;
        try
        {
            GameLaunchResult result = await gameLaunchService.LaunchAsync(entry.Kind).ConfigureAwait(false);

            await taskContext.SwitchToMainThreadAsync();
            switch (result.Outcome)
            {
                case GameLaunchOutcome.Started:
                case GameLaunchOutcome.AlreadyRunning:
                    // 启动成功不弹任何提示，磁贴自己会切到「运行中」
                    entry.IsRunning = true;
                    UpdateSummary();
                    break;
                case GameLaunchOutcome.NotInstalled:
                    messenger.Send(InfoBarMessage.Warning(SH.ViewCardLaunchGameStartedTitle, SH.ViewCardLaunchGameStatusNotInstalled));
                    break;
                case GameLaunchOutcome.Failed:
                    messenger.Send(InfoBarMessage.Error(SH.ViewCardLaunchGameStartedTitle, result.Message ?? SH.ViewCardLaunchGameLaunchFailed));
                    break;
            }
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            entry.IsLaunching = false;
        }
    }

    /// <summary>
    /// 卡片从可视树卸载时停止轮询和角色查询。
    /// </summary>
    public void StopRunningStatePolling()
    {
        CancelZenlessRoleRequest();

        if (runningStateTimer is null)
        {
            return;
        }

        runningStateTimer.Stop();
        runningStateTimer.Tick -= OnRunningStateTimerTick;
        runningStateTimer = null;
    }

    private async Task RefreshAsync(bool refreshAccount, bool refreshInstallations)
    {
        lastRefreshTimestamp = Environment.TickCount64;

        await taskContext.SwitchToMainThreadAsync();
        IsRefreshing = true;

        try
        {
            if (refreshAccount)
            {
                await RefreshAccountAsync().ConfigureAwait(false);
            }

            if (refreshInstallations)
            {
                await RefreshInstallationsAsync().ConfigureAwait(false);
            }

            await UpdateRunningStatesAsync().ConfigureAwait(false);
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsRefreshing = false;
        }
    }

    private async Task RefreshAccountAsync()
    {
        await taskContext.SwitchToMainThreadAsync();
        long observedVersion = accountMessageVersion;
        BindingUser? user = await userService.GetCurrentUserAsync().ConfigureAwait(false);

        await taskContext.SwitchToMainThreadAsync();
        // 读取期间如果已经收到较新的切换消息，不再应用这次读取的账号。
        if (accountMessageVersion == observedVersion && !IsViewUnloaded.Value)
        {
            ApplyAccount(user);
        }
    }

    /// <summary>
    /// 用内存中的账号对象直接刷新账号区（切换账号时立即生效，不等数据库）。
    /// </summary>
    private void ApplyAccount(BindingUser? user)
    {
        Guid? accountId = user?.InnerId;
        if (displayedAccountId != accountId)
        {
            CancelZenlessRoleRequest();
            displayedAccountId = accountId;
            ZenlessZoneZeroEntry.AccountUid = null;
        }

        IsLoggedIn = user is not null;
        if (user is null)
        {
            AccountNickname = null;
            AccountAttribution = null;
            AccountUidText = null;
            AccountAvatarUri = null;
            GenshinEntry.AccountUid = null;
            return;
        }

        UserGameRole? role = user.UserGameRoles.CurrentItem;

        // 卡片展示的是米游社账号本身（头像、昵称、米游社 UID），不是游戏角色的头像/昵称/游戏 UID。
        // 原神角色的 UID 只用于启动账号绑定，仍由下面的 entry 保留。
        AccountNickname = user.UserInfo?.Nickname ?? SH.ViewCardLaunchGameAccountFallback;
        AccountAttribution = null;
        AccountUidText = string.IsNullOrEmpty(user.UserInfo?.Uid) ? null : SH.FormatViewCardLaunchGameUid(user.UserInfo!.Uid!);
        AccountAvatarUri = user.UserInfo?.AvatarUri;
        GenshinEntry.AccountUid = role?.GameUid;

        if (zenlessRoleRequest is null)
        {
            ResolveZenlessZoneZeroAccountAsync(user).SafeForget();
        }
    }

    private async Task ResolveZenlessZoneZeroAccountAsync(BindingUser user)
    {
        using CancellationTokenSource request = new();
        zenlessRoleRequest = request;
        CancellationToken token = request.Token;
        try
        {
            await taskContext.SwitchToBackgroundAsync();
            ZZZRoleCache? cache = zzzAvatarCacheService.LoadRoles(user.InnerId);
            string? preferredUid = cache?.SelectedGameUid;
            string? cachedUid = cache?.Roles.FirstOrDefault(role => !string.IsNullOrWhiteSpace(role.GameUid) && role.GameUid == preferredUid)?.GameUid
                ?? cache?.Roles.FirstOrDefault(role => !string.IsNullOrWhiteSpace(role.GameUid))?.GameUid;

            await taskContext.SwitchToMainThreadAsync();
            if (zenlessRoleRequest != request || IsViewUnloaded.Value)
            {
                return;
            }

            // 先显示同一账号的离线缓存，随后以实际绑定的角色为准。
            ZenlessZoneZeroEntry.AccountUid ??= cachedUid;

            ImmutableArray<SignInGameRole> roles = await gameRoleCollectionService.GetRolesAsync(user, token).ConfigureAwait(false);
            IEnumerable<SignInGameRole> zenlessRoles = roles.Where(static role => role.Game is SignInGame.ZenlessZoneZero && !string.IsNullOrWhiteSpace(role.Uid));
            string? boundUid = zenlessRoles.FirstOrDefault(role => role.Uid == preferredUid)?.Uid ?? zenlessRoles.FirstOrDefault()?.Uid;

            await taskContext.SwitchToMainThreadAsync();
            if (zenlessRoleRequest == request && !IsViewUnloaded.Value && boundUid is not null)
            {
                ZenlessZoneZeroEntry.AccountUid = boundUid;
            }
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            if (zenlessRoleRequest == request)
            {
                zenlessRoleRequest = null;
            }
        }
    }

    private void CancelZenlessRoleRequest()
    {
        CancellationTokenSource? request = zenlessRoleRequest;
        zenlessRoleRequest = null;
        request?.Cancel();
    }

    private async Task RefreshInstallationsAsync()
    {
        GameInstallation genshin = await installationLocator.LocateAsync(GameLaunchKind.Genshin).ConfigureAwait(false);
        GameInstallation zenlessZoneZero = await installationLocator.LocateAsync(GameLaunchKind.ZenlessZoneZero).ConfigureAwait(false);

        await ApplyInstallationAsync(GenshinEntry, genshin, GenshinFallbackIconUri).ConfigureAwait(false);
        await ApplyInstallationAsync(ZenlessZoneZeroEntry, zenlessZoneZero, ZenlessZoneZeroFallbackIconUri).ConfigureAwait(false);
    }

    /// <summary>
    /// 图标直接取游戏 exe 自带的图标，和官方启动器/任务栏一致；取不到时用随包占位图标。
    /// </summary>
    private async Task ApplyInstallationAsync(GameLaunchEntry entry, GameInstallation installation, string fallbackIconUri)
    {
        byte[]? iconPng = installation.IsInstalled ? GameIconProvider.TryGetPngBytes(installation.ExecutablePath) : null;

        await taskContext.SwitchToMainThreadAsync();
        entry.ApplyInstallation(installation);

        if (iconPng is not null)
        {
            entry.IconSource = await GameIconProvider.CreateImageSourceAsync(iconPng);
        }
        else
        {
            entry.IconSource = GameIconProvider.CreateBundledIcon(fallbackIconUri);
        }

        UpdateSummary();
    }

    private async Task UpdateRunningStatesAsync()
    {
        await taskContext.SwitchToBackgroundAsync();
        bool isGenshinRunning = gameLaunchService.IsRunning(GameLaunchKind.Genshin);
        bool isZenlessZoneZeroRunning = gameLaunchService.IsRunning(GameLaunchKind.ZenlessZoneZero);

        await taskContext.SwitchToMainThreadAsync();
        GenshinEntry.IsRunning = isGenshinRunning;
        ZenlessZoneZeroEntry.IsRunning = isZenlessZoneZeroRunning;
        UpdateSummary();
    }

    private void StartRunningStatePolling()
    {
        if (runningStateTimer is not null)
        {
            return;
        }

        runningStateTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        runningStateTimer.Interval = RunningStatePollingInterval;
        runningStateTimer.IsRepeating = true;
        runningStateTimer.Tick += OnRunningStateTimerTick;
        runningStateTimer.Start();
    }

    private void OnRunningStateTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (isPollingRunningState)
        {
            return;
        }

        isPollingRunningState = true;
        PollRunningStatesAsync().SafeForget();
    }

    private async Task PollRunningStatesAsync()
    {
        try
        {
            await UpdateRunningStatesAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
        finally
        {
            isPollingRunningState = false;
        }
    }

    private void EnsureFallbackIcons()
    {
        GenshinEntry.IconSource ??= GameIconProvider.CreateBundledIcon(GenshinFallbackIconUri);
        ZenlessZoneZeroEntry.IconSource ??= GameIconProvider.CreateBundledIcon(ZenlessZoneZeroFallbackIconUri);
    }

    private void UpdateSummary()
    {
        int installed = (GenshinEntry.IsInstalled ? 1 : 0) + (ZenlessZoneZeroEntry.IsInstalled ? 1 : 0);

        if (GenshinEntry.IsRunning || ZenlessZoneZeroEntry.IsRunning)
        {
            SummaryText = SH.ViewCardLaunchGameSummaryRunning;
            return;
        }

        SummaryText = installed is 0
            ? SH.ViewCardLaunchGameSummaryNoGame
            : SH.FormatViewCardLaunchGameSummary(installed, 2);
    }
}
