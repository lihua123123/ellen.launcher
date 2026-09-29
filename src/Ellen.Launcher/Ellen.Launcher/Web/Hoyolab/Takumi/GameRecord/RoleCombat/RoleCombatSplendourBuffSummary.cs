// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.RoleCombat;

public sealed class RoleCombatSplendourBuffSummary
{
    [JsonPropertyName("total_level")]
    public required uint TotalLevel { get; init; }

    [JsonPropertyName("desc")]
    public required string Description { get; init; }
}