// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Passport;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Auth;

public sealed class ActionTicketWrapper
{
    [JsonPropertyName("ticket")]
    public string Ticket { get; set; } = default!;

    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("account_info")]
    public UserInformation AccountInfo { get; set; } = default!;
}
