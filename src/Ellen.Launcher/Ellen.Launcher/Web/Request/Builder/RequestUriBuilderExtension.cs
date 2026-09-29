// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Abstraction;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using System.Diagnostics;

namespace Ellen.Launcher.Web.Request.Builder;

public static class RequestUriBuilderExtension
{
    extension<T>(T builder)
        where T : class, IRequestUriBuilder
    {
        [DebuggerStepThrough]
        public T SetRequestUri(string? requestUri, UriKind uriKind = UriKind.RelativeOrAbsolute)
        {
            return builder.SetRequestUri(requestUri is null ? null : new Uri(requestUri, uriKind));
        }

        [DebuggerStepThrough]
        public T SetRequestUri(Uri? requestUri)
        {
            return builder.Configure(builder => builder.RequestUri = requestUri);
        }
    }
}