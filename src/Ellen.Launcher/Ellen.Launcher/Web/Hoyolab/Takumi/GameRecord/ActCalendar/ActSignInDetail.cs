// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActSignInDetail
{
    [JsonPropertyName("status")]
    public required ActSignInStatus Status { get; init; }

    [JsonPropertyName("progress")]
    public required int Progress { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }
}