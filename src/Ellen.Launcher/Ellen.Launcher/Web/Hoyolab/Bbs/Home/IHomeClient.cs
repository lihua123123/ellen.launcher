// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Web.Hoyolab.Bbs.Home;

public interface IHomeClient
{
    ValueTask<Response<NewHomeNewInfo>> GetNewHomeInfoAsync(int gid, CancellationToken token = default);
}