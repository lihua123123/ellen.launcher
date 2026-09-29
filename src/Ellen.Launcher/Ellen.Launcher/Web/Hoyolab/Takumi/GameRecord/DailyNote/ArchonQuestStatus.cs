// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.DailyNote;

[ExtendedEnum]
public enum ArchonQuestStatus
{
    [LocalizationKey(nameof(SH.WebDailyNoteArchonQuestStatusFinished))]
    StatusFinished,

    [LocalizationKey(nameof(SH.WebDailyNoteArchonQuestStatusOngoing))]
    StatusOngoing,

    [LocalizationKey(nameof(SH.WebDailyNoteArchonQuestStatusNotOpen))]
    StatusNotOpen,
}