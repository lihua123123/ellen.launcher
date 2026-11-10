// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game.QuickLaunch;

public interface IGameInstallationLocator
{
    /// <summary>
    /// 定位游戏安装位置。不会阻塞调用方线程，不会弹出任何 UI。
    /// </summary>
    ValueTask<GameInstallation> LocateAsync(GameLaunchKind kind, CancellationToken token = default);
}
