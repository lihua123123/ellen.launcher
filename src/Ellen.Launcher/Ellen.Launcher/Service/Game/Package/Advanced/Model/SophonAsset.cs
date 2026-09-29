// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Takumi.Downloader.Proto;

namespace Ellen.Launcher.Service.Game.Package.Advanced.Model;

public sealed class SophonAsset
{
    public SophonAsset(string urlPrefix, string urlSuffix, AssetProperty assetProperty)
    {
        UrlPrefix = string.Intern(urlPrefix);
        UrlSuffix = string.Intern(urlSuffix);
        AssetProperty = assetProperty;
    }

    public string UrlPrefix { get; }

    public string UrlSuffix { get; }

    public AssetProperty AssetProperty { get; }
}