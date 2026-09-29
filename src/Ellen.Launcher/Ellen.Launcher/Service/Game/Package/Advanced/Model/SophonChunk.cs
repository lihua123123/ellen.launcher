// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Takumi.Downloader.Proto;

namespace Ellen.Launcher.Service.Game.Package.Advanced.Model;

public sealed class SophonChunk
{
    public SophonChunk(string urlPrefix, string urlSuffix, AssetChunk assetChunk)
    {
        UrlPrefix = string.Intern(urlPrefix);
        UrlSuffix = string.Intern(urlSuffix);
        AssetChunk = assetChunk;
    }

    public string UrlPrefix { get; }

    public string UrlSuffix { get; }

    public AssetChunk AssetChunk { get; }

    public string ChunkDownloadUrl { get => $"{UrlPrefix}/{AssetChunk.ChunkName}{UrlSuffix}"; }
}