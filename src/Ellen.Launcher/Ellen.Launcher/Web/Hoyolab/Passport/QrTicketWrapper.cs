// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Passport;

public sealed class QrTicketWrapper
{
    [JsonPropertyName("ticket")]
    public string Ticket { get; set; } = default!;
}