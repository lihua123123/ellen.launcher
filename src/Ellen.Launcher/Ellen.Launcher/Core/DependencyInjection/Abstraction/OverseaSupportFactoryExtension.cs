// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.User;

namespace Ellen.Launcher.Core.DependencyInjection.Abstraction;

public static class OverseaSupportFactoryExtension
{
    extension<TClient>(IOverseaSupportFactory<TClient> factory)
    {
        public TClient CreateFor(UserAndUid userAndUid)
        {
            return factory.Create(userAndUid.IsOversea);
        }
    }
}