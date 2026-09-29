// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;

public sealed class RoleIdServer
{
    public RoleIdServer(PlayerUid uid)
    {
        RoleId = uid.Value;
        Server = uid.Region.Value;
    }

    [JsonPropertyName("role_id")]
    public string RoleId { get; }

    [JsonPropertyName("server")]
    public string Server { get; }
}