// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Factory.Process;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.Win32.Foundation;
using Ellen.Launcher.Web.Hoyolab.Passport;
using Ellen.Launcher.Web.Response;
using System.IO;
using BindingUser = Ellen.Launcher.ViewModel.User.User;

namespace Ellen.Launcher.Service.Game.QuickLaunch;

[Service(ServiceLifetime.Singleton, typeof(IGameLaunchService))]
public sealed partial class GameLaunchService : IGameLaunchService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IGameInstallationLocator installationLocator;
    private readonly IGameRegistrySessionService registrySession;
    private readonly LaunchOptions launchOptions;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    /// <summary>
    /// 票据创建失败只提示一次，避免每次启动都弹提示。
    /// </summary>
    private static int hasWarnedAuthTicketFailure;

    private static bool userHasWarnedLoginFailureOnce()
    {
        return Interlocked.CompareExchange(ref hasWarnedAuthTicketFailure, 1, 0) is 0;
    }

    [GeneratedConstructor]
    public partial GameLaunchService(IServiceProvider serviceProvider);

    public ValueTask<GameInstallation> GetInstallationAsync(GameLaunchKind kind, CancellationToken token = default)
    {
        return installationLocator.LocateAsync(kind, token);
    }

    public bool IsRunning(GameLaunchKind kind)
    {
        return kind switch
        {
            GameLaunchKind.Genshin => ProcessFactory.IsRunning([GameConstants.YuanShenProcessName, GameConstants.GenshinImpactProcessName], out _),
            GameLaunchKind.ZenlessZoneZero => ProcessFactory.IsRunning([GameConstants.ZenlessZoneZeroProcessName], out _),
            _ => false,
        };
    }

    public async ValueTask<GameLaunchResult> LaunchAsync(GameLaunchKind kind, CancellationToken token = default)
    {
        GameInstallation installation = await installationLocator.LocateAsync(kind, token).ConfigureAwait(false);
        if (!installation.IsInstalled)
        {
            return new(GameLaunchOutcome.NotInstalled);
        }

        if (IsRunning(kind))
        {
            return new(GameLaunchOutcome.AlreadyRunning);
        }

        string executablePath = installation.ExecutablePath!;
        string workingDirectory = installation.InstallDirectory!;

        BindingUser? currentUser = await GetCurrentUserAsync().ConfigureAwait(false);

        // 和 Starward 一样：游戏登录态存在注册表里，启动前把「当前选中的账号」的快照写回注册表，
        // 游戏就会直接登录这个账号；本机还没有该账号快照时退回通行证登录票据（login_auth_ticket）。
        (string? authTicket, string? loginError) = await ResolveLoginAsync(kind, currentUser, token).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(loginError)
            && userHasWarnedLoginFailureOnce())
        {
            await taskContext.SwitchToMainThreadAsync();
            messenger.Send(InfoBarMessage.Warning(SH.ViewModelLaunchGameCreateAuthTicketFailed, loginError));
            await taskContext.SwitchToBackgroundAsync();
        }

        string arguments = BuildArguments(authTicket);

        try
        {
            await taskContext.SwitchToBackgroundAsync();
            using (IProcess process = ProcessFactory.CreateDirect(arguments, executablePath, workingDirectory))
            {
                process.Start();
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode is (int)WIN32_ERROR.ERROR_ELEVATION_REQUIRED)
        {
            // 《原神》《绝区零》的可执行文件清单里声明了 requireAdministrator（反作弊驱动需要），
            // 启动器不是管理员时 CreateProcess 会直接失败（ERROR_ELEVATION_REQUIRED）。
            // 退回 ShellExecute + runas，让 UAC 只提权游戏本身 —— Starward 也是这样做的。
            return await LaunchElevatedAsync(kind, arguments, executablePath, workingDirectory).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new(GameLaunchOutcome.Failed, ex.Message);
        }

        await NotifyGameStartedAsync(kind).ConfigureAwait(false);
        return new(GameLaunchOutcome.Started);
    }

    private async ValueTask<GameLaunchResult> LaunchElevatedAsync(GameLaunchKind kind, string arguments, string executablePath, string workingDirectory)
    {
        try
        {
            using (IProcess process = ProcessFactory.CreateUsingShellExecuteRunAs(arguments, executablePath, workingDirectory))
            {
                process.Start();
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode is (int)WIN32_ERROR.ERROR_CANCELLED)
        {
            return new(GameLaunchOutcome.Failed, SH.ViewCardLaunchGameElevationCancelled);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new(GameLaunchOutcome.Failed, ex.Message);
        }

        await NotifyGameStartedAsync(kind).ConfigureAwait(false);
        return new(GameLaunchOutcome.Started);
    }

    private async ValueTask NotifyGameStartedAsync(GameLaunchKind kind)
    {
        if (kind is GameLaunchKind.Genshin)
        {
            await taskContext.SwitchToMainThreadAsync();
            GameLifeCycle.IsGameRunningProperty.Value = true;
        }
    }

    /// <summary>
    /// 与 Starward 一致：给 Unity 游戏传分辨率 / 窗口模式参数。
    /// 用户在「启动设置」里关掉命令行参数时只启动 exe。
    /// </summary>
    private string BuildArguments(string? authTicket)
    {
        CommandLineBuilder builder = new();

        if (launchOptions.AreCommandLineArgumentsEnabled.Value)
        {
            builder.Append("-screen-fullscreen", launchOptions.IsFullScreen.Value ? 1 : 0);
            builder.AppendIf(launchOptions.IsBorderless.Value, "-popupwindow");
            builder.AppendIf(launchOptions.IsExclusive.Value, "-window-mode", "exclusive");
            builder.AppendIf(launchOptions.IsScreenWidthEnabled.Value, "-screen-width", launchOptions.ScreenWidth.Value);
            builder.AppendIf(launchOptions.IsScreenHeightEnabled.Value, "-screen-height", launchOptions.ScreenHeight.Value);
            builder.AppendIf(launchOptions.IsMonitorEnabled.Value, "-monitor", launchOptions.Monitor.Value?.Value ?? 1);
        }

        // 登录票据不受「命令行参数」开关影响：它是「用当前账号登录」的凭据
        builder.AppendIf(!string.IsNullOrEmpty(authTicket), "login_auth_ticket", authTicket, CommandLineArgumentPrefix.Equal);

        return builder.ToString().Trim();
    }

    /// <summary>
    /// 决定用哪种方式让游戏登录「当前账号」：先用注册表快照切换，失败再用通行证登录票据。
    /// </summary>
    private async ValueTask<(string? Ticket, string? Error)> ResolveLoginAsync(GameLaunchKind kind, BindingUser? user, CancellationToken token)
    {
        if (user is null)
        {
            return (null, null);
        }

        if (TrySwitchRegistrySession(kind, user))
        {
            return (null, null);
        }

        (string? ticket, string? error) = await TryCreateAuthTicketAsync(kind, user, token).ConfigureAwait(false);
        return ticket is null ? (null, error ?? SH.ViewCardLaunchGameAccountNotSaved) : (ticket, null);
    }

    /// <summary>
    /// 读取注册表里当前的登录账号做一次快照，然后把目标账号的快照写回去。
    /// </summary>
    private bool TrySwitchRegistrySession(GameLaunchKind kind, BindingUser user)
    {
        registrySession.Capture(kind, user.IsOversea);

        string? mid = user.Entity.Mid;
        return !string.IsNullOrEmpty(mid) && registrySession.TryApply(kind, user.IsOversea, mid);
    }

    private async ValueTask<BindingUser?> GetCurrentUserAsync()
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserService>().GetCurrentUserAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 为当前米游社账号申请指定游戏的登录票据，失败时返回错误信息但仍然允许正常启动。
    /// </summary>
    private async ValueTask<(string? Ticket, string? Error)> TryCreateAuthTicketAsync(GameLaunchKind kind, BindingUser currentUser, CancellationToken token)
    {
        try
        {
            if (currentUser.SToken is null)
            {
                return (null, SH.ViewModelLaunchGameCreateAuthTicketFailed);
            }

            string gameBiz = (kind, currentUser.IsOversea) switch
            {
                (GameLaunchKind.Genshin, false) => "hk4e_cn",
                (GameLaunchKind.Genshin, true) => "hk4e_global",
                (GameLaunchKind.ZenlessZoneZero, false) => "nap_cn",
                _ => "nap_global",
            };

            using IServiceScope scope = serviceProvider.CreateScope();
            IHoyoPlayPassportClient client = scope.ServiceProvider
                .GetRequiredService<IOverseaSupportFactory<IHoyoPlayPassportClient>>()
                .Create(currentUser.IsOversea);

            Response<AuthTicketWrapper> response = await client
                .CreateAuthTicketAsync(currentUser.Entity, gameBiz, token)
                .ConfigureAwait(false);

            if (ResponseValidator.TryValidateWithoutUINotification(response, out AuthTicketWrapper? wrapper)
                && !string.IsNullOrEmpty(wrapper.Ticket))
            {
                return (wrapper.Ticket, null);
            }

            return (null, response.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentrySdk.CaptureException(ex);
            return (null, ex.Message);
        }
    }
}
