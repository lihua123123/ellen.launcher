// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Request.Builder.Abstraction;

namespace Ellen.Launcher.Web.Request.Builder;

public static class HttpRequestMessageBuilderFactoryExtension
{
    extension(IHttpRequestMessageBuilderFactory factory)
    {
        public HttpRequestMessageBuilder Create(string requestUri)
        {
            return factory.Create().SetRequestUri(requestUri);
        }
    }
}