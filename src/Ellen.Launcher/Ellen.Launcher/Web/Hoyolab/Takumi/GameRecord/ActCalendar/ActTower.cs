// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActTower : Act
{
    [JsonPropertyName("tower_detail")]
    public required ActTowerDetail TowerDetail { get; init; }
}