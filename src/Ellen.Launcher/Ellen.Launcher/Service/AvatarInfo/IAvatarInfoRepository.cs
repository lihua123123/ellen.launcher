// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;
using EntityAvatarInfo = Ellen.Launcher.Model.Entity.AvatarInfo;

namespace Ellen.Launcher.Service.AvatarInfo;

public interface IAvatarInfoRepository : IRepository<EntityAvatarInfo>
{
    void RemoveAvatarInfoRangeByUid(string uid);

    ImmutableArray<EntityAvatarInfo> GetAvatarInfoImmutableArrayByUid(string uid);
}