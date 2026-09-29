// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Net.Http.Headers;

namespace Ellen.Launcher.Web.Request.Builder.Abstraction;

public interface IHttpContentHeadersBuilder : IHttpContentBuilder, IHttpHeadersBuilder<HttpContentHeaders>;