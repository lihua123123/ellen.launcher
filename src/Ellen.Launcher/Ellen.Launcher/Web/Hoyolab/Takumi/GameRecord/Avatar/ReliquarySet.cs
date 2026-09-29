// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;

public sealed class ReliquarySet
{
    [JsonPropertyName("id")]
    public ReliquarySetId Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("affixes")]
    public List<ReliquaryAffix> Affixes { get; set; } = default!;
}