// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;

public sealed class HardChallengeMonster
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("level")]
    public required uint Level { get; init; }

    [JsonPropertyName("icon")]
    public required Uri Icon { get; init; }

    [JsonPropertyName("desc")]
    public required ImmutableArray<string> Descriptions { get; set; }

    [JsonPropertyName("tags")]
    public required ImmutableArray<HardChallengeMonsterTag> Tags { get; init; }

    [JsonPropertyName("monster_id")]
    public required MonsterId MonsterId { get; init; }
}