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

    public string FormattedTime
    {
        get => $"{Time.ToLocalTime():yyy.MM.dd HH:mm:ss}";
    }

    public Windows.UI.Color Color { get; set; }

    public DateTimeOffset Time { get; set; }
}