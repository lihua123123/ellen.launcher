// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Web.Hoyolab.Passport;

[Service(ServiceLifetime.Transient, typeof(IOverseaSupportFactory<IHoyoPlayPassportClient>))]
public sealed partial class HoyoPlayPassportClientFactory : OverseaSupportFactory<IHoyoPlayPassportClient, HoyoPlayPassportClient, HoyoPlayPassportClientOversea>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial HoyoPlayPassportClientFactory(IServiceProvider serviceProvider);
}