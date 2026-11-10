// Ellen.Launcher
// 多源读取 latest.json：GitCode / COS / GitHub 三处放同一份清单，谁先回谁生效。
// 目的不是负载均衡，而是——任一平台被墙、限流、或临时抽风时，更新检查仍能给出结论。

using System.Net.Http;

namespace Ellen.Launcher.Service.Update;

/// <summary>一次清单拉取的结果。失败时 <see cref="Manifest"/> 为 null，<see cref="Error"/> 说明每个源各错在哪。</summary>
public sealed record UpdateManifestFetchResult(UpdateManifest? Manifest, string? Source, string? Error);

[Service(ServiceLifetime.Singleton)]
public sealed partial class UpdateManifestClient
{
    /// <summary>
    /// 按国内可达性排序：GitCode 实测直连 0.2~0.4s，COS 同区域更快但依赖已上传，
    /// GitHub raw 作为最后兜底（实测 0.5~0.6s，个别网络下不可达）。
    /// 清单内容三处必须一致，改一处就要同步其余两处。
    /// </summary>
    public static readonly IReadOnlyList<string> Sources =
    [
        "https://raw.gitcode.com/EllenMeansLove/ellen.launcher/raw/master/latest.json",
        "https://ellen-launcher-1327452638.cos.ap-guangzhou.myqcloud.com/latest.json",
        "https://raw.githubusercontent.com/lihua123123/ellen.launcher/main/latest.json",
    ];

    private static readonly TimeSpan PerSourceTimeout = TimeSpan.FromSeconds(6);

    private readonly IHttpClientFactory httpClientFactory;

    [GeneratedConstructor]
    public partial UpdateManifestClient(IServiceProvider serviceProvider);

    public async ValueTask<UpdateManifestFetchResult> FetchAsync(CancellationToken token)
    {
        List<string> failures = [];

        foreach (string source in Sources)
        {
            try
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(PerSourceTimeout);

                using HttpClient client = httpClientFactory.CreateClient(nameof(UpdateManifestClient));
                using HttpResponseMessage response = await client.GetAsync(source, timeout.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add($"{Describe(source)} HTTP {(int)response.StatusCode}");
                    continue;
                }

                string json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                UpdateManifest? manifest = JsonSerializer.Deserialize<UpdateManifest>(json);

                if (manifest?.ParsedVersion is null)
                {
                    failures.Add($"{Describe(source)} 清单缺少合法 version");
                    continue;
                }

                return new(manifest, source, null);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                failures.Add($"{Describe(source)} 超时");
            }
            catch (Exception ex)
            {
                failures.Add($"{Describe(source)} {ex.GetType().Name}");
            }
        }

        return new(null, null, string.Join("；", failures));
    }

    private static string Describe(string source)
    {
        return Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) ? uri.Host : source;
    }
}
