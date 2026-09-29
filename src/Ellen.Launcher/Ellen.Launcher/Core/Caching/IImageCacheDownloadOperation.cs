// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.IO;

namespace Ellen.Launcher.Core.Caching;

public interface IImageCacheDownloadOperation
{
    ValueTask DownloadFileAsync(Uri uri, ValueFile baseFile);
}