// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActDoubleDetail
{
    // 剩余双倍次数
    [JsonPropertyName("left")]
    public required int Left { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }
}