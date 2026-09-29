// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.DeprecatedFile;
using System.Net.Http;

namespace Ellen.Launcher.Service.Game.Package;

public readonly struct PackageConverterDeprecationContext
{
    public readonly HttpClient HttpClient;
    public readonly IGameFileSystemView GameFileSystem;
    public readonly GameChannelSDK? GameChannelSdk;
    public readonly DeprecatedFilesWrapper? DeprecatedFiles;

    public PackageConverterDeprecationContext(HttpClient httpClient, IGameFileSystemView gameFileSystem, GameChannelSDK? gameChannelSdk, DeprecatedFilesWrapper? deprecatedFiles)
    {
        HttpClient = httpClient;
        GameFileSystem = gameFileSystem;
        GameChannelSdk = gameChannelSdk;
        DeprecatedFiles = deprecatedFiles;
    }
}