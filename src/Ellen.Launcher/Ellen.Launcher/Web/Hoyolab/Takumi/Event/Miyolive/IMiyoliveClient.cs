// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Miyolive;

public interface IMiyoliveClient
{
    ValueTask<Response<CodeListWrapper>> RefreshCodeAsync(string actId, CancellationToken token = default);
}