// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Service.EnemyArchive;
using Ellen.Launcher.ViewModel.Guide;
using Ellen.Launcher.Web.Endpoint.Upstream;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mime;

namespace Ellen.Launcher.Core.Caching;

[Service(ServiceLifetime.Singleton, typeof(IImageCacheDownloadOperation))]
[HttpClient(HttpClientConfiguration.Default)]
[PrimaryHttpMessageHandler(MaxConnectionsPerServer = 8)]
public sealed partial class ImageCacheDownloadOperation : IImageCacheDownloadOperation
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IHttpClientFactory httpClientFactory;

    [GeneratedConstructor]
    public partial ImageCacheDownloadOperation(IServiceProvider serviceProvider);

    public async ValueTask DownloadFileAsync(Uri uri, ValueFile baseFile)
    {
        /* 敌人图鉴图源的下载单独计数：CachedImage 的失败路径只降级占位图，
           旧实现既不进 metrics 也不进 Sentry（Sentry 默认未启用）—— 见 WikiImageDownloadCounter。 */
        bool isWikiImage = WikiImageDownloadCounter.IsDownloadableWikiImage(uri);
        if (isWikiImage)
        {
            WikiImageDownloadCounter.ReportAttempt();
        }

        try
        {
            /* 图标有两条等价来源：自建数据站 ellen.wiki 与 Snap CDN 双域名。
               线上自建站的 /api/launcher/assets 路由可能没部署（2026-11-10 祈愿记录头像
               整片空白就是这么来的），Snap 又缺一部分新角色资源；任一端 404 / 返回 JSON /
               连接超时都不能直接判失败，按顺序换下一个源重试。
               缓存键仍用原始 URL（ImageCacheFile.Create 按 uri.OriginalString 取 SHA1），
               因此换源不会让之前已缓存的图标失效。 */
            Exception? lastError = null;
            bool downloaded = false;

            foreach (Uri candidate in LauncherAssetMirror.BuildDownloadCandidates(uri))
            {
                using (HttpClient httpClient = httpClientFactory.CreateClient(nameof(ImageCacheDownloadOperation)))
                {
                    try
                    {
                        await DownloadFileUsingHttpClientAsync(httpClient, candidate, baseFile).ConfigureAwait(false);
                        downloaded = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                }

                // 失败时可能只写了一半，下一轮必须从头写，否则会被误判成成功
                TryDeletePartialFile(baseFile);
            }

            if (!downloaded || !File.Exists(baseFile))
            {
                throw lastError ?? publicImageCacheException.Throw($"'{uri.OriginalString}': File not exists after download attempt.");
            }

            if (isWikiImage)
            {
                WikiImageDownloadCounter.ReportSuccess();
            }
        }
        catch
        {
            if (isWikiImage)
            {
                WikiImageDownloadCounter.ReportFailure();
            }

            throw;
        }
    }

    private static void TryDeletePartialFile(ValueFile file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch (IOException)
        {
            // 忽略：下一轮 File.Create 会覆盖
        }
    }

    private async ValueTask DownloadFileUsingHttpClientAsync(HttpClient httpClient, Uri uri, ValueFile baseFile)
    {
        HttpRequestMessageBuilder requestMessageBuilder = httpRequestMessageBuilderFactory
            .Create()
            .SetRequestUri(uri)
            .SetStaticResourceControlHeadersIfRequired()
            .Get();

        using (HttpRequestMessage requestMessage = requestMessageBuilder.HttpRequestMessage)
        {
            using (HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                if (!responseMessage.IsSuccessStatusCode)
                {
                    throw responseMessage.StatusCode switch
                    {
                        HttpStatusCode.NotFound => publicImageCacheException.Throw($"Unable to download file from '{uri.OriginalString}': 404 Not Found"),
                        _ => publicImageCacheException.Throw($"Unexpected HTTP status code {responseMessage.StatusCode}"),
                    };
                }

                if (responseMessage.Content.Headers.ContentType?.MediaType is MediaTypeNames.Application.Json)
                {
                    publicImageCacheException.Throw($"Unexpected content type: {MediaTypeNames.Application.Json}");
                }

                using (Stream httpStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    string? directoryName = Path.GetDirectoryName(baseFile);
                    ArgumentException.ThrowIfNullOrEmpty(directoryName);

                    try
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    catch (DirectoryNotFoundException dnfEx)
                    {
                        throw publicImageCacheException.Throw($"Unable to create folder at '{directoryName}'", dnfEx);
                    }

                    FileStream fileStream;
                    try
                    {
                        fileStream = File.Create(baseFile);
                    }
                    catch (IOException ex)
                    {
                        // The process cannot access the file '?' because it is being used by another process.
                        throw publicImageCacheException.Throw($"Unable to create file at '{baseFile}'", ex);
                    }

                    try
                    {
                        using (fileStream)
                        {
                            await httpStream.CopyToAsync(fileStream).ConfigureAwait(false);
                        }
                    }
                    catch (IOException ex)
                    {
                        // Received an unexpected EOF or 0 bytes from the transport stream.
                        // Unable to read data from the transport connection: 远程主机强迫关闭了一个现有的连接。. SocketException: ConnectionReset
                        // Unable to read data from the transport connection: 你的主机中的软件中止了一个已建立的连接。. SocketException: ConnectionAborted
                        // HttpIOException: The response ended prematurely. (ResponseEnded)
                        // 磁盘空间不足。 : '?'.
                        throw publicImageCacheException.Throw("Unable to copy stream content to file", ex);
                    }
                }
            }
        }
    }
}