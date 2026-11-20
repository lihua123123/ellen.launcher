// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Passport;

/// <summary>
/// Token type used by the passport V2 token exchange endpoint.
/// </summary>
public enum PassportTokenType
{
    SToken = 1,
    LToken = 2,
    CookieToken = 4,
}

/// <summary>
/// Request body for account/ma-cn-session/app/exchange.
/// </summary>
public sealed class WebTokenExchangeRequest
{
    [JsonPropertyName("dst_token_type")]
    public int DestinationTokenType { get; set; }

    [JsonPropertyName("src_token")]
    public WebTokenExchangeSourceToken SourceToken { get; set; } = default!;

    [JsonPropertyName("mid")]
    public string Mid { get; set; } = default!;
}

public sealed class WebTokenExchangeSourceToken
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = default!;

    [JsonPropertyName("token_type")]
    public int TokenType { get; set; }
}

/// <summary>
/// Response data for the passport V2 token exchange endpoint.
/// </summary>
public sealed class WebTokenExchangeData
{
    [JsonPropertyName("token")]
    public WebTokenExchangeToken Token { get; set; } = default!;

    [JsonPropertyName("user_info")]
    public WebTokenExchangeUserInfo? UserInfo { get; set; }
}

public sealed class WebTokenExchangeToken
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = default!;

    [JsonPropertyName("token_type")]
    public int TokenType { get; set; }
}

public sealed class WebTokenExchangeUserInfo
{
    [JsonPropertyName("aid")]
    public string Aid { get; set; } = default!;
}
