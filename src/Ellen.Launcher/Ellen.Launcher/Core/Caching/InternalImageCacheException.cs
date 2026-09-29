// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;

namespace Ellen.Launcher.Core.Caching;

public class publicImageCacheException : Exception, IpublicException
{
    private publicImageCacheException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private publicImageCacheException(string message)
        : base(message)
    {
    }

    public static publicImageCacheException Throw(string message, Exception innerException)
    {
        throw new publicImageCacheException(message, innerException);
    }

    public static publicImageCacheException Throw(string message)
    {
        throw new publicImageCacheException(message);
    }
}