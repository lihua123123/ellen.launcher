// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using JetBrains.Annotations;
using Ellen.Launcher.Core.IO;

namespace Ellen.Launcher.Factory.Picker;

public interface IFileSystemPickerInteraction
{
    ValueResult<bool, ValueFile> PickFile([LocalizationRequired] string? title, string? defaultFileName, string? filterName, string? filterType);

    ValueResult<bool, ValueFile> SaveFile([LocalizationRequired] string? title, string? defaultFileName, string? filterName, string? filterType);

    ValueResult<bool, string?> PickFolder([LocalizationRequired] string? title);
}