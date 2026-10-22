// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DataTransfer;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Service.AvatarInfo;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;

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

    public partial IZZZGachaMetadataService ZZZGachaMetadataService { get; }

    public partial IZZZCinemaImageService ZZZCinemaImageService { get; }

    public partial IZZZAvatarCacheService ZZZAvatarCacheService { get; }

    public partial BindingClient BindingClient { get; }

    public partial IZZZGameRecordClient ZZZGameRecordClient { get; }

    public partial IGachaLogRepository GachaLogRepository { get; }

    public partial ITaskContext TaskContext { get; }

    public partial IUserService UserService { get; }

    public partial IMessenger Messenger { get; }
}