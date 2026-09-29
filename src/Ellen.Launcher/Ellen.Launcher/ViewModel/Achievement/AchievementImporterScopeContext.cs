// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DataTransfer;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Service.Yae;

namespace Ellen.Launcher.ViewModel.Achievement;

[Service(ServiceLifetime.Scoped)]
public sealed partial class AchievementImporterScopeContext
{
    [GeneratedConstructor]
    public partial AchievementImporterScopeContext(IServiceProvider serviceProvider);

    public partial IFileSystemPickerInteraction FileSystemPickerInteraction { get; }

    public partial JsonSerializerOptions JsonSerializerOptions { get; }

    public partial IContentDialogFactory ContentDialogFactory { get; }

    public partial IClipboardProvider ClipboardProvider { get; }

    public partial IServiceProvider ServiceProvider { get; }

    public partial IYaeService YaeService { get; }

    public partial IMessenger Messenger { get; }
}