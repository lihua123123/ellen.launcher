// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Passport;

public sealed class UidGameToken
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = default!;

    [JsonPropertyName("token")]
    public string GameToken { get; set; } = default!;
}
