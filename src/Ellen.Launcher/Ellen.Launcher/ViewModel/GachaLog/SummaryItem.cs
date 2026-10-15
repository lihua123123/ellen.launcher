// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;

namespace Ellen.Launcher.ViewModel.GachaLog;

public sealed class SummaryItem : Item
{
    public bool IsUp { get; set; }

    public bool IsGuarantee { get; set; }

    // Used in ListView for ProgressBar Maximum Value
    public int GuaranteeOrangeThreshold { get; set; }

    public int LastPull { get; set; }

    // Total pulls since the last rate-up (up) 5-star, combining both lost-50/50 and guarantee pity.
    // Only meaningful for IsUp == true items.
    public int TotalCyclePull { get; set; }

    // 抽卡记录时间直接显示落库的「墙钟」(Wall Clock)，即游戏内时间（国服为东八区时间）。
    // 绝区零从 UIGF 导入时偏移被记为 +00:00，原神实抓时被记为 +08:00，二者墙钟都已是国服时间；
    // 若用 ToLocalTime() 会按本机时区再偏移一次（+8），导致国服用户看到 09:33 -> 17:33 的错误。
    // IsUp 判定（ZZZGachaUpHelper）同样基于 Time.DateTime（墙钟）做 cutoff 比较，保持口径一致。
    public string FormattedTime
    {
        get => $"{Time.DateTime:yyyy.MM.dd HH:mm:ss}";
    }

    public Windows.UI.Color Color { get; set; }

    public DateTimeOffset Time { get; set; }
}