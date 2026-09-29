// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Model.Metadata.Reliquary;

public sealed class ReliquaryMainAffixLevel
{
    public required QualityType Rank { get; init; }

    public required uint Level { get; init; }

    public required TypeValueCollection<FightProperty, float> Properties { get; init; }
}