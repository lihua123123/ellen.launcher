// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.HoyoPlay;

public sealed class OfficialLauncherBackground
{
    [JsonPropertyName("retcode")]
    public int Retcode { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;

    [JsonPropertyName("data")]
    public OfficialLauncherBackgroundData? Data { get; init; }
}

public sealed class OfficialLauncherBackgroundData
{
    [JsonPropertyName("game_info_list")]
    public List<OfficialLauncherGameInfo> GameInfoList { get; init; } = default!;
}

public sealed class OfficialLauncherGameInfo
{
    [JsonPropertyName("game")]
    public OfficialLauncherGame Game { get; init; } = default!;

    [JsonPropertyName("backgrounds")]
    public List<OfficialLauncherBackgroundItem> Backgrounds { get; init; } = default!;
}

public sealed class OfficialLauncherGame
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = default!;

    [JsonPropertyName("biz")]
    public string Biz { get; init; } = default!;
}

public sealed class OfficialLauncherBackgroundItem
{
    [JsonPropertyName("video")]
    public OfficialLauncherVideo? Video { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = default!;
}

public sealed class OfficialLauncherVideo
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = default!;
}
