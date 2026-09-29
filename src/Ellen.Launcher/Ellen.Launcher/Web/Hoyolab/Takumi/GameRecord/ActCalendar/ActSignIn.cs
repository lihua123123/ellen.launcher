// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActSignIn : Act
{
    [JsonPropertyName("sign_in_detail")]
    public required ActSignInDetail SignInDetail { get; init; }
}