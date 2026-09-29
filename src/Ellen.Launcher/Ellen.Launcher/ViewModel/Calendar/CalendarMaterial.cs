// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Item;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Calendar;

public sealed class CalendarMaterial
{
    public required Material Inner { get; init; }

    public required ImmutableArray<CalendarItem> Items { get; init; }

    public bool Highlight { get; set; }

    public required RotationalMaterialIdEntry InnerEntry { get; init; }
}