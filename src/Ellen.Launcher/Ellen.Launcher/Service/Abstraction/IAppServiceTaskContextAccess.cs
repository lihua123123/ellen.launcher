// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Abstraction;

public interface IAppServiceTaskContextAccess : IAppService
{
    ITaskContext TaskContext { get; }
}