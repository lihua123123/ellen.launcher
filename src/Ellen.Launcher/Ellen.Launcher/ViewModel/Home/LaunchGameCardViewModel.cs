// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Model.Metadata.ZZZ;
using Ellen.Launcher.Service.Game;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.Locator;
using Ellen.Launcher.Service.Game.QuickLaunch;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using System.IO;
using BindingUser = Ellen.Launcher.ViewModel.User.User;

namespace Ellen.Launcher.ViewModel.Home;

/// <summary>
/// 首页「游戏启动」卡片：展示当前登录账号，并提供《原神》/《绝区零》的一键启动入口。
/// 路径识别、账号读取与进程启动的实现参考 Starward。
/// </summary>
[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Transient)]
public sealed partial class LaunchGameCardViewModel : Abstraction.ViewModelSlim, IRecipient<UserAndUidChangedMessage>
{
    private const string GenshinFallbackIconUri = "ms-appx:///Resource/Icon/UI_Icon_Paimon.png";
    private const string ZenlessZoneZeroFallbackIconUri = "ms-appx:///Resource/ZZZIcon/Element_Ether.png";

    /// <summary>
    /// 鼠标移入会触发刷新，避免频繁探测注册表与进程。
    /// </summary>
    private const long RefreshThrottleMilliseconds = 3000;

    /// <summary>
    /// 游戏退出后要把磁贴从「运行中」恢复成「启动」，轮询间隔取 2 秒。
    /// </summary>
    private static readonly TimeSpan RunningStatePollingInterval = TimeSpan.FromSeconds(2);

    private readonly IServiceProvider serviceProvider;
    private readonly IUserService userService;
    private readonly IGameLaunchService gameLaunchService;
    private readonly IGameInstallationLocator installationLocator;
    private readonly IGameLocatorFactory gameLocatorFactory;
    private readonly IZZZAvatarCacheService zzzAvatarCacheService;
    private readonly LaunchOptions launchOptions;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    private DispatcherQueueTimer? runningStateTimer;
    private bool isPollingRunningState;
    private long lastRefreshTimestamp;

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

    [ObservableProperty]
    public partial string? AccountAvatarUrl { get; set; }

    [ObservableProperty]
    public partial string? SummaryText { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    /// <summary>
    /// 切换账号时由消息驱动：先用消息里已有的账号信息立即刷新，再异步补齐绝区零角色与运行状态。
    /// </summary>
    public void Receive(UserAndUidChangedMessage message)
    {
        taskContext.InvokeOnMainThread(() => ApplyAccount(message.User));
        ResolveZenlessZoneZeroAccountAsync(message.User).SafeForget();
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
            StartRunningStatePolling();
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
    /// 点击磁贴：已安装则启动，未安装则让用户选择游戏路径。
    /// 运行中也可以点：会重新检测进程，游戏已经退出就立刻启动，不用等轮询。
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
            await SetGamePathAsync(entry).ConfigureAwait(false);
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

    [Command("SetGenshinGamePathCommand")]
    private Task SetGenshinGamePathAsync()
    {
        return SetGamePathAsync(GenshinEntry);
    }

    [Command("SetZenlessZoneZeroGamePathCommand")]
    private Task SetZenlessZoneZeroGamePathAsync()
    {
        return SetGamePathAsync(ZenlessZoneZeroEntry);
    }

    /// <summary>
    /// 卡片从可视树卸载时停止轮询。
    /// </summary>
    public void StopRunningStatePolling()
    {
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
        BindingUser? user = await userService.GetCurrentUserAsync().ConfigureAwait(false);

        // 绝区零角色走「我的角色」的本地缓存，避免切换账号时再发一次网络请求
        string? zenlessZoneZeroUid = user is null ? null : ResolveZenlessZoneZeroUid(user.InnerId);

        await taskContext.SwitchToMainThreadAsync();
        ApplyAccount(user);
        ZenlessZoneZeroEntry.AccountUid = zenlessZoneZeroUid;
    }

    private async Task ResolveZenlessZoneZeroAccountAsync(BindingUser? user)
    {
        try
        {
            string? zenlessZoneZeroUid = null;
            if (user is not null)
            {
                Guid innerId = user.InnerId;
                await taskContext.SwitchToBackgroundAsync();
                zenlessZoneZeroUid = ResolveZenlessZoneZeroUid(innerId);
                await taskContext.SwitchToMainThreadAsync();
            }

            ZenlessZoneZeroEntry.AccountUid = zenlessZoneZeroUid;
            await UpdateRunningStatesAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// 用内存中的账号对象直接刷新账号区（切换账号时立即生效，不等数据库）。
    /// </summary>
    private void ApplyAccount(BindingUser? user)
    {
        IsLoggedIn = user is not null;
        if (user is null)
        {
            AccountNickname = null;
            AccountAttribution = null;
            AccountUidText = null;
            AccountAvatarUrl = null;
            GenshinEntry.AccountUid = null;
            ZenlessZoneZeroEntry.AccountUid = null;
            return;
        }

        UserGameRole? role = user.UserGameRoles.CurrentItem;

        // 角色列表尚未拉取时仍展示账号本身，避免卡片出现空白区域
        AccountNickname = role?.Nickname ?? user.UserInfo?.Nickname ?? SH.ViewCardLaunchGameAccountFallback;
        AccountAttribution = role?.Description;
        AccountUidText = role is null ? null : SH.FormatViewCardLaunchGameUid(role.GameUid);
        AccountAvatarUrl = role?.ProfilePictureIcon;
        GenshinEntry.AccountUid = role?.GameUid;
    }

    private string? ResolveZenlessZoneZeroUid(Guid userInnerId)
    {
        ZZZRoleCache? cache = zzzAvatarCacheService.LoadRoles(userInnerId);
        if (cache is null || cache.Roles.Count is 0)
        {
            return null;
        }

        if (cache.SelectedGameUid is { Length: > 0 } selectedUid)
        {
            ZZZUserGameRole? selected = cache.Roles.FirstOrDefault(role => role.GameUid == selectedUid);
            return selected?.GameUid ?? cache.Roles[0].GameUid;
        }

        return cache.Roles[0].GameUid;
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

    private async Task SetGamePathAsync(GameLaunchEntry entry)
    {
        if (entry.Kind is GameLaunchKind.Genshin)
        {
            if (await gameLocatorFactory.LocateSingleAsync(GameLocationSourceKind.Manual).ConfigureAwait(false) is not (true, { } genshinPath))
            {
                return;
            }

            await taskContext.SwitchToMainThreadAsync();
            launchOptions.PerformGamePathEntrySynchronization(genshinPath);
        }
        else
        {
            IFileSystemPickerInteraction picker = serviceProvider.GetRequiredService<IFileSystemPickerInteraction>();
            (bool isPickerOk, ValueFile file) = picker.PickFile(
                SH.ViewPageAnnouncementSetZZZGamePathLabel,
                SH.ServiceGameLocatorPickerFilterText,
                GameConstants.ZenlessZoneZeroFileName);

            if (!isPickerOk)
            {
                return;
            }

            if (!string.Equals(Path.GetFileName(file), GameConstants.ZenlessZoneZeroFileName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await taskContext.SwitchToMainThreadAsync();
            launchOptions.ZZZGamePath.Value = file;
        }

        await taskContext.SwitchToMainThreadAsync();
        messenger.Send(InfoBarMessage.Success(SH.ViewPageAnnouncementSetGamePathSuccessTitle, SH.FormatViewCardLaunchGameSetPathSuccess(entry.Title)));
        await RefreshAsync(refreshAccount: false, refreshInstallations: true).ConfigureAwait(false);
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
