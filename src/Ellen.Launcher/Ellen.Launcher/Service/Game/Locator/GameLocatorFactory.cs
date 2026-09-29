// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game.Locator;

[Service(ServiceLifetime.Singleton, typeof(IGameLocatorFactory))]
public sealed partial class GameLocatorFactory : IGameLocatorFactory
{
    private readonly IServiceProvider serviceProvider;

    [GeneratedConstructor]
    public partial GameLocatorFactory(IServiceProvider serviceProvider);

    public IGameLocator Create(GameLocationSourceKind source)
    {
        return serviceProvider.GetRequiredKeyedService<IGameLocator>(source);
    }
}