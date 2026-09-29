// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActLiBen : Act
{
    [JsonPropertyName("liben_detail")]
    public required ActLiBenDetail LiBenDetail { get; init; }
}