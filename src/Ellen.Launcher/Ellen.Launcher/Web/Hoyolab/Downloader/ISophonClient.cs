// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Web.Hoyolab.Downloader;

public interface ISophonClient
{
    ValueTask<Response<SophonBuild>> GetBuildAsync(BranchWrapper branch, CancellationToken token = default);

    ValueTask<Response<SophonPatchBuild>> GetPatchBuildAsync(BranchWrapper branch, CancellationToken token = default);
}