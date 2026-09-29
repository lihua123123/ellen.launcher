// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;

public sealed class ReliquaryAffix
{
    [JsonPropertyName("activation_number")]
    public int ActivationNumber { get; set; }

    [JsonPropertyName("effect")]
    public string Effect { get; set; } = default!;
}