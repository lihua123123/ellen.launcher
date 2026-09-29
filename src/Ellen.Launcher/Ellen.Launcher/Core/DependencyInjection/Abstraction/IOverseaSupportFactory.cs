// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.DependencyInjection.Abstraction;

public interface IOverseaSupportFactory<out TClient>
{
    TClient Create(bool isOversea);
}