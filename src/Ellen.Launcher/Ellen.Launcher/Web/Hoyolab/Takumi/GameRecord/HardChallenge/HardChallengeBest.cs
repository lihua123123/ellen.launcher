// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;

public sealed class HardChallengeBest
{
    [JsonPropertyName("difficulty")]
    public required HardChallengeDifficultyLevel Difficulty { get; init; }

    [JsonPropertyName("second")]
    public required int Seconds { get; init; }

    [JsonPropertyName("icon")]
    public required string Icon { get; init; }
}