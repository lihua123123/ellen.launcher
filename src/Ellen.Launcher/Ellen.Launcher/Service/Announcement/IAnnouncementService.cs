// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Common.Announcement;

namespace Ellen.Launcher.Service.Announcement;

public interface IAnnouncementService
{
    ValueTask<AnnouncementWrapper?> GetAnnouncementWrapperAsync(string languageCode, Region region, CancellationToken token = default);
}