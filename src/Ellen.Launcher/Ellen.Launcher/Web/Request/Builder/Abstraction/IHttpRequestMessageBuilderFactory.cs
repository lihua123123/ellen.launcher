// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Request.Builder.Abstraction;

public interface IHttpRequestMessageBuilderFactory
{
    HttpRequestMessageBuilder Create();
}