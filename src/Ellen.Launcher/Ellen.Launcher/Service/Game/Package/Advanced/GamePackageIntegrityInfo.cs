// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Game.Package.Advanced.Model;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Game.Package.Advanced;

public sealed class GamePackageIntegrityInfo
{
    public required ImmutableArray<SophonAssetOperation> ConflictedAssets { get; init; }

    public required bool ChannelSdkConflicted { get; init; }

    public bool NoConflict { get => ConflictedAssets is [] && !ChannelSdkConflicted; }

    public (int ChunkCount, long ByteCount) GetConflictedBlockCountAndByteCount(GameChannelSDK? sdk)
    {
        int conflictChunks = ConflictedAssets.Sum(a => a.NewAsset.AssetChunks.Count);
        long conflictBytes = ConflictedAssets.Sum(a => a.NewAsset.AssetSize);

        if (ChannelSdkConflicted)
        {
            ArgumentNullException.ThrowIfNull(sdk);
            conflictChunks++;
            conflictBytes += sdk.ChannelSdkPackage.Size;
        }

        return (conflictChunks, conflictBytes);
    }
}