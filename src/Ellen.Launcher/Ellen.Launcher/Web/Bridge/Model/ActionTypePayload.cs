// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Bridge.Model;

public sealed class ActionTypePayload
{
    [JsonPropertyName("action_type")]
    public string ActionType { get; set; } = default!;
}