// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Bridge.Model;

public sealed class SharePayload
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;

    [JsonPropertyName("content")]
    public ShareContent Content { get; set; } = default!;
}