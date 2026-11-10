// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.IO;

namespace Ellen.Launcher.Service.Game.QuickLaunch;

/// <summary>
/// 游戏安装路径的来源。
/// </summary>
public enum GameInstallSource
{
    /// <summary>
    /// 未找到。
    /// </summary>
    None,

    /// <summary>
    /// 用户手动指定（启动器设置 / 文件选择器）。
    /// </summary>
    UserConfigured,

    /// <summary>
    /// 从官方启动器的卸载注册表项 + config.ini 推导（与 Starward 的识别方式一致）。
    /// </summary>
    Registry,

    /// <summary>
    /// 从游戏输出的 Unity 日志推导。
    /// </summary>
    UnityLog,
}

/// <summary>
/// 一次游戏安装检测的结果。
/// </summary>
public sealed class GameInstallation
{
    public required GameLaunchKind Kind { get; init; }

    /// <summary>
    /// 游戏可执行文件的完整路径，未找到时为 <see langword="null"/>。
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// <c>config.ini</c> 中的 game_version，读取失败时为 <see langword="null"/>。
    /// </summary>
    public string? Version { get; init; }

    public GameInstallSource Source { get; init; }

    public bool IsInstalled
    {
        get => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
    }

    public string? InstallDirectory
    {
        get => string.IsNullOrEmpty(ExecutablePath) ? null : Path.GetDirectoryName(ExecutablePath);
    }

    public static GameInstallation NotFound(GameLaunchKind kind)
    {
        return new() { Kind = kind };
    }
}
