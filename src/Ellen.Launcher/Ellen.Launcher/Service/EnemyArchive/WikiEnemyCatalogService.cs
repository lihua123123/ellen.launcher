// P2/P3 运行时数据源：用注入的 IEnemyWebSource（生产 HTTP / 开发 File 镜像）装配 753 条展示项。
// 默认 feature flag 为 legacy-722-bundled，本服务不会被调用；切到 enemy-web-753 才走这里。
// 图片 P3 为在线 + 内容寻址缓存（不打包、不宣称离线）；数据源为空时显式诊断、不静默回退。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Service.EnemyArchive;

public interface IWikiEnemyCatalogService
{
    ValueTask<WikiEnemyCatalogResult?> LoadAsync(CancellationToken token = default);
    IReadOnlyList<WikiDiagnostic> Diagnostics { get; }
    IReadOnlyList<AliasRecord> Aliases { get; }
    string? MirrorUnavailableReason { get; }

    /// <summary>随包图片包（P6）：清单、license.attribution、随包图片数；未打包时 Present=false。</summary>
    EnemyAssetsBundle AssetsBundle { get; }

    /// <summary>同步完成后丢弃装配结果，下次 LoadAsync 用新数据重建。</summary>
    void Invalidate();
}

[Service(ServiceLifetime.Singleton, typeof(IWikiEnemyCatalogService))]
public sealed partial class WikiEnemyCatalogService : IWikiEnemyCatalogService
{
    private readonly WikiSourceFeatureFlag flag;
    private readonly WikiDiagnosticsLog log;
    private readonly WikiP2Paths paths;
    private readonly IEnemyWebSource enemyWebSource;
    private readonly ILauncherSnapshotSource snapshotSource;
    private readonly WikiCacheStore cache;

    private WikiEnemyCatalogResult? cached;
    private IReadOnlyList<AliasRecord> aliases = [];
    private string? unavailableReason;

    /* P6 随包图片：只加载一次（清单 + 目录索引），之后每次装配复用。
       懒加载放在 LoadAsync 里，flag 为 legacy 时完全不碰磁盘。 */
    private EnemyAssetsBundle? assetsBundle;

    [GeneratedConstructor]
    public partial WikiEnemyCatalogService(IServiceProvider serviceProvider);

    public IReadOnlyList<WikiDiagnostic> Diagnostics => log.Records;

    public IReadOnlyList<AliasRecord> Aliases => aliases;

    public EnemyAssetsBundle AssetsBundle => assetsBundle ?? EnemyAssetsBundle.Empty;

    /// <summary>数据源不可用时给出原因；空表示可用。</summary>
    public string? MirrorUnavailableReason => unavailableReason;

    public void Invalidate()
    {
        cached = null;
    }

