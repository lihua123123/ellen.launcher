// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;

[Service(ServiceLifetime.Transient, typeof(IOverseaSupportFactory<IGameRecordClient>))]
public sealed partial class GameRecordClientFactory : OverseaSupportFactory<IGameRecordClient, GameRecordClient, GameRecordClientOversea>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial GameRecordClientFactory(IServiceProvider serviceProvider);
}