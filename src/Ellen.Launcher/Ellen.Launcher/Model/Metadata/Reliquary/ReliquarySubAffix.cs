// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata.Reliquary;

public sealed class ReliquarySubAffix : IDefaultIdentity<ReliquarySubAffixId>
{
    public required ReliquarySubAffixId Id { get; init; }

    public required FightProperty Type { get; init; }

    public required float Value { get; init; }
}