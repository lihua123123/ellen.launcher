// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Google.Protobuf;
using Ellen.Launcher.Model.InterChange.Achievement;
using Ellen.Launcher.Model.InterChange.Inventory;
using Ellen.Launcher.Service.Yae.PlayerStore;
using Ellen.Launcher.ViewModel.Game;

namespace Ellen.Launcher.Service.Yae;

public interface IYaeService
{
    ValueTask<UIAF?> GetAchievementAsync(IViewModelSupportLaunchExecution viewModel);

    ValueTask<UIIF?> GetInventoryAsync(IViewModelSupportLaunchExecution viewModel);

    ValueTask<ByteString?> GetPlayerStoreBytesAsync(IViewModelSupportLaunchExecution viewModel);

    ValueTask<PlayerStoreResult?> GetPlayerStoreResultAsync(IViewModelSupportLaunchExecution viewModel);
}