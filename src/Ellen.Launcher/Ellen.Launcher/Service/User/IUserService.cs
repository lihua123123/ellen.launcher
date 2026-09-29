// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using BindingUser = Ellen.Launcher.ViewModel.User.User;
using EntityUser = Ellen.Launcher.Model.Entity.User;

namespace Ellen.Launcher.Service.User;

public interface IUserService
{
    ITaskContext TaskContext { get; }

    ValueTask<AdvancedDbCollectionView<BindingUser, EntityUser>> GetUsersAsync();

    ValueTask<ValueResult<UserOptionResultKind, string?>> ProcessInputCookieAsync(InputCookie inputCookie);

    ValueTask<bool> RefreshCookieTokenAsync(EntityUser user);

    ValueTask RemoveUserAsync(BindingUser user);

    ValueTask RefreshProfilePictureAsync(UserGameRole userGameRole);

    ValueTask<bool> RetryResumeUninitializedUsersAsync(CancellationToken token = default);
}