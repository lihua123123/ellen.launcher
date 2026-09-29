// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Passport;

public sealed class Risk
{
    [JsonPropertyName("risk_ticket")]
    public required string RiskTicket { get; set; }

    [JsonPropertyName("verify_str")]
    public string? VerifyString { get; set; }
}