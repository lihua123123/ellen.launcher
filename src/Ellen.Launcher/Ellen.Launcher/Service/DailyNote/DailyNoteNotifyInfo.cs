// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.DailyNote;

public readonly struct DailyNoteNotifyInfo
{
    public readonly string Title;
    public readonly string AdaptiveHint;
    public readonly string Hint;

    public DailyNoteNotifyInfo(string title, string adaptiveHint, string hint)
    {
        Title = title;
        AdaptiveHint = adaptiveHint;
        Hint = hint;
    }
}