    public async ValueTask<WikiEnemyCatalogResult?> LoadAsync(CancellationToken token = default)
    {
        if (!flag.IsWebMode)
        {
            return null;
        }

        if (cached is { } hit)
        {
            return hit;
        }

        // 首屏走纯缓存读取（HTTP 源不触网、File 源直接读镜像），保证启动不被同步阻塞。
        if (cache.RecoveryNote is { Length: > 0 } recovery)
        {
            log.Add("cache-index-recovered", WikiSourceStamp.FromBundled("缓存索引恢复"),
                recovery, "索引损坏已自愈；下次同步会补齐缺失文件");
        }

        // W1-4：随包 753 / 缓存读取耗时（含解析）。离线下即 bundled753ReadMs 的自动来源。
        StartupProbe.Mark("source-read-start");
        EnemyWebReadResult web = enemyWebSource is ICachedEnemyWebSource cachedSource
            ? await cachedSource.ReadCachedAsync(token).ConfigureAwait(false)
            : await enemyWebSource.ReadAsync(token).ConfigureAwait(false);
        StartupProbe.Mark("source-read-done");

        if (web.Items.Count == 0)
        {
            unavailableReason = enemyWebSource is ICachedEnemyWebSource
                ? "本地缓存为空，后台同步中（首屏保持 legacy，不阻塞启动）"
                : "敌人数据源返回 0 条（检查网络/镜像与缓存），页面保持 legacy";
            log.Add("enemy-source-empty", web.Stamp, unavailableReason, unavailableReason);
            return null;
        }

        LauncherSnapshotReadResult snapshot = snapshotSource is ICachedLauncherSnapshotSource cachedSnapshot
            ? await cachedSnapshot.ReadCachedAsync(token).ConfigureAwait(false)
            : await snapshotSource.ReadAsync(token).ConfigureAwait(false);
        unavailableReason = null;
        StartupProbe.Mark("assemble-snapshot");

        // 允许的图片引用 = items 里真实出现过的 id/file；orphan 不进缓存、不进 UI。
        HashSet<string> allowed = new(StringComparer.Ordinal);
        foreach ((string id, EnemyItemDto dto) in web.Items)
        {
            AddRef(allowed, id, dto.Icon);
            foreach (string f in dto.Images) AddRef(allowed, id, f);
            foreach (string f in dto.LeylineImages) AddRef(allowed, id, f);
            foreach (EnemySkillDto skill in dto.Skills) AddRef(allowed, id, skill.Icon);
            foreach (EnemyStateDto state in dto.States ?? [])
            {
                AddRef(allowed, id, state.Icon);
                foreach (EnemySkillDto skill in state.Skills ?? []) AddRef(allowed, id, skill.Icon);
            }
            if (dto.Leyline is { Portrait: { } portrait }) AddRef(allowed, id, portrait);
        }
        StartupProbe.Mark("assemble-refs");

        /* P6 随包图片：装配前载入一次。离线（数据来自随包 753）时允许用 256px 缩略图顶替
           详情大图，避免断网进详情页只有占位图；在线时缩略图不参与，保证画质不降。 */
        assetsBundle ??= EnemyAssetsBundle.Load(paths.BundledAssetsRoot, log);
        bool offlineBundled = string.Equals(web.Stamp.Contract, WikiSourceStamp.Bundled753, StringComparison.Ordinal);
        StartupProbe.Mark("assemble-bundle");

        WikiImageCache images = new(
            allowed,
            paths.ImageCacheRoot,
            log,
            web.Stamp,
            key => cache.ReadBytes("image:" + key) is null ? null : cache.FilePath("image:" + key),
            assetsBundle,
            preferBundledThumb: offlineBundled);
        aliases = LoadAliases();
        StartupProbe.Mark("assemble-alias");
        cached = WikiEnemyCatalog.Build(web, snapshot, aliases, allowed, images, log, flag);
        StartupProbe.Mark("assemble-built");
        return cached;
    }

    private List<AliasRecord> LoadAliases()
    {
        if (!File.Exists(paths.AliasMapPath))
        {
            log.Add("alias-map-missing", WikiSourceStamp.FromBundled("alias map 缺失"),
                "alias-statekey-map.json 缺失，深链降级为 slug 默认状态（显式）", "alias map 未配置");
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(paths.AliasMapPath);
            System.Text.Json.JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
            AliasMapFile? file = System.Text.Json.JsonSerializer.Deserialize<AliasMapFile>(stream, options);
            return (file?.Records ?? []).Select(r => new AliasRecord
            {
                OldId = r.OldId,
                NewId = r.Slug,
                StateKey = r.StateKey,
                Rule = "statekey-map",
                MatchedVia = r.Origin,
            }).ToList();
        }
        catch (System.Text.Json.JsonException ex)
        {
            log.Add("alias-map-parse", WikiSourceStamp.FromBundled("alias map 解析失败"), ex.Message, "alias map JSON 非法");
            return [];
        }
    }

    private static void AddRef(HashSet<string> set, string id, string? file)
    {
        if (!string.IsNullOrWhiteSpace(file))
        {
            set.Add($"{id}/{file}");
        }
    }

    private sealed class AliasMapFile
    {
        [System.Text.Json.Serialization.JsonPropertyName("records")]
        public List<AliasMapRow> Records { get; set; } = [];
    }

    private sealed class AliasMapRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("oldId")] public string OldId { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("stateKey")] public string StateKey { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("origin")] public string Origin { get; set; } = string.Empty;
    }
}
