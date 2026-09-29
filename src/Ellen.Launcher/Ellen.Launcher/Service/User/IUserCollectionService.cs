// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using BindingUser = Ellen.Launcher.ViewModel.User.User;
using EntityUser = Ellen.Launcher.Model.Entity.User;

namespace Ellen.Launcher.Service.User;

public interface IUserCollectionService
{
    ValueTask<AdvancedDbCollectionView<BindingUser, EntityUser>> GetUsersAsync();

    ValueTask RemoveUserAsync(BindingUser user);

    ValueTask<ValueResult<UserOptionResultKind, string?>> TryCreateAndAddUserFromInputCookieAsync(InputCookie inputCookie);

    ValueTask<bool> RetryResumeUninitializedUsersAsync(CancellationToken token = default);
}