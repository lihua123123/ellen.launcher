// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class CardPoolWeapon : CardPoolItem
{
    [JsonPropertyName("id")]
    public required WeaponId Id { get; set; }

    [JsonPropertyName("wiki_url")]
    public required Uri WikiUrl { get; set; }
}