// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Core.LifeCycle;

namespace Ellen.Launcher.Factory.Picker;

[Service(ServiceLifetime.Transient, typeof(IFileSystemPickerInteraction))]
public sealed partial class FileSystemPickerInteraction : IFileSystemPickerInteraction
{
    private readonly ICurrentXamlWindowReference currentWindowReference;

    [GeneratedConstructor]
    public partial FileSystemPickerInteraction(IServiceProvider serviceProvider);

    public ValueResult<bool, ValueFile> PickFile(string? title, string? defaultFileName, string? filterName, string? filterType)
    {
        bool picked = FileSystem.PickFile(currentWindowReference.WindowHandle, title, defaultFileName, filterName, filterType, out string? path);
        return new(picked, path);
    }

    public ValueResult<bool, ValueFile> SaveFile(string? title, string? defaultFileName, string? filterName, string? filterType)
    {
        bool picked = FileSystem.SaveFile(currentWindowReference.WindowHandle, title, defaultFileName, filterName, filterType, out string? path);
        return new(picked, path);
    }

    public ValueResult<bool, string?> PickFolder(string? title)
    {
        bool picked = FileSystem.PickFolder(currentWindowReference.WindowHandle, title, out string? path);
        return new(picked, path);
    }
}