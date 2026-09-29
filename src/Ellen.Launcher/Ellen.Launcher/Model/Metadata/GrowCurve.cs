// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata;

public sealed class GrowCurve
{
    public required Level Level { get; init; }

    public required TypeValueCollection<GrowCurveType, float> Curves { get; init; }
}