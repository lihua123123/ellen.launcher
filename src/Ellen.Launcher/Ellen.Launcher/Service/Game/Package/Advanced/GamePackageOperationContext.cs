// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.Package.Advanced.AssetOperation;
using Ellen.Launcher.Service.Game.Package.Advanced.Model;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;
using System.IO;

namespace Ellen.Launcher.Service.Game.Package.Advanced;

public sealed class GamePackageOperationContext
{
    public GamePackageOperationContext(IServiceProvider serviceProvider, GamePackageOperationKind kind, IGameFileSystem gameFileSystem, string? extractDirectory = default)
    {
        Kind = kind;
        GameFileSystem = gameFileSystem;

        EffectiveGameDirectory = extractDirectory ?? gameFileSystem.GameDirectory;
        Asset = serviceProvider.GetRequiredService<IDriverMediaTypeAwareFactory<IGameAssetOperation>>().Create(EffectiveGameDirectory);

        EffectiveChunksDirectory = kind is GamePackageOperationKind.Verify
            ? Path.Combine(gameFileSystem.ChunksDirectory, "repair")
            : gameFileSystem.ChunksDirectory;
    }

    public GamePackageOperationKind Kind { get; }

    public IGameAssetOperation Asset { get; }

    public IGameFileSystem GameFileSystem { get; init; }

    public SophonDecodedBuild? LocalBuild { get; init; }

    public SophonDecodedBuild? RemoteBuild { get; init; }

    public SophonDecodedPatchBuild? PatchBuild { get; init; }

    public GameChannelSDK? GameChannelSDK { get; init; }

    public string EffectiveGameDirectory { get; }

    public string EffectiveChunksDirectory { get; }
}