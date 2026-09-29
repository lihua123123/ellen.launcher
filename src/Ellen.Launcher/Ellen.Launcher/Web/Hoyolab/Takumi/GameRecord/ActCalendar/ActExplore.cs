// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActExplore : Act
{
    [JsonPropertyName("explore_detail")]
    public required ActExploreDetail ExploreDetail { get; init; }
}