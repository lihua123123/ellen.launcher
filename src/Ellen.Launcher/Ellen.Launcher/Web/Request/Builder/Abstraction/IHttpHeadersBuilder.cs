// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Abstraction;
using System.Net.Http.Headers;

namespace Ellen.Launcher.Web.Request.Builder.Abstraction;

public interface IHttpHeadersBuilder<out T> : IBuilder
    where T : HttpHeaders
{
    T Headers { get; }
}