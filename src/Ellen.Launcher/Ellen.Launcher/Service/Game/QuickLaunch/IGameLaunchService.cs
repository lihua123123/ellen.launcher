// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game.QuickLaunch;

public interface IGameLaunchService
{
    ValueTask<GameInstallation> GetInstallationAsync(GameLaunchKind kind, CancellationToken token = default);

    /// <summary>
    /// 游戏进程是否正在运行。
    /// </summary>
    bool IsRunning(GameLaunchKind kind);

    /// <summary>
    /// 直接启动游戏（不经过官方启动器），与 Starward 的做法一致。
    /// </summary>
    ValueTask<GameLaunchResult> LaunchAsync(GameLaunchKind kind, CancellationToken token = default);
}
