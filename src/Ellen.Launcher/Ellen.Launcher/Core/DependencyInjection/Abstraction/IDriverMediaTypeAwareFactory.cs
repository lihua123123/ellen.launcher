// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.DependencyInjection.Abstraction;

public interface IDriverMediaTypeAwareFactory<out TService>
{
    TService Create(string path);
}