// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.ViewModel.Calendar;

public sealed class CalendarMetadataContext2
{
    public required CalendarMetadataContext MetadataContext { get; init; }

    public required ILookup<MonthAndDay, Avatar> AvatarBirthdays { get; init; }

    public required ILookup<MaterialId, CalendarItem> MaterialItems { get; init; }
}