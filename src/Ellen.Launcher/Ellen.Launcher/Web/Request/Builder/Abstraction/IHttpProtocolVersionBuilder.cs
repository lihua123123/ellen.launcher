// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Abstraction;

namespace Ellen.Launcher.Web.Request.Builder.Abstraction;

public interface IHttpProtocolVersionBuilder : IBuilder
{
    Version Version { get; set; }
}