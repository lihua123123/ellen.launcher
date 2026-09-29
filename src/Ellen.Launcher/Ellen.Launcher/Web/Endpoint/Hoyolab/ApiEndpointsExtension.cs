// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Branch;

namespace Ellen.Launcher.Web.Endpoint.Hoyolab;

public static class ApiEndpointsExtension
{
    extension(IApiEndpoints apiEndpoints)
    {
        public string SophonChunkGetBuildByBranch(BranchWrapper wrapper)
        {
            return string.Equals(wrapper.Branch, "PREDOWNLOAD", StringComparison.OrdinalIgnoreCase)
                ? apiEndpoints.SophonChunkGetBuildNoTag(wrapper)
                : apiEndpoints.SophonChunkGetBuild(wrapper);
        }
    }
}