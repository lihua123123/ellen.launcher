// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

public interface IInfrastructureGitRepositoryEndpoints : IInfrastructureRootAccess
{
    string GitRepository(string name)
    {
        return $"{Root}/git-repository/all?name={name}";
    }
}