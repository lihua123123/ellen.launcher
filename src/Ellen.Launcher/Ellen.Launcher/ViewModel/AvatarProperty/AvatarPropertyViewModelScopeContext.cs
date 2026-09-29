// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DataTransfer;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Service.AvatarInfo;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.User;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

[Service(ServiceLifetime.Scoped)]
public sealed partial class AvatarPropertyViewModelScopeContext
{
    [GeneratedConstructor]
    public partial AvatarPropertyViewModelScopeContext(IServiceProvider serviceProvider);

    public partial IContentDialogFactory ContentDialogFactory { get; }

    public partial IServiceScopeFactory ServiceScopeFactory { get; }

    public partial ICultivationService CultivationService { get; }

    public partial IAvatarInfoService AvatarInfoService { get; }

    public partial IClipboardProvider ClipboardProvider { get; }

    public partial IServiceProvider ServiceProvider { get; }

    public partial IMetadataService MetadataService { get; }

    public partial ITaskContext TaskContext { get; }

    public partial IUserService UserService { get; }

    public partial IMessenger Messenger { get; }
}