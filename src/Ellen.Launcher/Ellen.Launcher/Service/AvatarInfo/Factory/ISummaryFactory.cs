// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

public interface ISummaryFactory
{
    ValueTask<Summary> CreateAsync(SummaryFactoryMetadataContext context, IEnumerable<Model.Entity.AvatarInfo> avatarInfos, CancellationToken token);
}