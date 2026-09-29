// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Abstraction;
using System.Net.Http;

namespace Ellen.Launcher.Web.Request.Builder.Abstraction;

public interface IHttpRequestOptionsBuilder : IBuilder
{
    HttpRequestOptions Options { get; }
}