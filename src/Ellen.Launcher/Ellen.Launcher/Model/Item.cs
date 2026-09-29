// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Model;

public class Item : INameIcon<Uri>
{
    public string Name { get; init; } = default!;

    public Uri Icon { get; init; } = default!;

    public Uri Badge { get; init; } = default!;

    public QualityType Quality { get; init; }

    public uint Id { get; init; }
}