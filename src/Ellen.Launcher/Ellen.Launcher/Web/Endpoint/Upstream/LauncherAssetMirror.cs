// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Setting;
using System.Collections.Frozen;
using System.IO;

namespace Ellen.Launcher.Web.Endpoint.Upstream;

/// <summary>
/// 图标资源的自建站路由。
///
/// 背景：图标不随安装包下发，运行时统一走 <c>{Root}/static/raw/{category}/{fileName}</c>。
/// 那个 CDN 只覆盖它自己元数据里出现过的资源 —— 新角色（米提亚 / 瓦列里）除了头像本体，
/// 侧头像、名片、天赋、技能、抽卡图全部 404。所以 Snap 缺的那批文件由自建数据站
/// （<c>{wiki}/api/launcher/assets/{category}/{fileName}</c>）镜像补齐。
///
/// ⚠️ 2026-11-10 修正：线上 ellen.wiki 的 Worker **没有部署** <c>/api/launcher/assets</c> 路由
/// （那套 worker / 构建脚本改动一直只在工作区、没进 git）。此前本类「按分类无条件改走自建站」，
/// 结果所有没进本地 ImageCache 的图标都拿到 JSON 404 —— 祈愿记录里大量角色头像空白，
/// 只有早期缓存过的少数几张还能显示。
///
/// 现在改成**索引驱动**：只有随元数据同步下来的 <c>Assets.json</c>（记录 Snap 缺图的那批文件）
/// 命中的资源才走自建站，其余一律继续走 Snap CDN。下载层另有
/// <see cref="BuildDownloadCandidates"/> 的双源回退，自建站或 Snap 任一端挂了都不会留空白图标。
/// </summary>
public static class LauncherAssetMirror
{
    private const string DefaultEndpoint = "https://ellen.wiki";
    private const string DefaultBasePath = "/api/launcher/assets";
    private const string MirrorPathPrefix = "/api/launcher/assets/";
    private const string SnapPathPrefix = "/static/raw/";
    private const string SnapPrimary = "https://api.snaphutaorp.org";
    private const string SnapBackup = "https://api.hutaorp.org";

    private static readonly object Gate = new();
    private static readonly Lazy<string> ResolvedRoot = new(ResolveRoot, LazyThreadSafetyMode.ExecutionAndPublication);

    private static FrozenSet<string>? files;

    private static string Root => ResolvedRoot.Value;

    /// <summary>
    /// 把 <paramref name="category"/>/<paramref name="fileName"/> 解析成自建站 URL；
    /// 不在镜像索引里时返回 <see langword="null"/>，调用方回退到 Snap CDN。
    /// </summary>
    public static string? TryGetUrl(string category, string fileName)
    {
        return TryResolve(category, fileName, out string? url) ? url : null;
    }

    /// <summary>同上，但显式告知是否命中 ellen.wiki 镜像（未命中即回退 Snap，必须被记录）。</summary>
    public static bool TryResolve(string category, string fileName, out string? url)
    {
        url = null;
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        FrozenSet<string> index = EnsureLoaded();
        if (!index.Contains($"{category}/{fileName}"))
        {
            return false;
        }

        url = string.Intern($"{Root}{DefaultBasePath}/{category}/{fileName}");
        return true;
    }

    /// <summary>
    /// 给图标下载列出候选源（第一个永远是调用方给的原始 URL）。
    /// 自建站 URL 后面补 Snap 双域名；Snap URL 后面补另一个 Snap 域名与自建站。
    /// 自建站没部署图标路由 / Snap 缺新资源时，下载层按顺序重试即可，不必改动缓存键。
    /// </summary>
    public static IReadOnlyList<Uri> BuildDownloadCandidates(Uri uri)
    {
        List<Uri> candidates = [uri];
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
        {
            return candidates;
        }

        HashSet<string> seen = new(StringComparer.Ordinal) { uri.OriginalString };
        string path = uri.AbsolutePath;

        if (TrySplitAssetPath(path, MirrorPathPrefix, out string category, out string file))
        {
            // 自建站优先（它镜像了 Snap 缺的新资源），站点没部署或挂了就退回 Snap 双域名
            Add(ServerDomain.GetApiRoot(), SnapPathPrefix, category, file);
            Add(SnapPrimary, SnapPathPrefix, category, file);
            Add(SnapBackup, SnapPathPrefix, category, file);
        }
        else if (TrySplitAssetPath(path, SnapPathPrefix, out category, out file))
        {
            // Snap 主域失败 → 备用域 → 自建站（新角色资源 Snap 可能还没收录）
            Add(ServerDomain.GetApiRoot(), SnapPathPrefix, category, file);
            Add(SnapPrimary, SnapPathPrefix, category, file);
            Add(SnapBackup, SnapPathPrefix, category, file);
            Add(Root, MirrorPathPrefix, category, file);
        }

        return candidates;

        void Add(string host, string prefix, string cat, string fileName)
        {
            string url = $"{host}{prefix}{cat}/{Uri.EscapeDataString(fileName)}";
            if (seen.Add(url))
            {
                candidates.Add(new Uri(url, UriKind.Absolute));
            }
        }
    }

