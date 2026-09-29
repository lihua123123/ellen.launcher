// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Web.Hoyolab.Downloader;

[Service(ServiceLifetime.Transient, typeof(IOverseaSupportFactory<ISophonClient>))]
public sealed partial class SophonClientFactory : OverseaSupportFactory<ISophonClient, SophonClient, SophonClientOversea>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial SophonClientFactory(IServiceProvider serviceProvider);
}