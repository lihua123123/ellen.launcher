// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;

public sealed class HardChallengePopularity
{
    [JsonPropertyName("avatar_list")]
    public required ImmutableArray<HardChallengeSimpleAvatar> AvatarList { get; init; }
}