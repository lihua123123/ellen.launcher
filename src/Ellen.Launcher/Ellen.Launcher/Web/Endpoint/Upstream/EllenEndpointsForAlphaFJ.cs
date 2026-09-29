// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

// 福建
[Service(ServiceLifetime.Singleton, typeof(IEllenEndpoints), Key = EllenEndpointsKind.AlphaCN)]
public sealed class EllenEndpointsForAlphaFJ : IEllenEndpoints
{
    string IInfrastructureRootAccess.Root { get => "https://alpha.snapgenshin.cn/fj"; }

    string IInfrastructureRawRootAccess.RawRoot { get => "https://alpha.snapgenshin.cn"; }

    public string PatchSnapHutao()
    {
        return $"{((IInfrastructureRootAccess)this).Root}/patch/alpha";
    }
}