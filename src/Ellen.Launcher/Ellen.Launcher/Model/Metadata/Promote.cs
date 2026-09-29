// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata;

public sealed class Promote
{
    public required PromoteId Id { get; init; }

    public required PromoteLevel Level { get; init; }

    public required TypeValueCollection<FightProperty, float> AddProperties { get; init; }
}