// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Web.Hoyolab.Passport;

[Service(ServiceLifetime.Transient, typeof(IOverseaSupportFactory<IPassportClient>))]
public sealed partial class PassportClientFactory : OverseaSupportFactory<IPassportClient, PassportClient, PassportClientOversea>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial PassportClientFactory(IServiceProvider serviceProvider);
}