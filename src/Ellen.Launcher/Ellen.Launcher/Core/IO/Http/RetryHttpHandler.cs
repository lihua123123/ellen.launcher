// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Web;
using Ellen.Launcher.Web.Request.Builder;
using System.Net.Http;
using System.Runtime.ExceptionServices;

namespace Ellen.Launcher.Core.IO.Http;

[Service(ServiceLifetime.Transient)]
public sealed partial class RetryHttpHandler : DelegatingHandler
{
    public static HttpRequestOptionsKey<bool> DisableRetry { get; } = new("DisableRetry");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(DisableRetry, out bool skipRetry) && skipRetry)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        ExceptionDispatchInfo? dispatch = default;
        int requestCount = 0;
        while (requestCount < 3)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // Retry on server error status codes (5xx) such as 502 Bad Gateway
                if ((int)response.StatusCode >= 500)
                {
                    response.EnsureSuccessStatusCode();
                }

                return response;
            }
            catch (HttpRequestException ex)
            {
                response?.Dispose();

                // Detect SSL connection error → auto switch to backup domain + rewrite request URL
                NetworkError networkError = HttpRequestExceptionHandling.HttpRequestExceptionToNetworkError(ex);
                if (networkError is NetworkError.ERR_SECURE_CONNECTION_RESET
                                 or NetworkError.ERR_SECURE_CONNECTION_ERROR
                                 or NetworkError.ERR_SECURE_CONNECTION_ABORTED)
                {
                    ServerDomain.TryAutoFallback();
                    RewriteToBackupDomain(request);
                }

                dispatch = ExceptionDispatchInfo.Capture(ex);
                request.Resurrect();
            }

            requestCount++;
        }

        dispatch?.Throw();
        throw EllenException.InvalidOperation("Unexpected request retry state");
    }

    private static void RewriteToBackupDomain(HttpRequestMessage request)
    {
        if (request.RequestUri is Uri uri)
        {
            string original = uri.OriginalString;
            string rewritten = original.Replace("snaphutaorp.org", "hutaorp.org");
            if (rewritten != original)
            {
                request.RequestUri = new Uri(rewritten);
            }
        }
    }
}
