// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Verification;

public sealed class VerificationResult
{
    [JsonPropertyName("challenge")]
    public string? Challenge { get; set; }
}