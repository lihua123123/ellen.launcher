// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Model.InterChange.GachaLog;

// ReSharper disable once InconsistentNaming
public class UIGF3
{
    [JsonRequired]
    [JsonPropertyName("info")]
    [JsonPropertyOrder(0)]
    public UIGF3Info Info { get; init; } = default!;

    [JsonPropertyName("list")]
    [JsonPropertyOrder(1)]
    public ImmutableArray<Hk4eItem> List { get; set; }
}
