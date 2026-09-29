// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Passport;

public sealed class Link
{
    [JsonPropertyName("thirdparty")]
    public string Thirdparty { get; set; } = default!;

    [JsonPropertyName("union_id")]
    public string UnionId { get; set; } = default!;

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = default!;
}
