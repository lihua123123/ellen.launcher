// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Miyolive;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class MiyoliveClientOversea : IMiyoliveClient
{
    [GeneratedConstructor]
    public partial MiyoliveClientOversea(IServiceProvider serviceProvider, HttpClient httpClient);

    public ValueTask<Response<CodeListWrapper>> RefreshCodeAsync(string actId, CancellationToken token = default)
    {
        return ValueTask.FromException<Response<CodeListWrapper>>(EllenException.NotSupported());
    }
}