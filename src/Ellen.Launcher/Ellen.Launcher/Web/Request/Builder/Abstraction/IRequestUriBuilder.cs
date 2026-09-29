// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Abstraction;

namespace Ellen.Launcher.Web.Request.Builder.Abstraction;

public interface IRequestUriBuilder : IBuilder
{
    Uri? RequestUri { get; set; }
}