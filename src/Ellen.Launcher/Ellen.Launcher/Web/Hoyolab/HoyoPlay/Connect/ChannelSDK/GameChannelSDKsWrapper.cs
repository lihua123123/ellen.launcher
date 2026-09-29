// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;

public sealed class GameChannelSDKsWrapper
{
    [JsonPropertyName("game_channel_sdks")]
    public List<GameChannelSDK> GameChannelSDKs { get; set; } = default!;
}
