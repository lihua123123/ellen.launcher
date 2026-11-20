// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Model.InterChange.GachaLog;

// ReSharper disable once InconsistentNaming
public sealed class UIGFEntry<TItem>
{
    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required ulong Uid { get; init; }

    [JsonPropertyName("timezone")]
    public required int TimeZone { get; init; }

    [JsonPropertyName("lang")]
    public string? Language { get; init; }

    [JsonPropertyName("list")]
    public required ImmutableArray<TItem> List { get; init; }
}