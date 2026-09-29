// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;

public sealed class Avatar : Calculable
{
    [JsonPropertyName("avatar_level")]
    public QualityType AvatarLevel { get; set; }

    [JsonPropertyName("weapon_cat_id")]
    public WeaponType WeaponCatId { get; set; }

    [JsonPropertyName("element_attr_id")]
    public ElementAttributeId ElementAttrId { get; set; }

    [JsonPropertyName("promote_level")]
    public PromoteLevel PromoteLevel { get; set; }
}