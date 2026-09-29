// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Miyolive;

[Service(ServiceLifetime.Transient, typeof(IOverseaSupportFactory<IMiyoliveClient>))]
public sealed partial class MiyoliveClientFactory : OverseaSupportFactory<IMiyoliveClient, MiyoliveClient, MiyoliveClientOversea>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial MiyoliveClientFactory(IServiceProvider serviceProvider);
}