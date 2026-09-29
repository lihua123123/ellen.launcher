// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Frozen;

namespace Ellen.Launcher.Service.GachaLog;

public static class GachaLog
{
    public static readonly FrozenSet<GachaType> QueryTypes =
    [
        GachaType.NewBie,
        GachaType.Standard,
        GachaType.ActivityAvatar,
        GachaType.ActivityWeapon,
        GachaType.ActivityCity,
        GachaType.UGCStandard,
        GachaType.UGCAvatarEventWish,
    ];
}