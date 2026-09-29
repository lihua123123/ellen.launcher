// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;

public sealed class DetailedWeapon : Weapon
{
    [JsonPropertyName("promote_level")]
    public PromoteLevel PromoteLevel { get; set; }

    // Ignored field string name
    // Ignored field string type_name
    // Ignored field string desc
    // Ignored filed Property main_property
    // Ignored field Property sub_property
}