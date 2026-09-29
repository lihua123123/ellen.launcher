// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Game.Package;
using Ellen.Launcher.Service.Game.Scheme;

namespace Ellen.Launcher.ViewModel.Game;

public interface IViewModelSupportLaunchExecution
{
    LaunchScheme? TargetScheme { get; }

    LaunchScheme? CurrentScheme { get; }

    GameAccount? GameAccount { get; }

    ValueTask<BlockDeferral<PackageConvertStatus>> CreateConvertBlockDeferralAsync();
}