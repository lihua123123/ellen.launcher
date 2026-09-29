// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;
using EntityAvatarInfo = Ellen.Launcher.Model.Entity.AvatarInfo;

namespace Ellen.Launcher.Service.AvatarInfo;

[Service(ServiceLifetime.Singleton, typeof(IAvatarInfoRepository))]
public sealed partial class AvatarInfoRepository : IAvatarInfoRepository
{
    [GeneratedConstructor]
    public partial AvatarInfoRepository(IServiceProvider serviceProvider);

    public partial IServiceProvider ServiceProvider { get; }

    public ImmutableArray<EntityAvatarInfo> GetAvatarInfoImmutableArrayByUid(string uid)
    {
        return this.ImmutableArray(i => i.Uid == uid);
    }

    public void RemoveAvatarInfoRangeByUid(string uid)
    {
        this.Delete(i => i.Uid == uid);
    }
}