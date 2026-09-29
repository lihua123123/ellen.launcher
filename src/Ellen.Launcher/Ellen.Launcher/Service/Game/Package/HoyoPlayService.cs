// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using JetBrains.Annotations;
using Ellen.Launcher.Service.Game.Scheme;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.DeprecatedFile;
using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Service.Game.Package;

[Service(ServiceLifetime.Singleton, typeof(IHoyoPlayService))]
public sealed partial class HoyoPlayService : IHoyoPlayService
{
    private readonly IServiceProvider serviceProvider;

    [GeneratedConstructor]
    public partial HoyoPlayService(IServiceProvider serviceProvider);

    public ValueTask<ValueResult<bool, GameBranchesWrapper>> TryGetBranchesAsync(LaunchScheme scheme)
    {
        return TryGetAsync(scheme, static (client, scheme) => client.GetBranchesAsync(scheme));
    }

    public ValueTask<ValueResult<bool, GameChannelSDKsWrapper>> TryGetChannelSDKsAsync(LaunchScheme scheme)
    {
        return TryGetAsync(scheme, static (client, scheme) => client.GetChannelSDKAsync(scheme));
    }

    public ValueTask<ValueResult<bool, DeprecatedFileConfigurationsWrapper>> TryGetDeprecatedFileConfigurationsAsync(LaunchScheme scheme)
    {
        return TryGetAsync(scheme, static (client, scheme) => client.GetDeprecatedFileConfigurationsAsync(scheme));
    }

    private async ValueTask<ValueResult<bool, T>> TryGetAsync<T>(LaunchScheme scheme, [RequireStaticDelegate] Func<HoyoPlayClient, LaunchScheme, ValueTask<Response<T>>> asyncMethod)
    {
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            HoyoPlayClient hoyoPlayClient = scope.ServiceProvider.GetRequiredService<HoyoPlayClient>();

            Response<T> response = await asyncMethod(hoyoPlayClient, scheme).ConfigureAwait(false);
            if (!ResponseValidator.TryValidate(response, serviceProvider, out T? data))
            {
                return new(false, default!);
            }

            return new(true, data);
        }
    }
}