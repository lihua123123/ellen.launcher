// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Web.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using System.Net.Http;
using WebDailyNote = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.DailyNote.DailyNote;

namespace Ellen.Launcher.Service.DailyNote;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class DailyNoteWebhookOperation
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly DailyNoteOptions dailyNoteOptions;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial DailyNoteWebhookOperation(IServiceProvider serviceProvider, HttpClient httpClient);

    public void TryPostDailyNoteToWebhook(PlayerUid playerUid, WebDailyNote dailyNote)
    {
        string? targetUrl = dailyNoteOptions.WebhookUrl.Value;
        if (string.IsNullOrEmpty(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out Uri? targetUri))
        {
            return;
        }

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(targetUri)
            .SetHeader("x-uid", $"{playerUid}")
            .PostJson(dailyNote);

        builder.Send(httpClient);
    }
}