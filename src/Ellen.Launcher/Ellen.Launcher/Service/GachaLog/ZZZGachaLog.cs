// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Frozen;

namespace Ellen.Launcher.Service.GachaLog;

public static class ZZZGachaLog
{
    /// <summary>
    /// 绝区零的调频卡池类型：常驻频段 / 独家频段 / 音擎频段 / 邦布频段 / 独家重映 / 音擎回响
    /// </summary>
    public static readonly FrozenSet<GachaType> QueryTypes =
    [
        GachaType.ZZZStandardChannel,
        GachaType.ZZZExclusiveChannel,
        GachaType.ZZZWEngineChannel,
        GachaType.ZZZBangbooChannel,
        GachaType.ZZZExclusiveRescreening,
        GachaType.ZZZWEngineReverberation,
    ];
}
