// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata;

namespace Ellen.Launcher.ViewModel.GachaLog;

public sealed class CountdownHistory
{
    public CountdownHistory(GachaEvent gachaEvent)
    {
        LastTime = gachaEvent.To;
        FormattedTime = $"{gachaEvent.To:yyyy-MM-dd}";
        FormattedVersionOrder = $"{gachaEvent.Version} {(gachaEvent.Order is 1 ? SH.ViewModelGachaLogCountdownOrderUp : SH.ViewModelGachaLogCountdownOrderDown)}";
        Banner = gachaEvent.Banner;
    }

    /// <summary>
    /// 绝区零「计时」用的获取记录：没有卡池横幅，用物品图标代替。
    /// </summary>
    public CountdownHistory(DateTimeOffset time, string channelName, Uri image)
    {
        LastTime = time;
        FormattedTime = $"{time:yyyy-MM-dd}";
        FormattedVersionOrder = channelName;
        Banner = image;
    }

    public string FormattedTime { get; }

    public string FormattedVersionOrder { get; }

    public Uri Banner { get; }

    public DateTimeOffset LastTime { get; }
}