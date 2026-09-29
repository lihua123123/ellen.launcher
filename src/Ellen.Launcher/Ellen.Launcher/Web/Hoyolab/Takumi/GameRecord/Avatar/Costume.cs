// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;

public sealed class Costume
{
    [JsonPropertyName("id")]
    public CostumeId Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    // Ignored field string icon
}