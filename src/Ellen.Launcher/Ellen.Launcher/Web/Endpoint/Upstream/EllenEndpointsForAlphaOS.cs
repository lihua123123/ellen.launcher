// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

[Service(ServiceLifetime.Singleton, typeof(IEllenEndpoints), Key = EllenEndpointsKind.AlphaOS)]
public sealed class EllenEndpointsForAlphaOS : IEllenEndpoints
{
    string IInfrastructureRootAccess.Root { get => "https://alpha.snapgenshin.cn/global"; }

    string IInfrastructureRawRootAccess.RawRoot { get => "https://alpha.snapgenshin.cn"; }

    public string PatchSnapHutao()
    {
        return $"{((IInfrastructureRootAccess)this).Root}/patch/alpha";
    }
}