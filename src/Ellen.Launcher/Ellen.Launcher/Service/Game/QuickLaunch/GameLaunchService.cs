// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Factory.Process;
using Ellen.Launcher.Win32.Foundation;
using System.IO;

namespace Ellen.Launcher.Service.Game.QuickLaunch;

[Service(ServiceLifetime.Singleton, typeof(IGameLaunchService))]
public sealed partial class GameLaunchService : IGameLaunchService
{
    private readonly IGameInstallationLocator installationLocator;
    private readonly LaunchOptions launchOptions;
    private readonly ITaskContext taskContext;

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
        string arguments = BuildArguments();

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
    private string BuildArguments()
    {
        if (!launchOptions.AreCommandLineArgumentsEnabled.Value)
        {
            return string.Empty;
        }

        CommandLineBuilder builder = new();
        builder.Append("-screen-fullscreen", launchOptions.IsFullScreen.Value ? 1 : 0);
        builder.AppendIf(launchOptions.IsBorderless.Value, "-popupwindow");
        builder.AppendIf(launchOptions.IsExclusive.Value, "-window-mode", "exclusive");
        builder.AppendIf(launchOptions.IsScreenWidthEnabled.Value, "-screen-width", launchOptions.ScreenWidth.Value);
        builder.AppendIf(launchOptions.IsScreenHeightEnabled.Value, "-screen-height", launchOptions.ScreenHeight.Value);
        builder.AppendIf(launchOptions.IsMonitorEnabled.Value, "-monitor", launchOptions.Monitor.Value?.Value ?? 1);

        return builder.ToString().Trim();
    }
}
