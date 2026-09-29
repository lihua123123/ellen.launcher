// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;

[Service(ServiceLifetime.Transient, typeof(IOverseaSupportFactory<ISignInClient>))]
public sealed partial class SignInClientFactory : OverseaSupportFactory<ISignInClient, SignInClient, SignInClientOversea>
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial SignInClientFactory(IServiceProvider serviceProvider);
}