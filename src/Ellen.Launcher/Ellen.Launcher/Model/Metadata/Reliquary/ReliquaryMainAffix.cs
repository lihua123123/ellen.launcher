// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata.Reliquary;

public class ReliquaryMainAffix
{
    public required ReliquaryMainAffixId Id { get; init; }

    public required FightProperty Type { get; init; }
}