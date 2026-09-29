// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Bbs.Home;

[HttpClient(HttpClientConfiguration.XRpc3)]
public sealed partial class HomeClientOversea : IHomeClient
{
    [GeneratedConstructor]
    public partial HomeClientOversea(IServiceProvider serviceProvider, HttpClient httpClient);

    public ValueTask<Response<NewHomeNewInfo>> GetNewHomeInfoAsync(int gid, CancellationToken token = default)
    {
        return ValueTask.FromException<Response<NewHomeNewInfo>>(EllenException.NotSupported());
    }
}