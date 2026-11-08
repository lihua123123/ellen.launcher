// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Setting;
using System.Collections.Frozen;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Ellen.Launcher.Web.Endpoint.Upstream;

/// <summary>
/// 图标资源的自建站路由。
///
/// 背景：图标不随安装包下发，运行时统一走 <c>{Root}/static/raw/{category}/{fileName}</c>。
/// 那个 CDN 只覆盖它自己元数据里出现过的资源 —— 新角色（米提亚 / 瓦列里）除了头像本体，
/// 侧头像、名片、天赋、技能、抽卡图全部 404。
///
/// 所以角色 / 武器 / 物品这几类图标一律改走自建数据站
/// （<c>{wiki}/api/launcher/assets/{category}/{fileName}</c>）。服务端先看构建期镜像，
/// 再走边缘缓存，最后按 Snap 备用域 → Snap 主域 → Enka → Project Amber 回源，
/// 源站缺图也不会出现空白图标。
///
/// 其它分类（圣遗物、成就、敌人等）保持原样，继续走 Snap。
/// </summary>
public static class LauncherAssetMirror
{
    private const string DefaultEndpoint = "https://ellen.wiki";
    private const string DefaultBasePath = "/api/launcher/assets";

    /// <summary>
    /// 由自建站托管的图标分类。名单与 <c>scripts/lib/assets.mjs</c> 的枚举保持一致，
    /// 覆盖角色卡面 / 头像 / 名片 / 天赋 / 技能 / 装扮 / 游迹 / 武器。
    ///
    /// 故意**不含 ItemIcon**：物品图标在背包里一次要出上千张，全量走中转会拖慢首屏；
    /// 新角色缺的那几个材料图标由 <c>Assets.json</c> 索引按需命中，其余继续走 Snap。
    /// </summary>
    private static readonly FrozenSet<string> MirroredCategories = new[]
    {
        "AvatarIcon", "AvatarCard", "GachaAvatarImg", "GachaAvatarIcon",
        "NameCardIcon", "NameCardPic", "NameCardPicAlpha", "TraceEffectIcon", "Costume",
        "Talent", "Skill", "EquipIcon", "GachaEquipIcon",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly object Gate = new();
    private static readonly Lazy<string> ResolvedRoot = new(ResolveRoot, LazyThreadSafetyMode.ExecutionAndPublication);

    private static FrozenSet<string>? files;

    private static string Root => ResolvedRoot.Value;

    /// <summary>
    /// 把 <paramref name="category"/>/<paramref name="fileName"/> 解析成自建站 URL；
    /// 不属于托管分类且不在镜像索引里时返回 <see langword="null"/>，调用方回退到 Snap CDN。
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

        if (MirroredCategories.Contains(category))
        {
            _ = EnsureLoaded();
            url = string.Intern($"{Root}{DefaultBasePath}/{category}/{fileName}");
            return true;
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
    /// 未镜像分类的回退计数（category → 次数）。P4 要求「未命中必须显式告警」，
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

            AssetIndex? payload = JsonSerializer.Deserialize<AssetIndex>(File.ReadAllText(latest));
            if (payload?.Files is not { Count: > 0 })
            {
                return null;
            }

            return payload.Files.ToFrozenSet(StringComparer.Ordinal);
        }
        catch (Exception)
        {
            /* 索引只是加速项，读失败就退回「按分类路由 + Snap」，不要影响启动 */
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
        public List<string> Files { get; set; } = [];
    }
}
