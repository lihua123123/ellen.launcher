// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.AvatarInfo.Factory;
using Ellen.Launcher.ViewModel.AvatarProperty;
using Ellen.Launcher.ViewModel.User;

namespace Ellen.Launcher.Service.AvatarInfo;

public interface IAvatarInfoService
{
    ValueTask<Summary?> GetSummaryAsync(SummaryFactoryMetadataContext context, UserAndUid userAndUid, RefreshOptionKind refreshOptionKind, CancellationToken token = default);
}