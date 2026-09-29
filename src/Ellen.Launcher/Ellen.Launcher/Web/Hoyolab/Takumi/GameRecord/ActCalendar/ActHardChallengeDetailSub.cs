// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActHardChallengeDetailSub
{
    [JsonPropertyName("seconds")]
    public required int Seconds { get; init; }

    [JsonPropertyName("x")]
    public required int X { get; init; }

    [JsonPropertyName("y")]
    public required int Y { get; init; }
}