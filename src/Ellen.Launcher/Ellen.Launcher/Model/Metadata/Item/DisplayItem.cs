// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata.Item;

public class DisplayItem : IDefaultIdentity<MaterialId>
{
    public required MaterialId Id { get; init; }

    public required QualityType RankLevel { get; init; }

    public required ItemType ItemType { get; init; }

    public required string Icon { get; init; }

    public required string Name { get; init; }

    public uint Rank { get; init; }

    public string? Description { get; init; }

    public string? TypeDescription { get; init; }
}