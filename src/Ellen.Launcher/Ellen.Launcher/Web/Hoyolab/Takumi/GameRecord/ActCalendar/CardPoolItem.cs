// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public class CardPoolItem
{
    [JsonPropertyName("icon")]
    public required Uri Icon { get; set; }

    [JsonPropertyName("rarity")]
    public required QualityType Rarity { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}