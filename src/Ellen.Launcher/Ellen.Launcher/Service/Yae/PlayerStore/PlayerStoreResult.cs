// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Google.Protobuf;
using Ellen.Launcher.Core.LifeCycle.InterProcess.Yae;

namespace Ellen.Launcher.Service.Yae.PlayerStore;

public sealed class PlayerStoreResult
{
    public ByteString? StoreBytes { get; init; }

    public Dictionary<InterestedPropType, double> PropMap { get; init; } = [];
}
