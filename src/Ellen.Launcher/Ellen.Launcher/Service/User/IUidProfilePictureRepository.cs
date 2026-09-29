// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;

namespace Ellen.Launcher.Service.User;

public interface IUidProfilePictureRepository : IRepository<UidProfilePicture>
{
    UidProfilePicture? SingleUidProfilePictureOrDefaultByUid(string uid);

    void UpdateUidProfilePicture(UidProfilePicture profilePicture);

    void DeleteUidProfilePictureByUid(string uid);
}