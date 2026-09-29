// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

[ExtendedEnum]
public enum CardPoolStatus
{
    [LocalizationKey(nameof(SH.WebHoyolabGameRecordActCalendarCardPoolStatusUpcoming))]
    Upcoming = 1,

    [LocalizationKey(nameof(SH.WebHoyolabGameRecordActCalendarCardPoolStatusOngoing))]
    Ongoing = 2,
}