// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;

public class HardChallengeSimpleAvatar
{
    [JsonPropertyName("avatar_id")]
    public required AvatarId AvatarId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("element")]
    public required string Element { get; init; }

    [JsonPropertyName("image")]
    public required string Image { get; init; }

    [JsonPropertyName("rarity")]
    public required QualityType Rarity { get; init; }
}