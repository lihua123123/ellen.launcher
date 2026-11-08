// P2 演练器：用本地镜像（FileWikiDataSource）真实跑一遍 753 条装配、双读比对、
// 深链还原与回滚演练，产出机器可读报告。运行时不调用它，UI 走 WikiEnemyCatalogService。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class WikiP2Paths
{
    /// <summary>ellen-wiki/src/data/enemies（含 index.json 与 items/）。</summary>
    public string EnemyDataRoot { get; init; } = string.Empty;

    /// <summary>ellen-wiki/dist/api/launcher/manifest.json。</summary>
    public string SnapshotManifestPath { get; init; } = string.Empty;

    /// <summary>workspace/wiki-migration/reports/alias-statekey-map.json。</summary>
    public string AliasMapPath { get; init; } = string.Empty;

    /// <summary>workspace/wiki-migration/reports/orphan-images.json。</summary>
    public string OrphanListPath { get; init; } = string.Empty;

    /// <summary>P2 图片缓存根目录；留空表示只走在线 URL（不打包）。</summary>
    public string ImageCacheRoot { get; init; } = string.Empty;

    /// <summary>
    /// P5 客户端 753 条目 JSON 随包根目录（方案 B 地基）：含 <c>index.json</c> + 扁平 <c>&lt;slug&gt;.json</c>。
    /// 默认指向打包产物的 Web753 目录；运行时由安装环境经 ELLEN_WIKI_BUNDLE 覆盖。
    /// </summary>
    public string BundledWebRoot { get; init; } = string.Empty;

    /// <summary>
    /// P6 随包图片根目录：含 <c>assets-manifest.json</c> + <c>orig/</c> + <c>thumbs/</c>。
    /// 默认取随包 753 目录下的 <c>assets</c>；运行时由 <c>ELLEN_WIKI_ASSETS</c> 覆盖。
    /// </summary>
    public string BundledAssetsRoot { get; init; } = string.Empty;

    public bool IsComplete => File.Exists(Path.Combine(EnemyDataRoot, "index.json")) && File.Exists(SnapshotManifestPath);

    /// <summary>随包 753 目录是否就绪（index + 至少 1 个 item）。</summary>
    public bool HasBundled753 => !string.IsNullOrEmpty(BundledWebRoot)
        && File.Exists(Path.Combine(BundledWebRoot, "index.json"))
        && Directory.EnumerateFiles(BundledWebRoot, "*.json").Any(f => !string.Equals(Path.GetFileName(f), "index.json", StringComparison.OrdinalIgnoreCase) && !string.Equals(Path.GetFileName(f), "bundle-manifest.json", StringComparison.OrdinalIgnoreCase));
}

public sealed class WikiP2Report
{
    public string GeneratedAt { get; init; } = string.Empty;
    public string Phase { get; init; } = "P2";
    public string UiSource { get; init; } = string.Empty;
    public string DefaultUiSource { get; init; } = WikiSourceFeatureFlag.Legacy722Bundled;
    public bool DefaultIsLegacy { get; init; }
    public string SourceStamp { get; init; } = string.Empty;
    public string CurveSourceText { get; init; } = string.Empty;
    /// <summary>zip 内是否真的有 MonsterCurve（A1 交付前为 false，交付后为 true）。</summary>
    public bool CurveInZip { get; init; }
    /// <summary>曲线缺失告警：zip 里没有 MonsterCurve 时必须为 true，且来源文案必须显式写「内置兜底」。</summary>
    public bool CurveStrictAlert { get; init; }

    public int ItemsParsed { get; init; }
    public int ItemsExpected { get; init; } = 753;
    public int CategoryCount { get; init; }
    public int UnknownBaseStats { get; init; }
    public int ResistanceStateCount { get; init; }
    public int ImageReferences { get; init; }
    public int ImageCacheHits { get; init; }
    public int ImageCacheMisses { get; init; }
    public int OrphanSkipped { get; init; }
    public int OrphanReported { get; init; }
    public bool OrphansExcludedFromUi { get; init; }
    public int EvidenceCapsulesRendered { get; init; }

    public double LoadMilliseconds { get; init; }
    public double FilterMilliseconds { get; init; }
    public double ImageResolveMilliseconds { get; init; }
    public double DeepLinkMilliseconds { get; init; }

    public int SearchTokenTotal { get; init; }
    public int SearchHitByNameEn { get; init; }
    public int SearchHitByAlias { get; init; }
    public int SearchHitByLegacyCategory { get; init; }
    public int SearchHitByOldId { get; init; }

