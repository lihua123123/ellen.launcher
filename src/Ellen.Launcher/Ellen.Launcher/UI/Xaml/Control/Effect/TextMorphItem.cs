// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.UI.Xaml.Control.Effect;

public sealed class TextMorphItem
{
    public required string Text { get; init; }

    public required DoubleTimeline Timeline { get; init; }
}