// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Service.Backpack;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Yae;

namespace Ellen.Launcher.ViewModel.Backpack;

[Service(ServiceLifetime.Scoped)]
public sealed partial class BackpackViewModelScopeContext
{
    [GeneratedConstructor]
    public partial BackpackViewModelScopeContext(IServiceProvider serviceProvider);

    public partial IBackpackService BackpackService { get; }

    public partial IContentDialogFactory ContentDialogFactory { get; }

    public partial IMessenger Messenger { get; }

    public partial IFileSystemPickerInteraction FileSystemPickerInteraction { get; }

    public partial IMetadataService MetadataService { get; }

    public partial IServiceProvider ServiceProvider { get; }

    public partial ITaskContext TaskContext { get; }

    public partial IYaeService YaeService { get; }
}
