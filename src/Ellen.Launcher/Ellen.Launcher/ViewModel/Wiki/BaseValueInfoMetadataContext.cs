// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Wiki;

public sealed class BaseValueInfoMetadataContext
{
    public required ImmutableDictionary<Level, TypeValueCollection<GrowCurveType, float>> GrowCurveMap { get; set; }

    public required ImmutableDictionary<PromoteLevel, Promote>? PromoteMap { get; set; }
}