// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Bridge.Model;

public sealed class PushPagePayload
{
    [JsonPropertyName("page")]
    public string Page { get; set; } = default!;
}