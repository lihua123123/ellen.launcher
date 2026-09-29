// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;
using InGameReliquary = Ellen.Launcher.Service.Yae.PlayerStore.Reliquary;

namespace Ellen.Launcher.Model.InterChange.Inventory;

// ReSharper disable once InconsistentNaming
public sealed class UIIFReliquary
{
    [JsonPropertyName("level")]
    public ReliquaryLevel Level { get; init; }

    [JsonPropertyName("mainPropId")]
    public ReliquaryMainAffixId MainPropId { get; init; }

    [JsonPropertyName("appendPropIdList")]
    public ImmutableArray<ReliquarySubAffixId> AppendPropIdList { get; init; }

    public static UIIFReliquary FromInGameReliquary(InGameReliquary reliquary)
    {
        return new()
        {
            Level = reliquary.Level,
            MainPropId = reliquary.MainPropId,
            AppendPropIdList = [.. reliquary.AppendPropIdList],
        };
    }
}