// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Service.Game.Package.Advanced.AssetOperation;

[Service(ServiceLifetime.Transient, typeof(IDriverMediaTypeAwareFactory<IGameAssetOperation>))]
public sealed partial class GameAssetOperationFactory : DriverMediaTypeAwareFactory<IGameAssetOperation, GameAssetOperationSSD, GameAssetOperationHDD>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial GameAssetOperationFactory(IServiceProvider serviceProvider);
}