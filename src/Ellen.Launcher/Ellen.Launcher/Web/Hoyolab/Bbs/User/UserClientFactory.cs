// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Web.Hoyolab.Bbs.User;

[Service(ServiceLifetime.Transient, typeof(IOverseaSupportFactory<IUserClient>))]

public sealed partial class UserClientFactory : OverseaSupportFactory<IUserClient, UserClient, UserClientOversea>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial UserClientFactory(IServiceProvider serviceProvider);
}