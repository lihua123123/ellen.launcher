// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.UI.Xaml.Data;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Calendar;

public sealed partial class CalendarDay : IPropertyValuesProvider
{
    public required DateTimeOffset Date { get; init; }

    public required int DayInMonth { get; init; }

    public required string DayName { get; init; }

    public required ImmutableArray<Item> BirthDayAvatars { get; init; }

    public required ImmutableArray<CalendarMaterial> Materials { get; init; }
}