// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Web.Hoyolab.Bbs.Home;

[Service(ServiceLifetime.Transient, typeof(IOverseaSupportFactory<IHomeClient>))]
public sealed partial class HomeClientFactory : OverseaSupportFactory<IHomeClient, HomeClient, HomeClientOversea>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial HomeClientFactory(IServiceProvider serviceProvider);
}