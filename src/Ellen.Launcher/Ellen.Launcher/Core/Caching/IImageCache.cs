// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Ellen.Launcher.Core.IO;

namespace Ellen.Launcher.Core.Caching;

public interface IImageCache
{
    ValueFile GetFileFromCategoryAndName(string category, string fileName);

    ValueTask<ValueFile> GetFileFromCacheAsync(Uri uri);

    ValueTask<ValueFile> GetFileFromCacheAsync(Uri uri, ElementTheme theme);

    void Remove(Uri uriForCachedItem);
}