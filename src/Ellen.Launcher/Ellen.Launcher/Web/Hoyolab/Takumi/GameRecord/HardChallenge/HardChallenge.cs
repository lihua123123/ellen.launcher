// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;

public sealed class HardChallenge
{
    [JsonPropertyName("data")]
    public required ImmutableArray<HardChallengeData> Data { get; init; }

    [JsonPropertyName("is_unlock")]
    public required bool IsUnlock { get; init; }

    [JsonPropertyName("links")]
    public required HardChallengeLinks Links { get; init; }
}