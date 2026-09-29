// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;

public sealed class HardChallengeAvatar : HardChallengeSimpleAvatar
{
    [JsonPropertyName("level")]
    public required Level Level { get; init; }

    /// <summary>
    /// 实际上是命座
    /// </summary>
    [JsonPropertyName("rank")]
    public required int Rank { get; init; }
}