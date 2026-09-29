// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;

namespace Ellen.Launcher.Service.User;

public interface IUserMetadataContext : IMetadataContext,
    IMetadataArrayProfilePictureSource;
