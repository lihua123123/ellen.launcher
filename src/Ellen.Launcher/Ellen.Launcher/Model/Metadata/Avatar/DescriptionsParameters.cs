// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class DescriptionsParameters
{
    public required ImmutableArray<string> Descriptions { get; init; }

    public required IdLevelParametersCollection<ProudSkillId, SkillLevel, float> Parameters { get; init; }

    public int[]? DescriptionList { get; init; }

    public int[]? SpecialDescriptionList { get; init; }
}