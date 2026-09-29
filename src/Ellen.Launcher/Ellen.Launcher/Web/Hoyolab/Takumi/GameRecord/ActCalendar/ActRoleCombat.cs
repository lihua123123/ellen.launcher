// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActRoleCombat : Act
{
    [JsonPropertyName("role_combat_detail")]
    public required ActRoleCombatDetail RoleCombatDetail { get; init; }
}