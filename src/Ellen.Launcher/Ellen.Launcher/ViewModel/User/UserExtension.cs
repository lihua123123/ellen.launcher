// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity.Extension;

namespace Ellen.Launcher.ViewModel.User;

public static class UserExtension
{
    extension(User user)
    {
        public bool TryUpdateFingerprint(string? deviceFp)
        {
            return user.Entity.TryUpdateFingerprint(deviceFp);
        }
    }
}