    public int DeepLinkTotal { get; init; }
    public int DeepLinkExact { get; init; }
    public int DeepLinkDegraded { get; init; }
    public IReadOnlyList<WikiP2DeepLinkRow> DeepLinkRows { get; init; } = [];

    public bool RollbackDrillPassed { get; init; }
    public string RollbackDrillDetail { get; init; } = string.Empty;
    public IReadOnlyList<string> SwitchLog { get; init; } = [];

    public IReadOnlyList<WikiDiagnostic> Diagnostics { get; init; } = [];
    public IReadOnlyList<string> Failures { get; init; } = [];
    public bool Pass { get; init; }
}

public sealed class WikiP2DeepLinkRow
{
    public string OldId { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string StateKey { get; init; } = string.Empty;
    public string Strategy { get; init; } = string.Empty;
    public bool Degraded { get; init; }
}

public static class WikiP2Harness
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task<WikiP2Report> RunAsync(WikiP2Paths paths, CancellationToken token = default)
    {
        List<string> failures = [];
        if (!paths.IsComplete)
        {
            failures.Add("本地镜像路径不完整：需要 index.json 与 manifest.json");
            return new WikiP2Report { GeneratedAt = DateTime.UtcNow.ToString("O"), Failures = failures };
        }

        WikiDiagnosticsLog log = new();
        WikiSourceFeatureFlag flag = new() { Strict = true };
        List<string> switchLog = [];

        FileEnemyWebSource webSource = new(paths.EnemyDataRoot);
        FileLauncherSnapshotSource snapshotSource = new(paths.SnapshotManifestPath);
        EnemyWebReadResult web = await webSource.ReadAsync(token).ConfigureAwait(false);
        LauncherSnapshotReadResult snapshot = await snapshotSource.ReadAsync(token).ConfigureAwait(false);
        foreach (WikiDiagnostic d in web.Diagnostics.Concat(snapshot.Diagnostics))
        {
            log.Add(d.Kind, web.Stamp, d.Detail, d.FallbackReason);
        }

        List<AliasRecord> aliases = LoadAliases(paths.AliasMapPath, log, web.Stamp);
        HashSet<string> orphans = LoadOrphans(paths.OrphanListPath);

        // 允许引用的图片 = 753 items 里真实出现过的 id/file；orphan 天然不在其中。
        HashSet<string> allowed = new(StringComparer.Ordinal);
        foreach ((string id, EnemyItemDto dto) in web.Items)
        {
            AddRef(allowed, id, dto.Icon);
            foreach (string f in dto.Images) AddRef(allowed, id, f);
            foreach (string f in dto.LeylineImages) AddRef(allowed, id, f);
            foreach (EnemySkillDto skill in dto.Skills) AddRef(allowed, id, skill.Icon);
            if (dto.Leyline is { Portrait: { } portrait }) AddRef(allowed, id, portrait);
        }

        WikiImageCache cache = new(allowed, paths.ImageCacheRoot, log, web.Stamp);
        WikiEnemyCatalogResult catalog = WikiEnemyCatalog.Build(web, snapshot, aliases, allowed, cache, log, flag);

        bool orphansExcluded = orphans.All(o => !allowed.Contains(o));
        int evidenceRendered = catalog.Items.Count(i => i.HasEvidence);

        // 搜索 token 命中：按真实字段分别验证。
        int hitNameEn = catalog.Items.Count(i => i.NameEn is { } en && i.SearchTokens.Contains(en));
        int hitAlias = catalog.Items.Count(i => i.Aliases.Count > 0 && i.Aliases.All(a => i.SearchTokens.Contains(a)));
        int hitLegacy = catalog.Items.Count(i => i.LegacyCategoryName is { } legacy && i.SearchTokens.Contains(legacy));
        Dictionary<string, string> slugToOld = [];
        foreach (AliasRecord r in aliases) slugToOld.TryAdd(r.NewId, r.OldId);
        int hitOldId = catalog.Items.Count(i => slugToOld.TryGetValue(i.Id, out string? old) && i.SearchTokens.Contains(old));

        // 深链：34 组多对一的全部 83 个旧 id 逐条还原。
        // 两个方向都要验：旧 InternalName → (slug,stateKey)；以及 slug+stateKey 回查是否精确。
        Stopwatch dlWatch = Stopwatch.StartNew();
        AliasResolver resolver = new(aliases, log);
        WikiDeepLink deepLink = new(resolver);
        List<WikiP2DeepLinkRow> rows = [];
        HashSet<string> groupSlugs = [.. aliases.GroupBy(a => a.NewId).Where(g => g.Count() > 1).Select(g => g.Key)];
        List<AliasRecord> groupMembers = [.. aliases.Where(a => groupSlugs.Contains(a.NewId))];
        foreach (AliasRecord record in groupMembers)
        {
            // 旧 ID 入口：应解析到登记的 slug + stateKey。
            (string fromOldSlug, string fromOldKey, string fromOldStrategy, bool fromOldDegraded) = deepLink.Resolve(record.OldId, null);
            // 新形态入口：slug + stateKey 应精确命中冻结键，不落默认状态。
            (string slug, string stateKey, string strategy, bool degraded) = deepLink.Resolve(record.NewId, record.StateKey);
            bool exact = !fromOldDegraded && !degraded
                && string.Equals(fromOldSlug, record.NewId, StringComparison.Ordinal)
                && string.Equals(fromOldKey, record.StateKey, StringComparison.Ordinal)
                && string.Equals(slug, record.NewId, StringComparison.Ordinal)
                && string.Equals(stateKey, record.StateKey, StringComparison.Ordinal);
            rows.Add(new WikiP2DeepLinkRow
            {
                OldId = record.OldId,
                Slug = slug,
                StateKey = stateKey,
                Strategy = exact ? $"exact({fromOldStrategy}→{strategy})" : $"mismatch({fromOldStrategy}→{strategy})",
                Degraded = !exact,
            });
        }
        dlWatch.Stop();
        int dlExact = rows.Count(r => !r.Degraded);

        // 回滚演练：默认 legacy → 切 web → 切回 legacy。
        bool defaultIsLegacy = flag.IsLegacyMode;
        switchLog.Add($"初始={flag.UiSource}");
        flag.Switch(WikiSourceFeatureFlag.EnemyWeb753);
        switchLog.Add($"切流后={flag.UiSource}, IsWebMode={flag.IsWebMode}");
        bool webModeReached = flag.IsWebMode;
        flag.Switch(WikiSourceFeatureFlag.Legacy722Bundled);
        switchLog.Add($"回滚后={flag.UiSource}, IsLegacyMode={flag.IsLegacyMode}");
        bool rollback = flag.RollbackDrill() && flag.IsLegacyMode && webModeReached && defaultIsLegacy;

        // 验收断言。
        if (catalog.TotalCount != 753) failures.Add($"条目数 {catalog.TotalCount} ≠ 753");
        if (catalog.UnknownBaseStatsCount != 40) failures.Add($"空 baseStats {catalog.UnknownBaseStatsCount} ≠ 40");
        if (catalog.Items.Any(i => i.BaseValueUnknown && i.BaseValueText != "未知")) failures.Add("空 baseStats 未显示「未知」");
        if (catalog.Items.Any(i => i.BaseValueText.Contains("NaN", StringComparison.OrdinalIgnoreCase))) failures.Add("出现 NaN");
        if (catalog.ResistanceStateCount != 913) failures.Add($"抗性状态 {catalog.ResistanceStateCount} ≠ 913");
        if (!orphansExcluded) failures.Add("orphan 图片进入了允许引用集合");
        if (cache.OrphansSkipped > 0 && catalog.OrphanSkippedCount != cache.OrphansSkipped) failures.Add("orphan 跳过计数不一致");
        if (groupSlugs.Count != 34) failures.Add($"多对一组数 {groupSlugs.Count} ≠ 34");
        if (rows.Count != 83) failures.Add($"多对一旧 id 数 {rows.Count} ≠ 83");
        if (dlExact != rows.Count) failures.Add($"深链精确还原 {dlExact}/{rows.Count}");
        if (rows.Any(r => r.Degraded)) failures.Add("深链出现降级");
        if (!rollback) failures.Add("回滚演练失败");
        /* 曲线来源文案必须与 zip 实际情况一致，两种状态都不许「静默」：
           zip 有 MonsterCurve → 必须说明来自 zip，且不能还留着「内置兜底」；
           zip 没有 → 必须显式出现「内置兜底」。
           旧写法只承认「缺失」一种状态，A1 把 MonsterCurve 放进 zip 后就变成假失败。 */
        bool curveFromZip = snapshot.Curves.MonsterCurvePresentInZip;
        bool textSaysFallback = catalog.CurveSourceText.Contains("内置兜底", StringComparison.Ordinal);
        if (curveFromZip == textSaysFallback)
        {
            failures.Add(curveFromZip
                ? "zip 内已有 MonsterCurve，但曲线来源文案仍写「内置兜底」"
                : "曲线缺失未显式显示兜底来源");
        }
        if (evidenceRendered != 0) failures.Add($"证据胶囊应因数据缺失不渲染，实际 {evidenceRendered}");

        return new WikiP2Report
        {
            GeneratedAt = DateTime.UtcNow.ToString("O"),
            UiSource = WikiSourceFeatureFlag.EnemyWeb753,
            DefaultIsLegacy = defaultIsLegacy,
            SourceStamp = catalog.SourceStampText,
            CurveSourceText = catalog.CurveSourceText,
            CurveInZip = snapshot.Curves.MonsterCurvePresentInZip,
            CurveStrictAlert = !snapshot.Curves.MonsterCurvePresentInZip,
            ItemsParsed = catalog.TotalCount,
            CategoryCount = catalog.Categories.Count,
            UnknownBaseStats = catalog.UnknownBaseStatsCount,
            ResistanceStateCount = catalog.ResistanceStateCount,
            ImageReferences = catalog.ImageRefCount,
            ImageCacheHits = catalog.ImageCacheHits,
            ImageCacheMisses = catalog.ImageCacheMisses,
            OrphanSkipped = catalog.OrphanSkippedCount,
            OrphanReported = orphans.Count,
            OrphansExcludedFromUi = orphansExcluded,
            EvidenceCapsulesRendered = evidenceRendered,
            LoadMilliseconds = Math.Round(catalog.LoadMilliseconds, 3),
            FilterMilliseconds = Math.Round(catalog.FilterMilliseconds, 3),
            ImageResolveMilliseconds = Math.Round(catalog.ImageResolveMilliseconds, 3),
            DeepLinkMilliseconds = Math.Round(dlWatch.Elapsed.TotalMilliseconds, 3),
            SearchTokenTotal = catalog.Items.Sum(i => i.SearchTokens.Count),
            SearchHitByNameEn = hitNameEn,
            SearchHitByAlias = hitAlias,
            SearchHitByLegacyCategory = hitLegacy,
            SearchHitByOldId = hitOldId,
            DeepLinkTotal = rows.Count,
            DeepLinkExact = dlExact,
            DeepLinkDegraded = rows.Count(r => r.Degraded),
            DeepLinkRows = rows,
            RollbackDrillPassed = rollback,
            RollbackDrillDetail = string.Join(" → ", switchLog),
            SwitchLog = switchLog,
            Diagnostics = catalog.Diagnostics,
            Failures = failures,
            Pass = failures.Count == 0,
        };
    }

