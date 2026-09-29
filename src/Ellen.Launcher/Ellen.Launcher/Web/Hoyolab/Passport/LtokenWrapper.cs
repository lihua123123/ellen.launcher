// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Passport;

public sealed class LTokenWrapper
{
    [JsonPropertyName("ltoken")]
    public string LToken { get; set; } = default!;
}