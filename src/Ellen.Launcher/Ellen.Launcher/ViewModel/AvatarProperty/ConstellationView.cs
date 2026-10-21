// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public sealed class ConstellationView : NameIconDescription
{
    public bool IsActivated { get; set; }

    public string? SpecialDescription { get; set; }

    /// <summary>
    /// 绝区零影画暂无图标，先用 1-6 的数字占位。
    /// </summary>
    public int Number { get; set; }

    public bool ShowNumber { get; set; }

    public bool ShowLock { get; set; }
}