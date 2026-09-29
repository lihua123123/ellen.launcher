// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.Package.Advanced.Model;
using Ellen.Launcher.Web.Hoyolab.Downloader;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Branch;

namespace Ellen.Launcher.Service.Game.Package.Advanced;

public interface IGamePackageService
{
    ValueTask<bool> ExecuteOperationAsync(GamePackageOperationContext context);

    ValueTask CancelOperationAsync();

    ValueTask ContinueOperationAsync();

    ValueTask<SophonDecodedBuild?> DecodeManifestsAsync(IGameFileSystemView gameFileSystem, BranchWrapper? branch, CancellationToken token = default);

    ValueTask<SophonDecodedBuild?> DecodeManifestsAsync(IGameFileSystemView gameFileSystem, SophonBuild? build, CancellationToken token = default);

    ValueTask<SophonDecodedPatchBuild?> DecodeDiffManifestsAsync(IGameFileSystemView gameFileSystem, BranchWrapper? branch, CancellationToken token = default);

    ValueTask<SophonDecodedPatchBuild?> DecodeDiffManifestsAsync(IGameFileSystemView gameFileSystem, SophonPatchBuild? patchBuild, CancellationToken token = default);
}