    private static bool TrySplitAssetPath(string path, string prefix, out string category, out string file)
    {
        category = string.Empty;
        file = string.Empty;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = path[prefix.Length..];
        int slash = rest.IndexOf('/');
        if (slash <= 0 || slash >= rest.Length - 1)
        {
            return false;
        }

        category = rest[..slash];
        foreach (char c in category)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        file = Uri.UnescapeDataString(rest[(slash + 1)..]);
        return file.LastIndexOf('.') > 0;
    }

    /// <summary>
    /// 未命中镜像索引的回退计数（category → 次数）。P4 要求「未命中必须显式告警」，
    /// 但图标调用量极大，不能每次写一条诊断；这里聚合成可展示的快照。
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> FallbackCounts = new(StringComparer.Ordinal);

    private static long fallbackTotal;

    public static void RecordFallback(string category)
    {
        FallbackCounts.AddOrUpdate(category, 1, static (_, v) => v + 1);
        Interlocked.Increment(ref fallbackTotal);
    }

    /// <summary>诊断页展示：总回退次数、涉及分类数、Top 5 分类。空表示全部命中 ellen.wiki。</summary>
    public static (long Total, int CategoryCount, IReadOnlyList<string> TopCategories) GetFallbackSnapshot()
    {
        IReadOnlyList<string> top = [.. FallbackCounts
            .OrderByDescending(p => p.Value)
            .Take(5)
            .Select(p => $"{p.Key}×{p.Value}")];
        return (Interlocked.Read(ref fallbackTotal), FallbackCounts.Count, top);
    }

    public static void ResetFallbackCounters()
    {
        FallbackCounts.Clear();
        Interlocked.Exchange(ref fallbackTotal, 0);
    }

    /// <summary>同步完成后调用：下一轮重新读索引，不必重启应用。</summary>
    public static void Invalidate()
    {
        lock (Gate)
        {
            files = null;
        }
    }

    private static FrozenSet<string> EnsureLoaded()
    {
        FrozenSet<string>? cached = files;
        if (cached is not null)
        {
            return cached;
        }

        lock (Gate)
        {
            return files ??= Load() ?? FrozenSet<string>.Empty;
        }
    }

    private static FrozenSet<string>? Load()
    {
        try
        {
            /* 与 WikiMetadataSyncService 的落盘位置一致：<LocalCache>/Metadata/Wiki/Genshin/<locale>/ */
            string indexRoot = Path.Combine(EllenRuntime.LocalCacheDirectory, "Metadata", "Wiki", "Genshin");
            if (!Directory.Exists(indexRoot))
            {
                return null;
            }

            /* 语言目录名由 CultureOptions 决定，这里不注入服务，直接找最新的那份索引 */
            string? latest = null;
            DateTime latestAt = DateTime.MinValue;
            foreach (string candidate in Directory.EnumerateFiles(indexRoot, "Assets.json", SearchOption.AllDirectories))
            {
                DateTime at = File.GetLastWriteTimeUtc(candidate);
                if (at > latestAt)
                {
                    latestAt = at;
                    latest = candidate;
                }
            }

            if (latest is null)
            {
                return null;
            }

            /* 索引由 scripts/lib/assets.mjs 生成，键名是小写 files；旧实现按 PascalCase 绑定，
               导致索引恒为空、所有图标都被判成「未命中」。这里显式指定并兼容大小写。 */
            AssetIndex? payload = JsonSerializer.Deserialize<AssetIndex>(File.ReadAllText(latest), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (payload?.Files is not { Count: > 0 })
            {
                return null;
            }

            return payload.Files.ToFrozenSet(StringComparer.Ordinal);
        }
        catch (Exception)
        {
            /* 索引只是加速项，读失败就退回「全部按 Snap 路由」，不要影响启动 */
            return null;
        }
    }

    private static string ResolveRoot()
    {
        string fromEnvironment = Environment.GetEnvironmentVariable("ELLEN_WIKI_ENDPOINT") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.TrimEnd('/');
        }

        string configured = LocalSetting.Get(SettingKeys.WikiMetadataEndpoint, DefaultEndpoint);
        return string.IsNullOrWhiteSpace(configured) ? DefaultEndpoint : configured.TrimEnd('/');
    }

    private sealed class AssetIndex
    {
        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = [];
    }
}
