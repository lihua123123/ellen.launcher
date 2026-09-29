// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Game.Scheme;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.DeprecatedFile;

namespace Ellen.Launcher.Service.Game.Package;

public interface IHoyoPlayService
{
    ValueTask<ValueResult<bool, GameBranchesWrapper>> TryGetBranchesAsync(LaunchScheme scheme);

    ValueTask<ValueResult<bool, GameChannelSDKsWrapper>> TryGetChannelSDKsAsync(LaunchScheme scheme);

    ValueTask<ValueResult<bool, DeprecatedFileConfigurationsWrapper>> TryGetDeprecatedFileConfigurationsAsync(LaunchScheme scheme);
}