    private static void AddRef(HashSet<string> set, string id, string? file)
    {
        if (!string.IsNullOrWhiteSpace(file))
        {
            set.Add($"{id}/{file}");
        }
    }

    private static List<AliasRecord> LoadAliases(string path, WikiDiagnosticsLog log, WikiSourceStamp stamp)
    {
        if (!File.Exists(path))
        {
            log.Add("alias-map-missing", stamp, $"{path} 不存在，深链降级为 slug 默认状态", "alias-statekey-map.json 缺失");
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            AliasMapFile? file = JsonSerializer.Deserialize<AliasMapFile>(stream, Json);
            return (file?.Records ?? []).Select(r => new AliasRecord
            {
                OldId = r.OldId,
                NewId = r.Slug,
                StateKey = r.StateKey,
                StateLabel = string.Empty,
                Rule = "statekey-map",
                MatchedVia = r.Origin,
            }).ToList();
        }
        catch (JsonException ex)
        {
            log.Add("alias-map-parse", stamp, ex.Message, "alias map JSON 解析失败");
            return [];
        }
    }

    private static HashSet<string> LoadOrphans(string path)
    {
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            OrphanFile? file = JsonSerializer.Deserialize<OrphanFile>(stream, Json);
            return new HashSet<string>(file?.Orphans ?? [], StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private sealed class AliasMapFile
    {
        [JsonPropertyName("records")] public List<AliasMapRow> Records { get; set; } = [];
    }

    private sealed class AliasMapRow
    {
        [JsonPropertyName("oldId")] public string OldId { get; set; } = string.Empty;
        [JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;
        [JsonPropertyName("stateKey")] public string StateKey { get; set; } = string.Empty;
        [JsonPropertyName("origin")] public string Origin { get; set; } = string.Empty;
    }

    private sealed class OrphanFile
    {
        [JsonPropertyName("orphans")] public List<string> Orphans { get; set; } = [];
        [JsonPropertyName("orphanCount")] public int OrphanCount { get; set; }
    }
}
