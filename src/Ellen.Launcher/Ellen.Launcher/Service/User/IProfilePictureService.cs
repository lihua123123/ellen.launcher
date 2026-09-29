// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;

namespace Ellen.Launcher.Service.User;

public interface IProfilePictureService
{
    ValueTask TryInitializeAsync(ViewModel.User.User user, CancellationToken token = default);

    ValueTask RefreshUserGameRoleAsync(UserGameRole userGameRole, CancellationToken token = default);
}