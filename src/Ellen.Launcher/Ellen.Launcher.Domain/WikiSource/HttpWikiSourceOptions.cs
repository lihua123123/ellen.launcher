// P3 HTTP 运行时源配置：合规（UA/来源说明）、限速、退避、超时、缓存目录。
// 纯 net10.0，无 UI 依赖；HttpMessageHandler 可注入，便于单测用本地镜像演练。

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class HttpWikiSourceOptions
{
    public const string DefaultEndpoint = "https://ellen.wiki";

    /// <summary>可用 ELLEN_WIKI_ENDPOINT 覆盖（与既有 WikiMetadataSyncService 一致）。</summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    /// <summary>合规：显式 User-Agent 与来源说明，不抓网页 HTML，只用公开 API。</summary>
    public string UserAgent { get; init; } = "EllenLauncher/1.0 (+ellen.wiki public API; contact: launcher data sync)";

    public string Referer { get; init; } = "https://ellen.wiki/";

    /// <summary>单请求超时；同步失败时降级到缓存/legacy，不阻塞启动。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>限速：两次请求最小间隔，避免对 ellen.wiki 造成压力。</summary>
    public TimeSpan MinRequestInterval { get; init; } = TimeSpan.FromMilliseconds(20);

    /// <summary>失败重试次数（指数退避）。</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>
    /// 条目并发数（P5）。753 条顺序请求在真实网络下会被 RTT 放大到几分钟；
    /// 默认 4 路并发 + 20ms 起始间隔，既压得住总时长，也不对上游造成突发压力。
    /// </summary>
    public int ItemConcurrency { get; init; } = 4;

    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(150);

    /// <summary>本地缓存根目录（文件级 ETag / 内容寻址图片均落此处）。</summary>
    public string CacheRoot { get; init; } = string.Empty;

    /// <summary>
    /// 磁盘上限（字节）。超过后按 LRU 淘汰（manifest / enemy-index 受保护）。
    /// 0 表示不限制；默认 1 GiB，够放 753 items + 4655 张图。
    /// </summary>
    public long CacheMaxBytes { get; init; } = 1L * 1024 * 1024 * 1024;

    /// <summary>
    /// P5 分层配额；空则按 <see cref="CacheMaxBytes"/> 生成默认分层（infra/items/images/thumbs）。
    /// 分层后详情图不会把条目数据挤出缓存，也不会让缩略图被大图顶掉。
    /// </summary>
    public IReadOnlyList<WikiCacheTierLimit> CacheTierLimits { get; init; } = [];

    public IReadOnlyList<WikiCacheTierLimit> EffectiveTierLimits =>
        CacheTierLimits.Count > 0 ? CacheTierLimits : WikiCacheTiers.Defaults(CacheMaxBytes);

    /// <summary>自定义 handler：生产用 HttpClientHandler，测试注入本地镜像 handler。</summary>
    public HttpMessageHandler? Handler { get; init; }

    public string ManifestPath => "/api/launcher/manifest.json";
    public string EnemyIndexPath => "/api/enemies/index.json";

    /// <summary>
    /// P4：敌人图片 assets manifest。上游尚未提供，路径为提议值（见 P4 需求单 A4）；
    /// 404 时客户端显式降级为「在线 + 按需缓存」，不宣称离线。
    /// </summary>
    public string EnemyAssetsManifestPath => "/api/enemies/assets-manifest.json";

    /// <summary>
    /// 线上 C2 是**扁平**的 /api/enemies/&lt;slug&gt;.json（753 个），不是 items/ 子目录。
    /// P0 文档曾误写 /api/enemies/items/&lt;id&gt;.json（那是 src/data/enemies 的源目录结构）。
    /// 已用 WebFetch 核实：https://ellen.wiki/api/enemies/abyss-corroded-golden-wolflord.json 返回该敌人 JSON。
    /// </summary>
    public string EnemyItemPath(string id) => $"/api/enemies/{Uri.EscapeDataString(id)}.json";

    public string EnemyImagePath(string id, string file) => $"/images/enemies/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(file)}";

    public Uri Build(string path) => new(Endpoint.TrimEnd('/') + path, UriKind.Absolute);

    public static HttpWikiSourceOptions FromEnvironment(string? cacheRoot = null)
    {
        string endpoint = Environment.GetEnvironmentVariable("ELLEN_WIKI_ENDPOINT") ?? DefaultEndpoint;
        return new HttpWikiSourceOptions
        {
            Endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.TrimEnd('/'),
            CacheRoot = cacheRoot ?? Environment.GetEnvironmentVariable("ELLEN_WIKI_CACHE") ?? string.Empty,
        };
    }
}
