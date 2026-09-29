// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web;
using System.Net;
using System.Net.Http;

namespace Ellen.Launcher.Core.IO.Http;

/// <summary>
/// Global redirect handler.
/// Since SocketsHttpHandler.AllowAutoRedirect=false,
/// this handler manually follows all 3xx responses, rewriting domains as needed.
/// Reuses the original HttpRequestMessage to preserve HttpCompletionOption and other properties.
/// </summary>
[Service(ServiceLifetime.Transient)]
public sealed partial class ServerDomainRedirectHandler : DelegatingHandler
{
    private const int MaxRedirects = 5;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        int redirectCount = 0;
        while (IsRedirectStatusCode(response.StatusCode) && redirectCount < MaxRedirects)
        {
            Uri? location = response.Headers.Location;
            if (location is null || !location.IsAbsoluteUri)
            {
                return response;
            }

            // Rewrite domain if needed (backup ↔ primary)
            string rewritten = RewriteDomainIfNeeded(location.OriginalString);
            Uri target = rewritten != location.OriginalString ? new Uri(rewritten, UriKind.Absolute) : location;

            response.Dispose();

            // 301/302/303 → GET + clear body
            // 307/308 → preserve method + body
            request.RequestUri = target;
            if (response.StatusCode is not HttpStatusCode.RedirectKeepVerb and not HttpStatusCode.PermanentRedirect)
            {
                request.Method = HttpMethod.Get;
                request.Content = null;
            }

            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            redirectCount++;
        }

        return response;
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode is >= 300 and < 400;
    }

    private static string RewriteDomainIfNeeded(string url)
    {
        if (ServerDomain.IsBackupMode())
        {
            // Backup mode: rewrite primary domain → backup domain in redirects
            return url.Replace("snaphutaorp.org", "hutaorp.org");
        }

        return url;
    }
}
