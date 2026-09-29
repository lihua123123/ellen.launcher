// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Web.Hoyolab.Bbs.Home;

public sealed class Carousels
{
    [JsonPropertyName("position")]
    public required int Position { get; init; }

    [JsonPropertyName("data")]
    public required ImmutableArray<HomeBanner> Data { get; init; }
}