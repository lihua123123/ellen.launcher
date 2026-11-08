// P2 展示层：把 C2 领域模型装配成 ViewModel 可直接绑定的展示项。
// 这里不是 DTO（DTO 在 Contracts/），XAML 只认本文件的类型，禁止把 API 字段直接绑 UI。
// 纯 net10.0，无 WinUI 依赖，可单测。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Domain.WikiSource;

/// <summary>敌人图鉴列表/详情的展示项。字段全部是"可显示字符串 + 已展开集合"。</summary>
public sealed class WikiEnemyViewItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? NameEn { get; init; }
    public string? Subtitle { get; init; }
    public string CategoryKey { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public string? TierName { get; init; }

    /// <summary>旧中文分类名，仅作搜索兼容，不作为主分类。</summary>
    public string? LegacyCategoryName { get; init; }

    /// <summary>40 条空 baseStats 显示「未知」，绝不出现 0 / NaN。</summary>
    public string BaseValueText { get; init; } = "未知";

    public bool BaseValueUnknown { get; init; }

    public string IconUri { get; init; } = string.Empty;
    public bool HasIcon => !string.IsNullOrEmpty(IconUri);

    /// <summary>详情页大图（reference*.png）。离线且未缓存时是随包 256px 缩略图。</summary>
    public string ArtUri { get; init; } = string.Empty;
    public bool HasArt => !string.IsNullOrEmpty(ArtUri);

    /// <summary>大图是否是降质的随包缩略图预览（离线兜底），UI 需要显式说明，不能假装是原图。</summary>
    public bool ArtIsPreview { get; init; }
    public string? ArtNote => ArtIsPreview ? "离线：显示随包 256px 预览（原图需联网或缓存后查看）" : null;

    public IReadOnlyList<string> Aliases { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>搜索 token：name / nameEn / aliases / tags / categoryLabel / 旧中文分类 / 旧 InternalName。</summary>
    public IReadOnlyList<string> SearchTokens { get; init; } = [];

    public IReadOnlyList<WikiResistanceState> ResistanceStates { get; init; } = [];

    /// <summary>技能卡片：图标已解析成本地/在线 URI，说明已是纯文本。</summary>
    public IReadOnlyList<WikiSkillView> Skills { get; init; } = [];

    /// <summary>基础数值 + 曲线名，给等级滑块换算用；源里没有 baseStats 时为 null。</summary>
    public WikiBaseValue? BaseValue { get; init; }

    /// <summary>特殊机制（源里的 notes）。空集合 → UI 整块隐藏，不留空标题。</summary>
    public IReadOnlyList<WikiNoteView> Notes { get; init; } = [];
    public bool HasNotes => Notes.Count > 0;

    /// <summary>元素能量（产球）三列：坚盾值 / 元素微粒 / 元素晶球。无数据 → 整块隐藏。</summary>
    public IReadOnlyList<WikiEnergyRowView> EnergyRows { get; init; } = [];
    public bool HasEnergy => EnergyRows.Count > 0;

    /// <summary>技能块是否渲染：空集合时整块隐藏，不留只有标题的空卡片。</summary>
    public bool HasSkills => Skills.Count > 0;
    public IReadOnlyList<WikiImageRef> Images { get; init; } = [];
    public IReadOnlyList<WikiLevelRow> LevelRows { get; init; } = [];

    /// <summary>证据胶囊：源里没有 evidence 就是 null，UI 不渲染，禁止编造等级。</summary>
    public string? EvidenceText { get; init; }
    public bool HasEvidence => !string.IsNullOrEmpty(EvidenceText);

    /// <summary>深链命中的稳定状态键（default / state:key / leyline:0 / alias:i）。</summary>
    public string StateKey { get; init; } = "default";

    public string? CurveSourceText { get; init; }
    public WikiSourceStamp Stamp { get; init; } = new();
}

public sealed class WikiEnemyCategoryGroup
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>技能卡片：图标 URI 已由 <see cref="WikiImageCache"/> 解析（随包 → 缓存 → 在线），说明是纯文本。</summary>
public sealed class WikiSkillView
{
    public string Name { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public bool HasTag => !string.IsNullOrEmpty(Tag);

    public string DescriptionText { get; init; } = string.Empty;
    public bool HasDescription => !string.IsNullOrEmpty(DescriptionText);

    /// <summary>技能图标（源里的 skill-N.webp）。上游没给图标 → 空字符串，UI 不渲染图标位。</summary>
    public string IconUri { get; init; } = string.Empty;
    public bool HasIcon => !string.IsNullOrEmpty(IconUri);
}

/// <summary>特殊机制的一行：标题可空，正文已由 <see cref="WikiRichText"/> 去标记。</summary>
public sealed class WikiNoteView
{
    public string? Title { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool HasTitle => !string.IsNullOrEmpty(Title);
}

/// <summary>元素能量一行的展示文本：坚盾值百分比，微粒/晶球为 0 时留空（与旧版一致）。</summary>
public sealed class WikiEnergyRowView
{
    public string HpText { get; init; } = string.Empty;
    public string ParticleText { get; init; } = string.Empty;
    public string OrbText { get; init; } = string.Empty;
}

public sealed class WikiEnemyCatalogResult
{
    public IReadOnlyList<WikiEnemyViewItem> Items { get; init; } = [];
    public IReadOnlyList<WikiEnemyCategoryGroup> Categories { get; init; } = [];
    public IReadOnlyList<WikiDiagnostic> Diagnostics { get; init; } = [];

    public int TotalCount { get; init; }
    public int UnknownBaseStatsCount { get; init; }
    public int ResistanceStateCount { get; init; }
    public int ImageRefCount { get; init; }
    public int OrphanSkippedCount { get; init; }
    public int ImageCacheHits { get; init; }
    public int ImageCacheMisses { get; init; }

    /// <summary>P6：命中随包原图的次数（零网络；列表页离线命中的主来源）。</summary>
    public int BundledImageHits { get; init; }

    /// <summary>P6：离线时用随包 256px 缩略图顶替原图的次数（详情大图降质预览）。</summary>
    public int BundledThumbPreviews { get; init; }
    public string CurveSourceText { get; init; } = string.Empty;
    public string SourceStampText { get; init; } = string.Empty;
    public string UiSource { get; init; } = string.Empty;

    /// <summary>P5：本次目录来自随包 753 离线基线（非 HTTP 缓存、非 legacy）。用于显示「离线可用」提示，不改变业务语义。</summary>
    public bool IsOfflineBundled { get; init; }

    public double LoadMilliseconds { get; init; }
    public double FilterMilliseconds { get; init; }
    public double ImageResolveMilliseconds { get; init; }
}

/// <summary>P2 图片：在线 URL + 内容寻址缓存；orphan 不进缓存，失败降级占位图并记录。</summary>
public sealed class WikiImageCache
{
    public const string PlaceholderUri = "ms-appx:///Assets/Blank.png";

    private readonly HashSet<string> allowedRefs;
    private readonly string cacheRoot;
    private readonly WikiDiagnosticsLog log;
    private readonly WikiSourceStamp stamp;
    private readonly Func<string, string?>? localResolver;
    private readonly EnemyAssetsBundle? bundle;
    private readonly bool preferBundledThumb;

    /// <param name="localResolver">
    /// 可选：内容寻址缓存解析器（入参 <c>id/file</c>，返回本地文件绝对路径或 null）。
    /// P3 HTTP 源下载的图片落在 <see cref="WikiCacheStore"/> 的内容寻址目录，
    /// 与「<c>cacheRoot/id/file</c> 扁平布局」不同，所以必须由调用方提供解析。
    /// </param>
    public WikiImageCache(
        IEnumerable<string> allowedRefs,
        string cacheRoot,
        WikiDiagnosticsLog log,
        WikiSourceStamp stamp,
        Func<string, string?>? localResolver = null,
        EnemyAssetsBundle? bundle = null,
        bool preferBundledThumb = false)
    {
        this.allowedRefs = new HashSet<string>(allowedRefs, StringComparer.Ordinal);
        this.cacheRoot = cacheRoot;
        this.log = log;
        this.stamp = stamp;
        this.localResolver = localResolver;
        this.bundle = bundle;
        this.preferBundledThumb = preferBundledThumb;
    }

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int OrphansSkipped { get; private set; }

    /// <summary>命中随包原图的次数（零网络，全质量）。</summary>
    public int BundledHits { get; private set; }

    /// <summary>用随包 256px 缩略图代替在线原图的次数（仅离线时发生，属降质预览）。</summary>
    public int BundledThumbPreviews { get; private set; }

    /// <summary>返回可绑定 URI：随包原图 → 内容寻址缓存 → 扁平本地缓存 →（离线）随包缩略图 → 在线 URL → 占位图。</summary>
    public string Resolve(WikiImageRef image)
    {
        string key = image.CacheKey;
        if (!allowedRefs.Contains(key))
        {
            OrphansSkipped++;
            return PlaceholderUri;
        }

        /* ① 随包原图：A4 清单里没有 thumb 的条目（icon / 技能图标等小图）随包的就是原图本身，
           质量一致、零网络。这条排在缓存前面：本地文件比缓存目录更快，也不会被 LRU 淘汰。 */
        if (bundle?.TryResolveOriginal(image.EnemyId, image.File, out string bundledPath) == true)
        {
            BundledHits++;
            Hits++;
            return new Uri(bundledPath).AbsoluteUri;
        }

        // 内容寻址缓存：有 hash 时先查 <id>/<file>:<hash>，再退回 <id>/<file>
        // （P3 下载的图片没有 hash，P4 assets manifest 到位后才带 hash；两种都要能解析）。
        foreach (string candidate in Candidates(image))
        {
            if (localResolver?.Invoke(candidate) is { Length: > 0 } resolved && System.IO.File.Exists(resolved))
            {
                Hits++;
                return new Uri(resolved).AbsoluteUri;
            }
        }

        string local = string.IsNullOrEmpty(cacheRoot)
            ? string.Empty
            : System.IO.Path.Combine(cacheRoot, image.EnemyId, image.File);
        if (local.Length > 0 && System.IO.File.Exists(local))
        {
            Hits++;
            return new Uri(local).AbsoluteUri;
        }

        /* ② 离线：大图没有本地原图时用随包 256px 预览顶上，不直接掉占位图。
           只在 offlineBundled 时启用——在线时用缩略图会白白把详情大图降成 256px。 */
        if (preferBundledThumb && bundle?.TryResolveThumb(image.EnemyId, image.File, out string thumbPath) == true)
        {
            BundledThumbPreviews++;
            Hits++;
            log.Add("image-bundled-thumb-preview", stamp,
                $"{key}: 离线使用随包缩略图（256px 预览）", null);
            return new Uri(thumbPath).AbsoluteUri;
        }

        Misses++;
        if (string.IsNullOrWhiteSpace(image.Uri))
        {
            log.Add("image-fallback", stamp, $"{key}: 无在线 URL，降级占位图", "图片引用不可解析");
            return PlaceholderUri;
        }

        // P2 在线 + 内容寻址缓存：hash 可用时键为 id/file:hash，P4 补 assets manifest 后改本地路径。
        return image.Uri;
    }

    private static IEnumerable<string> Candidates(WikiImageRef image)
    {
        if (image.HashAvailable && !string.IsNullOrEmpty(image.ContentHash))
        {
            yield return WikiCacheStore.ImageKey(image.EnemyId, image.File, image.ContentHash);
        }

        yield return image.CacheKey;
    }
}

/// <summary>深链：slug + stateKey → slug 默认 → 旧 InternalName。降级必须写日志。</summary>
public sealed class WikiDeepLink
{
    private readonly AliasResolver resolver;

    public WikiDeepLink(AliasResolver resolver)
    {
        this.resolver = resolver;
    }

    public (string Slug, string StateKey, string Strategy, bool Degraded) Resolve(string? rawId, string? stateKey)
    {
        if (string.IsNullOrWhiteSpace(rawId))
        {
            return (string.Empty, "default", "empty", false);
        }

        // 显式判定：rawId 是冻结表里登记过的 slug（含中文 slug）→ 用 (slug, stateKey) 校验。
        // 不能靠「含 -」猜 slug，中文 slug（如「门扉前的弈局」）没有连字符。
        if (IsKnownSlug(rawId))
        {
            AliasResolution resolution = resolver.ResolveDeepLink(rawId, stateKey);
            return (resolution.Slug ?? rawId, resolution.StateKey, resolution.Strategy, resolution.Degraded);
        }

        AliasResolution byOld = resolver.Resolve(rawId);
        return (byOld.Slug ?? rawId, byOld.StateKey, byOld.Strategy, byOld.Degraded);
    }

    private bool IsKnownSlug(string id)
    {
        return resolver.KnownSlugs.Contains(id);
    }
}

/// <summary>把 C2 读取结果装配成 UI 可用的目录；计时用于 P2 性能报告（真实 Stopwatch，不编造）。</summary>
public static class WikiEnemyCatalog
{
    public static readonly Dictionary<string, string> LegacyCategoryNames = new(StringComparer.Ordinal)
    {
        ["Hilichurls"] = "丘丘人",
        ["OtherHumanFactions"] = "其他人形势力",
        ["NonCategorized"] = "未分类",
    };

    public static WikiEnemyCatalogResult Build(
        EnemyWebReadResult web,
        LauncherSnapshotReadResult? snapshot,
        IEnumerable<AliasRecord> aliasRecords,
        IEnumerable<string> allowedImageRefs,
        WikiImageCache imageCache,
        WikiDiagnosticsLog log,
        WikiSourceFeatureFlag flag,
        string? categoryAllText = "全部")
    {
        ArgumentNullException.ThrowIfNull(web);
        Stopwatch total = Stopwatch.StartNew();
        EnemyAdapter adapter = new(log, flag.Strict);
        Dictionary<string, string> oldIdBySlug = [];
        foreach (AliasRecord record in aliasRecords)
        {
            if (!string.IsNullOrEmpty(record.NewId))
            {
                oldIdBySlug.TryAdd(record.NewId, record.OldId);
            }
        }

        string curveSource = "曲线来源：ellen.wiki 快照（zip 内 MonsterCurve，Type 12）";
        bool curveMissing = snapshot is null || !snapshot.Curves.MonsterCurvePresentInZip;
        if (curveMissing)
        {
            curveSource = "曲线来源：内置兜底（显式）";
            log.Add("curve-fallback", web.Stamp, "zip 内无 MonsterCurve.json，UI 显式显示内置兜底", CurveContract.ValidateMissingReason);
        }

        List<WikiEnemyViewItem> items = [];
        int unknown = 0;
        int resistanceStates = 0;
        int imageRefs = 0;
        Stopwatch imageWatch = Stopwatch.StartNew();

        foreach ((string id, EnemyItemDto dto) in web.Items.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            WikiEnemy enemy = adapter.Adapt(dto, web.Index.Enemies.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal)), web.Stamp);

            bool baseUnknown = enemy.BaseValueUnknown;
            if (baseUnknown)
            {
                unknown++;
            }

            string baseText = baseUnknown
                ? "未知"
                : $"HP {enemy.BaseValue!.Hp:0.##} / ATK {enemy.BaseValue.Atk:0.##}";

            // 搜索 token：只收真实存在的字符串，不编造。
            List<string> tokens = [enemy.Name];
            AddToken(tokens, enemy.NameEn);
            AddToken(tokens, enemy.CategoryName);
            AddToken(tokens, LegacyCategoryNames.GetValueOrDefault(enemy.CategoryKey));
            AddToken(tokens, enemy.Subtitle);
            tokens.AddRange(enemy.Aliases);
            tokens.AddRange(enemy.Tags);
            if (oldIdBySlug.TryGetValue(id, out string? oldId))
            {
                AddToken(tokens, oldId);
            }

            string? evidence = enemy.ResistanceStates.FirstOrDefault(s => !string.IsNullOrEmpty(s.Evidence))?.Evidence;
            List<WikiImageRef> images = [];
            // 文件名 → 已解析 URI：技能图标本来就在 images 里（adapter 的 BuildImages 会带上），
            // 复用同一份结果，别二次 Resolve —— 那会把 ImageAttempts / BundledHits 凭空翻一倍。
            Dictionary<string, string> resolvedByFile = new(StringComparer.OrdinalIgnoreCase);
            foreach (WikiImageRef image in enemy.Images)
            {
                imageRefs++;
                string resolved = imageCache.Resolve(image);
                resolvedByFile[image.File] = resolved;
                images.Add(new WikiImageRef
                {
                    EnemyId = image.EnemyId,
                    File = image.File,
                    Uri = resolved,
                    CacheKey = image.CacheKey,
                    HashAvailable = image.HashAvailable,
                    ContentHash = image.ContentHash,
                });
            }

            /* 技能图标：复用 images 里已解析的结果（随包原图 → 内容缓存 → 在线），
               解析失败落在占位图时按「没有图标」处理，UI 不显示空白方框。 */
            List<WikiSkillView> skills = [];
            foreach (WikiSkill skill in enemy.Skills)
            {
                string skillIconUri = string.Empty;
                if (skill.Icon is { } skillRef)
                {
                    skillIconUri = resolvedByFile.TryGetValue(skillRef.File, out string? resolvedIcon)
                        ? resolvedIcon
                        : imageCache.Resolve(skillRef);
                    if (string.Equals(skillIconUri, WikiImageCache.PlaceholderUri, StringComparison.Ordinal))
                    {
                        skillIconUri = string.Empty;
                    }
                }

                skills.Add(new WikiSkillView
                {
                    Name = skill.Name,
                    Tag = skill.Tag,
                    DescriptionText = skill.DescriptionText,
                    IconUri = skillIconUri,
                });
            }

            resistanceStates += enemy.ResistanceStates.Count;

            /* 列表头像只认源里的 icon 文件（avatar.webp）。上游没给头像 → 留空，
               绝不拿 images[0]（= 一图流 reference*.png）顶替：那是详情大图，不是头像。 */
            string icon = string.IsNullOrEmpty(enemy.IconFile)
                ? string.Empty
                : images.FirstOrDefault(i =>
                        string.Equals(i.File, enemy.IconFile, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(i.Uri, WikiImageCache.PlaceholderUri, StringComparison.Ordinal))?.Uri
                    ?? string.Empty;
            WikiImageRef? artRef = images.FirstOrDefault(i =>
                    i.File.StartsWith("reference", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(i.Uri, WikiImageCache.PlaceholderUri, StringComparison.Ordinal))
                ?? images.FirstOrDefault(i =>
                    i.File.StartsWith("leyline", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(i.Uri, WikiImageCache.PlaceholderUri, StringComparison.Ordinal));
            string artUri = artRef?.Uri ?? string.Empty;
            bool artIsPreview = artUri.Contains("thumb-", StringComparison.OrdinalIgnoreCase);

            items.Add(new WikiEnemyViewItem
            {
                Id = enemy.Id,
                Name = enemy.Name,
                NameEn = enemy.NameEn,
                Subtitle = enemy.Subtitle,
                CategoryKey = enemy.CategoryKey,
                CategoryLabel = string.IsNullOrEmpty(enemy.CategoryName) ? enemy.CategoryKey : enemy.CategoryName,
                TierName = enemy.TierName,
                LegacyCategoryName = LegacyCategoryNames.GetValueOrDefault(enemy.CategoryKey),
                BaseValueText = baseText,
                BaseValueUnknown = baseUnknown,
                IconUri = icon,
                ArtUri = artUri,
                ArtIsPreview = artIsPreview,
                Aliases = enemy.Aliases,
                Tags = enemy.Tags,
                SearchTokens = [.. tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase)],
                ResistanceStates = enemy.ResistanceStates,
                Skills = skills,
                BaseValue = enemy.BaseValue,
                Notes = enemy.Notes
                    .Select(static note => new WikiNoteView
                    {
                        Title = note.Text,
                        Text = string.Concat(note.Description.Select(static span => span.Text)),
                    })
                    .ToList(),
                EnergyRows = enemy.Energy is null
                    ? []
                    : enemy.Energy.Rows
                        .Select(static row => new WikiEnergyRowView
                        {
                            HpText = $"{row.Hp}%",
                            ParticleText = row.Particle > 0 ? row.Particle.ToString() : string.Empty,
                            OrbText = row.Orb > 0 ? row.Orb.ToString() : string.Empty,
                        })
                        .ToList(),
                Images = images,
                LevelRows = enemy.LevelRows,
                EvidenceText = evidence,
                StateKey = "default",
                CurveSourceText = curveSource,
                Stamp = enemy.Stamp,
            });
        }

        imageWatch.Stop();
        total.Stop();

        // 分类分组：以网页 categoryLabel 为准，首项「全部」。
        List<WikiEnemyCategoryGroup> categories = [new() { Key = string.Empty, Name = categoryAllText ?? "全部", Count = items.Count }];
        foreach (IGrouping<string, WikiEnemyViewItem> group in items.GroupBy(i => i.CategoryKey))
        {
            categories.Add(new WikiEnemyCategoryGroup
            {
                Key = group.Key,
                Name = group.First().CategoryLabel,
                Count = group.Count(),
            });
        }

        // 真实计时：筛选一次全量 + 一次分类，供性能报告。
        Stopwatch filterWatch = Stopwatch.StartNew();
        _ = Filter(items, string.Empty, ["丘丘"]).Count;
        _ = Filter(items, categories.Count > 1 ? categories[1].Key : string.Empty, []).Count;
        filterWatch.Stop();

        string stampText = $"source={web.Stamp.Source} contract={web.Stamp.Contract} digest={snapshot?.Manifest.Digest ?? "-"} generatedAt={snapshot?.Manifest.GeneratedAt ?? "-"}";

        bool offlineBundled = string.Equals(web.Stamp.Contract, WikiSourceStamp.Bundled753, StringComparison.Ordinal);

        return new WikiEnemyCatalogResult
        {
            Items = items,
            Categories = categories,
            Diagnostics = log.Records,
            TotalCount = items.Count,
            UnknownBaseStatsCount = unknown,
            ResistanceStateCount = resistanceStates,
            ImageRefCount = imageRefs,
            OrphanSkippedCount = imageCache.OrphansSkipped,
            ImageCacheHits = imageCache.Hits,
            ImageCacheMisses = imageCache.Misses,
            BundledImageHits = imageCache.BundledHits,
            BundledThumbPreviews = imageCache.BundledThumbPreviews,
            CurveSourceText = curveSource,
            SourceStampText = stampText,
            UiSource = flag.UiSource,
            IsOfflineBundled = offlineBundled,
            LoadMilliseconds = total.Elapsed.TotalMilliseconds,
            FilterMilliseconds = filterWatch.Elapsed.TotalMilliseconds,
            ImageResolveMilliseconds = imageWatch.Elapsed.TotalMilliseconds,
        };
    }

    /// <summary>分类 + token 筛选；token 命中 SearchTokens（含 nameEn/aliases/tags/categoryLabel/旧分类/旧 ID）。</summary>
    public static IReadOnlyList<WikiEnemyViewItem> Filter(
        IEnumerable<WikiEnemyViewItem> items,
        string? categoryKey,
        IEnumerable<string> tokens)
    {
        string[] words = [.. tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim())];
        bool all = string.IsNullOrEmpty(categoryKey);
        return [.. items.Where(item =>
            (all || string.Equals(item.CategoryKey, categoryKey, StringComparison.Ordinal))
            && words.All(word => item.SearchTokens.Any(t => t.Contains(word, StringComparison.OrdinalIgnoreCase))))];
    }

    private static void AddToken(List<string> tokens, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tokens.Add(value);
        }
    }
}
