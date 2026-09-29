// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Request.Builder.Abstraction;
using System.Net.Http;
using System.Text;

namespace Ellen.Launcher.Web.Request.Builder;

public static class HttpContentSerializerExtension
{
    extension(IHttpContentSerializer serializer)
    {
        public HttpContent? Serialize<T>(T content, Encoding? encoding)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            return serializer.Serialize(content, typeof(T), encoding);
        }
    }
}