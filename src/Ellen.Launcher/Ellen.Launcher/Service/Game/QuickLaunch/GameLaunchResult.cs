// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game.QuickLaunch;

public enum GameLaunchOutcome
{
    /// <summary>
    /// 已成功拉起游戏进程。
    /// </summary>
    Started,

    /// <summary>
    /// 游戏已在运行。
    /// </summary>
    AlreadyRunning,

    /// <summary>
    /// 找不到游戏可执行文件。
    /// </summary>
    NotInstalled,

    /// <summary>
    /// 启动失败，详见 <see cref="GameLaunchResult.Message"/>。
    /// </summary>
    Failed,
}

public sealed record GameLaunchResult(GameLaunchOutcome Outcome, string? Message = null);
