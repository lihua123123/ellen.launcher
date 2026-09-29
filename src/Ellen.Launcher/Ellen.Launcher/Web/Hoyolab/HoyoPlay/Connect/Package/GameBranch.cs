// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Package;

public sealed class GameBranch
{
    [JsonPropertyName("major")]
    public Package Major { get; set; } = default!;

    [JsonPropertyName("patches")]
    public List<Package> Patches { get; set; } = default!;
}
