// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
using Ellen.Launcher.Core.Abstraction;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using System.Diagnostics;
using System.Net.Http;

namespace Ellen.Launcher.Web.Request.Builder;

public static class HttpRequestOptionsBuilderExtension
{
    extension<TBuilder>(TBuilder builder)
        where TBuilder : class, IHttpRequestOptionsBuilder
    {
        [DebuggerStepThrough]
        public TBuilder SetOptions<TValue>(HttpRequestOptionsKey<TValue> key, TValue value)
        {
            return builder.Configure(builder => builder.Options.Set(key, value));
        }
    }
}