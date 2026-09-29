// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;

public sealed class ReliquaryProperty
{
    [JsonPropertyName("property_type")]
    public FightProperty PropertyType { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = default!;

    [JsonPropertyName("times")]
    public uint Times { get; set; }
}