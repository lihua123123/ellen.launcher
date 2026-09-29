// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Takumi.Downloader.Proto;

namespace Ellen.Launcher.Service.Game.Package.Advanced.Model;

public sealed class SophonDecodedPatchManifest
{
    public SophonDecodedPatchManifest(string originalTag, string tag, string urlPrefix, string urlSuffix, PatchManifest data)
    {
        OriginalTag = originalTag;
        Tag = tag;
        UrlPrefix = urlPrefix;
        UrlSuffix = urlSuffix;
        Data = data;
    }

    public string OriginalTag { get; }

    public string Tag { get; }

    public string UrlPrefix { get; }

    public string UrlSuffix { get; }

    public PatchManifest Data { get; }
}