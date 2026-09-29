// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DataTransfer;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Service.Achievement;
using Ellen.Launcher.Service.Metadata;

namespace Ellen.Launcher.ViewModel.Achievement;

[Service(ServiceLifetime.Scoped)]
public sealed partial class AchievementViewModelScopeContext
{
    [GeneratedConstructor]
    public partial AchievementViewModelScopeContext(IServiceProvider serviceProvider);

    public partial IFileSystemPickerInteraction FileSystemPickerInteraction { get; }

    public partial ILogger<AchievementViewModelScopeContext> Logger { get; }

    public partial JsonSerializerOptions JsonSerializerOptions { get; }

    public partial IContentDialogFactory ContentDialogFactory { get; }

    public partial AchievementImporter AchievementImporter { get; }

    public partial IAchievementService AchievementService { get; }

    public partial IClipboardProvider ClipboardProvider { get; }

    public partial IServiceProvider ServiceProvider { get; }

    public partial IMetadataService MetadataService { get; }

    public partial ITaskContext TaskContext { get; }

    public partial IMessenger Messenger { get; }
}