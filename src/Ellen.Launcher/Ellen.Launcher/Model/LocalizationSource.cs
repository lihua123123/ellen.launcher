// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model;

[Flags]
public enum LocalizationSource
{
    None = 0b0000,
    Ellen = 0b0001,
    Crowdin = 0b0010,
    Gemini = 0b0100,
}