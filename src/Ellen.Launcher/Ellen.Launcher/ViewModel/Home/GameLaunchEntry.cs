// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Ellen.Launcher.Service.Game.QuickLaunch;

namespace Ellen.Launcher.ViewModel.Home;

/// <summary>
/// 首页「游戏启动」卡片中的一个游戏入口。
/// </summary>
public sealed partial class GameLaunchEntry : ObservableObject
{
    private GameLaunchEntry(GameLaunchKind kind, string title)
    {
        Kind = kind;
        Title = title;
    }

    public GameLaunchKind Kind { get; }

    public string Title { get; }

    /// <summary>
    /// 游戏图标。优先取游戏可执行文件自带的图标（与官方启动器一致），
    /// 未安装 / 提取失败时退回随包内置的占位图标。
    /// </summary>
    [ObservableProperty]
    public partial ImageSource? IconSource { get; set; }

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool IsLaunching { get; set; }

    [ObservableProperty]
    public partial string? Version { get; set; }

    [ObservableProperty]
    public partial string? ExecutablePath { get; set; }

    /// <summary>
    /// 当前米游社账号在该游戏下的角色 UID；账号没有该游戏角色时为 <see langword="null"/>。
    /// </summary>
    [ObservableProperty]
    public partial string? AccountUid { get; set; }

    /// <summary>
    /// 磁贴第二行：安装状态 / 该游戏下的账号。
    /// 账号没有该游戏角色时显示「需在游戏内登录」，即启动后由游戏自己走登录流程。
    /// 运行状态由按钮文字表示，避免同一个「运行中」出现两次。
    /// </summary>
    public string DetailText
    {
        get
        {
            if (!IsInstalled)
            {
                return SH.ViewCardLaunchGameStatusNotInstalled;
            }

            return string.IsNullOrEmpty(AccountUid)
                ? SH.ViewCardLaunchGameNeedsInGameLogin
                : SH.FormatViewCardLaunchGameUid(AccountUid);
        }
    }

    public string ActionText
    {
        get
        {
            if (IsRunning)
            {
                return SH.ViewCardLaunchGameActionRunning;
            }

            if (IsLaunching)
            {
                return SH.ViewCardLaunchGameActionLaunching;
            }

            return IsInstalled ? SH.ViewCardLaunchGameActionLaunch : SH.ViewCardLaunchGameActionSetPath;
        }
    }

    /// <summary>
    /// 运行中也保持可点击：点一下会重新检测进程，游戏已退出就立刻启动，不必等轮询刷新。
    /// </summary>
    public bool CanLaunch
    {
        get => IsInstalled && !IsLaunching;
    }

    /// <summary>
    /// 悬停提示：版本 + 可执行文件路径。
    /// </summary>
    public string ToolTipText
    {
        get
        {
            if (!IsInstalled)
            {
                return SH.ViewCardLaunchGamePathNotSet;
            }

            return string.IsNullOrEmpty(Version)
                ? ExecutablePath ?? SH.ViewCardLaunchGamePathNotSet
                : $"{Version} · {ExecutablePath}";
        }
    }

    public static GameLaunchEntry Create(GameLaunchKind kind, string title)
    {
        return new(kind, title);
    }

    public void ApplyInstallation(GameInstallation installation)
    {
        ExecutablePath = installation.ExecutablePath;
        Version = installation.Version;
        IsInstalled = installation.IsInstalled;
    }

    partial void OnIsInstalledChanged(bool value)
    {
        NotifyDerivedStateChanged();
    }

    partial void OnIsRunningChanged(bool value)
    {
        NotifyDerivedStateChanged();
    }

    partial void OnIsLaunchingChanged(bool value)
    {
        NotifyDerivedStateChanged();
    }

    partial void OnVersionChanged(string? value)
    {
        NotifyDerivedStateChanged();
    }

    partial void OnExecutablePathChanged(string? value)
    {
        NotifyDerivedStateChanged();
    }

    partial void OnAccountUidChanged(string? value)
    {
        NotifyDerivedStateChanged();
    }

    private void NotifyDerivedStateChanged()
    {
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(ActionText));
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(ToolTipText));
    }
}
