// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Passport;

public sealed class TokenWrapper
{
    [JsonPropertyName("token_type")]
    public int TokenType { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = default!;
}