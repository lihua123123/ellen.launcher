// P3 HTTP 客户端：合规 UA/Referer、限速、指数退避、ETag 条件请求（304）、失败显式诊断。
// 只请求公开 API 的 JSON 与图片，不抓网页 HTML。

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class WikiHttpResponse
{
    public bool Success { get; init; }
    public bool NotModified { get; init; }
    public byte[]? Bytes { get; init; }
    public string? ETag { get; init; }
    public int StatusCode { get; init; }
    public string? Error { get; init; }
    public int Attempts { get; init; }
}

public sealed class WikiHttpClient : IDisposable
{
    private readonly HttpWikiSourceOptions options;
    private readonly HttpClient client;
    private readonly WikiDiagnosticsLog log;
    private readonly WikiSourceStamp stamp;
    private readonly bool ownsClient;
    private long lastRequestTicks;
    private int hits;
    private int misses;
    private int notModified;
    private int downloads;
    private int failures;
    private int bytes;

    public WikiHttpClient(HttpWikiSourceOptions options, WikiDiagnosticsLog? log = null, WikiSourceStamp? stamp = null)
    {
        this.options = options;
        this.log = log ?? new WikiDiagnosticsLog();
        this.stamp = stamp ?? WikiSourceStamp.FromEllenWiki("C1/C2 HTTP", null, null);
        HttpClientHandler? handler = options.Handler as HttpClientHandler;
        client = new HttpClient(options.Handler ?? handler ?? new HttpClientHandler(), disposeHandler: options.Handler is null)
        {
            Timeout = options.Timeout,
        };
        ownsClient = true;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        client.DefaultRequestHeaders.Referrer = new Uri(options.Referer);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public WikiCacheStats Stats => new()
    {
        Hits = hits,
        Misses = misses,
        NotModified = notModified,
        Downloads = downloads,
        Failures = failures,
        BytesDownloaded = bytes,
    };

    /// <summary>条件 GET：带 If-None-Match，304 直接复用缓存；失败按指数退避重试。</summary>
    public async Task<WikiHttpResponse> GetAsync(string path, string? etag = null, CancellationToken token = default)
    {
        Uri uri = options.Build(path);
        int attempts = 0;
        Exception? last = null;

        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            attempts = attempt + 1;
            await ThrottleAsync(token).ConfigureAwait(false);
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, uri);
                if (!string.IsNullOrEmpty(etag))
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                }

                using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    Interlocked.Increment(ref notModified);
                    return new WikiHttpResponse { Success = true, NotModified = true, StatusCode = 304, ETag = etag, Attempts = attempts };
                }

                if (!response.IsSuccessStatusCode)
                {
                    last = new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    if (!IsRetryable(response.StatusCode))
                    {
                        break;
                    }
                }
                else
                {
                    byte[] data = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                    Interlocked.Increment(ref downloads);
                    Interlocked.Add(ref bytes, data.Length);
                    return new WikiHttpResponse
                    {
                        Success = true,
                        Bytes = data,
                        ETag = response.Headers.ETag?.Tag,
                        StatusCode = (int)response.StatusCode,
                        Attempts = attempts,
                    };
                }
            }
            catch (HttpRequestException ex) { last = ex; }
            catch (TaskCanceledException ex) when (!token.IsCancellationRequested) { last = ex; }
            catch (OperationCanceledException) { throw; }

            if (attempt < options.MaxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt)), token).ConfigureAwait(false);
            }
        }

        Interlocked.Increment(ref failures);
        string error = last?.Message ?? "未知 HTTP 错误";
        log.Add("http-failure", stamp, $"{uri}: {error}（重试 {attempts} 次）", $"HTTP 请求失败：{error}");
        return new WikiHttpResponse { Success = false, Error = error, Attempts = attempts };
    }

    private static bool IsRetryable(HttpStatusCode code) =>
        code is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private async Task ThrottleAsync(CancellationToken token)
    {
        long previous = Interlocked.Read(ref lastRequestTicks);
        long now = DateTime.UtcNow.Ticks;
        long minTicks = options.MinRequestInterval.Ticks;
        long waitTicks = minTicks - (now - previous);
        if (waitTicks > 0)
        {
            await Task.Delay(TimeSpan.FromTicks(waitTicks), token).ConfigureAwait(false);
        }

        Interlocked.Exchange(ref lastRequestTicks, DateTime.UtcNow.Ticks);
    }

    public void RecordHit() => Interlocked.Increment(ref hits);

    public void RecordMiss() => Interlocked.Increment(ref misses);

    public void Dispose()
    {
        if (ownsClient)
        {
            client.Dispose();
        }
    }
}
