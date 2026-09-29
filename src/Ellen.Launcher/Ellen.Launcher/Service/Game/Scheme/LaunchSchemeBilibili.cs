// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Service.Game.Scheme;

public sealed class LaunchSchemeBilibili : LaunchScheme
{
    private const string HoyoPlayLauncherBilibiliId = "umfgRO5gh5";
    private const string HoyoPlayGameBilibiliId = "T2S0Gz4Dr2";

    public LaunchSchemeBilibili(SubChannelType subChannel, bool isNotCompatOnly = true)
    {
        LauncherId = HoyoPlayLauncherBilibiliId;
        GameId = HoyoPlayGameBilibiliId;
        Channel = ChannelType.Bili;
        SubChannel = subChannel;
        IsOversea = false;
        IsNotCompatOnly = isNotCompatOnly;
    }
}