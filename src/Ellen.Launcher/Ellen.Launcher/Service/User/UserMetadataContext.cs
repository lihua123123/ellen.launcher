// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Avatar;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.User;

public class UserMetadataContext : IUserMetadataContext
{
    public ImmutableArray<ProfilePicture> ProfilePictures { get; set; } = default!;
}
