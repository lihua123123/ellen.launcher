// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using BindingUser = Ellen.Launcher.ViewModel.User.User;

namespace Ellen.Launcher.Service.User;

public sealed class UserRemovedMessage
{
    public UserRemovedMessage(BindingUser removedUser)
    {
        RemovedUser = removedUser;
    }

    public BindingUser RemovedUser { get; }
}