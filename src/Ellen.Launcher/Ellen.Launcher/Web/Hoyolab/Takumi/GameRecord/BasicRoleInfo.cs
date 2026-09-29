// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;

public sealed class BasicRoleInfo
{
    [JsonPropertyName("AvatarUrl")]
    public string AvatarUrl { get; set; } = default!;

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = default!;

    [JsonPropertyName("region")]
    public string RegionName { get; set; } = default!;

    [JsonPropertyName("level")]
    public int Level { get; set; } = default!;
}