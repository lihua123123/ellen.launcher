// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

// Comment or Uncomment to switch endpoint behavior
#define DEV_FORCE_USE_RELEASE_ENDPOINT

namespace Ellen.Launcher.Web.Endpoint.Upstream;

[Service(ServiceLifetime.Singleton, typeof(IEllenEndpointsFactory))]
public sealed partial class EllenEndpointsFactory : IEllenEndpointsFactory
{
    private readonly IServiceProvider serviceProvider;

    [GeneratedConstructor]
    public partial EllenEndpointsFactory(IServiceProvider serviceProvider);

    public IEllenEndpoints Create()
    {
#if RELEASE && (!IS_ALPHA_BUILD || !IS_CARARY_BUILD)
        return serviceProvider.GetRequiredKeyedService<IEllenEndpoints>(EllenEndpointsKind.Release);
#elif DEBUG && DEV_FORCE_USE_RELEASE_ENDPOINT
        return serviceProvider.GetRequiredKeyedService<IEllenEndpoints>(EllenEndpointsKind.Release);
#else
        return Core.Setting.LocalSetting.Get(Core.Setting.SettingKeys.AlphaBuildUseCnPatchEndpoint, false)
            ? Core.Setting.LocalSetting.Get(Core.Setting.SettingKeys.AlphaBuildUseFjPatchEndpoint, false)
                ? serviceProvider.GetRequiredKeyedService<IEllenEndpoints>(EllenEndpointsKind.AlphaFJ)
                : serviceProvider.GetRequiredKeyedService<IEllenEndpoints>(EllenEndpointsKind.AlphaCN)
            : serviceProvider.GetRequiredKeyedService<IEllenEndpoints>(EllenEndpointsKind.AlphaOS);
#endif
    }
}