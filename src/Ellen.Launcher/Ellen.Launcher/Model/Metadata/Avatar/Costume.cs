// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.UI.Xaml.Data;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed partial class Costume : IPropertyValuesProvider
{
    public required CostumeId Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required bool IsDefault { get; init; }

    public string? FrontIcon { get; init; }

    public string? SideIcon { get; init; }
}