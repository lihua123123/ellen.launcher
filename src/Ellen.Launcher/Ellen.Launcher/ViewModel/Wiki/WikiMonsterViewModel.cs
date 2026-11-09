// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Domain.WikiSource;
using Ellen.Launcher.Model.EnemyArchive;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.EnemyArchive;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.Web.Endpoint.Upstream;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Ellen.Launcher.ViewModel.Wiki;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class WikiMonsterViewModel : Abstraction.ViewModel
{
    private const uint MaxMonsterLevel = 110U;

    private readonly IMonsterCatalogService monsterCatalogService;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly WikiSourceFeatureFlag wikiSourceFlag;
    private readonly IWikiEnemyCatalogService wikiEnemyCatalogService;
    private readonly WikiSyncCoordinator wikiSync;
    private readonly WikiRolloutMetricsService rolloutMetrics;

    private WikiMonsterMetadataContext? metadataContext;
    private ImmutableArray<MonsterCatalogEntry> allMonsters = [];

    /// <summary>
    /// 证据等级（<c>EnemyArchives.json</c> 的 <c>evidence</c> 值）→ 胶囊的文案 / 底色 / 字色。
    /// <para>⚠️ 刷子键必须用**仓库里已在用的主题资源**：引用不存在的键会在渲染期抛
    /// <c>XamlParseException</c> 导致崩溃，而且编译期与 XamlStyleAudit 都查不出来。</para>
    /// <para>文案与配色的分工：「双读一致」是正常基线所以用中性色；两头的极端才用彩色 ——
    /// <c>cal-gold</c>（最强，人眼复核过）用绿、<c>single-read</c>（最弱，没走过第二通道）用红。
    /// 红绿必须分开，否则 <c>single-read</c> 会与 <c>2geo</c> 混在一起被忽略。</para>
    /// </summary>
    private static readonly Dictionary<string, (string Text, string Background, string Foreground)> EvidenceStyles = new()
    {
        // 该卡经人眼读图复核
        ["cal-gold"] = ("已复核", "SystemFillColorSuccessBrush", "TextOnAccentFillColorPrimaryBrush"),
        // 两套裁图几何各读一次且逐格一致；这是「正常基线」，所以用中性色
        ["2geo"] = ("双读一致", "SubtleFillColorSecondaryBrush", "TextFillColorSecondaryBrush"),
        // 两几何分歧，目视裁决
        ["eyeball"] = ("目视裁决", "SystemFillColorCautionBrush", "TextOnAccentFillColorPrimaryBrush"),
        // 没走过第二通道（**不是**「值有问题」）—— 用红而不是浅一点的中性色。
        ["single-read"] = ("未双读", "SystemFillColorCriticalBrush", "TextOnAccentFillColorPrimaryBrush"),
    };

    [GeneratedConstructor]
    public partial WikiMonsterViewModel(IServiceProvider serviceProvider);

    // ---------------- P2：ellen.wiki 753 条双视图 ----------------
    // 默认 IsWebMode=false，旧 722 路径与 XAML 完全不变；切 enemy-web-753 才显示新视图。

    [ObservableProperty]
    public partial bool IsWebMode { get; set; }

    public bool IsLegacyMode => !IsWebMode;

    [ObservableProperty]
    public partial IReadOnlyList<WikiEnemyViewItem>? WebMonsters { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<WikiEnemyCategoryGroup>? WebCategories { get; set; }

    [ObservableProperty]
    public partial WikiEnemyCategoryGroup? WebSelectedCategory { get; set; }

    [ObservableProperty]
    public partial WikiEnemyViewItem? WebCurrentItem { get; set; }

    [ObservableProperty]
    public partial SearchData? WebSearchData { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<WikiResistanceState>? WebResistanceStates { get; set; }

    [ObservableProperty]
    public partial int WebSelectedResistanceStateIndex { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<WikiResistanceCell>? WebResistanceCells { get; set; }

    /// <summary>多于一个抗性状态时才显示状态下拉；单状态（只有「正常」）露一个下拉是噪声。</summary>
    [ObservableProperty]
    public partial bool WebHasResistanceStates { get; set; }

    /// <summary>
    /// 当前条目的等级换算结果（拖动滑块查看该等级的基础生命值 / 基础攻击力）。
    /// 曲线倍率来自随包 <c>Resource/Metadata/.../MonsterCurve.json</c>；源里没有 baseStats 的条目为 null，UI 整块隐藏。
    /// </summary>
    [ObservableProperty]
    public partial BaseValueInfo? WebBaseValueInfo { get; set; }

    // ---------------- P5：首次断网显式回退 + 灰度诊断 ----------------

    /// <summary>显式回退横幅（无缓存且不可达 → 回 builtin 722）。空表示不显示。</summary>
    [ObservableProperty]
    public partial string? FallbackBannerText { get; set; }

    [ObservableProperty]
    public partial bool IsFallbackBannerVisible { get; set; }

    /// <summary>web 视图空态文案（首次同步中）；不白屏。</summary>
    [ObservableProperty]
    public partial string? WebEmptyStateText { get; set; }


    partial void OnIsWebModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLegacyMode));
    }

    partial void OnWebSelectedCategoryChanged(WikiEnemyCategoryGroup? value)
    {
        if (allWebItems.Count > 0)
        {
            ApplyWebFilter();
        }
    }

    partial void OnWebSelectedResistanceStateIndexChanged(int value)
    {
        UpdateWebResistanceCells();
    }

    public IAdvancedCollectionView<MonsterCatalogEntry>? Monsters
    {
        get;
        set
        {
            if (field is not null)
            {
                field.CurrentChanged -= OnCurrentMonsterChanged;
            }

            SetProperty(ref field, value);

            if (value is not null)
            {
                value.CurrentChanged += OnCurrentMonsterChanged;
            }
        }
    }

    /// <summary>
    /// 侧边栏一级：分类（首项「全部」固定在最前，其余按上游 <c>Category</c> 顺序）。
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<MonsterCategoryGroup> Categories { get; set; }

    /// <summary>
    /// 当前选中的分类；<see langword="null"/> 表示尚未初始化（等同「全部」）。
    /// </summary>
    [ObservableProperty]
    public partial MonsterCategoryGroup? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial BaseValueInfo? BaseValueInfo { get; set; }

    [ObservableProperty]
    public partial SearchData? SearchData { get; set; }

    /// <summary>
    /// 当前选中的抗性状态（0 = 常态，其余对应 <c>Archive.Resistances.States</c> 的下标 + 1）。
    /// </summary>
    [ObservableProperty]
    public partial int SelectedResistanceStateIndex { get; set; }

    /// <summary>
    /// 抗性状态的选项：首项固定为「常态」，其余与 <c>Archive.Resistances.States</c> 一一对应。
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<EnemyArchiveResistanceState> ResistanceStates { get; set; }

    /// <summary>
    /// 当前状态下展开好的八项抗性，界面直接绑定，避免在 XAML 里做条件判断。
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<EnemyArchiveResistanceCell> ResistanceCells { get; set; }

    [ObservableProperty]
    public partial string? ResistanceEvidenceText { get; set; }

    [ObservableProperty]
    public partial Brush? ResistanceEvidenceBackground { get; set; }

    [ObservableProperty]
    public partial Brush? ResistanceEvidenceForeground { get; set; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        // P2：feature flag 分流。默认 legacy-722-bundled，下面整段旧逻辑一字不改。
        IsWebMode = wikiSourceFlag.IsWebMode;
        OnPropertyChanged(nameof(IsLegacyMode));
        if (IsWebMode)
        {
            return await LoadWebAsync(token).ConfigureAwait(false);
        }

        return await LoadLegacyAsync(token).ConfigureAwait(false);
    }

    /// <summary>内置 722 视图的装配（默认路径，也是 web 无缓存断网时的显式回退目标）。</summary>
    private async ValueTask<bool> LoadLegacyAsync(CancellationToken token)
    {
        ImmutableArray<MonsterCatalogEntry> entries = await monsterCatalogService.GetEntriesAsync().ConfigureAwait(false);
        if (entries.IsDefaultOrEmpty)
        {
            return false;
        }

        allMonsters = entries;

        // 成长曲线表还是从元数据取（源里只有曲线名，没有逐级倍率）。
        if (await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            metadataContext = await metadataService.GetContextAsync<WikiMonsterMetadataContext>(token).ConfigureAwait(false);
        }

        ImmutableArray<MonsterCategoryGroup> categories = BuildCategories(entries);
        SearchData searchData = SearchData.CreateForWikiMonster(entries);

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            IAdvancedCollectionView<MonsterCatalogEntry> monstersView = entries.AsAdvancedCollectionView();

            await taskContext.SwitchToMainThreadAsync();
            SearchData = searchData;
            Categories = categories;
            SelectedCategory = categories[0];
            Monsters = monstersView;
            ApplyFilter();
            Monsters.MoveCurrentToFirst();
        }

        // G7a：legacy 侧也要留证据，否则「并存 / 回滚」两项永远判不出来。
        rolloutMetrics.RecordLegacyLoad(entries.Length);

        return true;
    }

    // ---------------- P2 web 分支 ----------------

    private async ValueTask<bool> LoadWebAsync(CancellationToken token, bool triggerSync = true)
    {
        WikiEnemyCatalogResult? catalog = await wikiEnemyCatalogService.LoadAsync(token).ConfigureAwait(false);
        if (catalog is null)
        {
            // 无可用缓存：先按「首次运行策略」给出明确结论（回 legacy / 同步中空态），再后台同步。
            // 任何分支都不允许返回空白页。
            WikiFirstRunDecision decision = WikiFirstRunPolicy.Decide(new WikiFirstRunInputs
            {
                WebEnabled = true,
                CachedItemCount = 0,

                // 只区分「本机有没有网络」；上游是否可达由后台同步的真实结果回答。
                // 有网 → 同步中空态；无网 → 立刻显式回退内置 722。
                Online = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(),
                FailureReason = wikiEnemyCatalogService.MirrorUnavailableReason,
            });
            rolloutMetrics.RecordDecision(decision);

            await taskContext.SwitchToMainThreadAsync();
            FallbackBannerText = decision.Banner;
            IsFallbackBannerVisible = decision.Mode == WikiFirstRunMode.LegacyFallback;

            if (decision.Mode == WikiFirstRunMode.LegacyFallback)
            {
                IsWebMode = false;
                bool legacyLoaded = await LoadLegacyAsync(token).ConfigureAwait(false);
                if (triggerSync)
                {
                    _ = SyncWebInBackgroundAsync(switchToWebOnSuccess: true);
                }

                return legacyLoaded;
            }

            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                await taskContext.SwitchToMainThreadAsync();
                WebEmptyStateText = decision.Banner ?? "正在同步…";
            }

            if (triggerSync)
            {
                _ = SyncWebInBackgroundAsync();
            }

            return true;
        }

        rolloutMetrics.RecordWebLoad(catalog.LoadMilliseconds, catalog.TotalCount, catalog.IsOfflineBundled);
        await EnsureWebCurveMapAsync(token).ConfigureAwait(false);

        using (await EnterCriticalSectionAsync().ConfigureAwait(false))
        {
            await taskContext.SwitchToMainThreadAsync();
            allWebItems = catalog.Items;
            WebMonsters = catalog.Items;
            WebCategories = catalog.Categories;
            WebSelectedCategory = catalog.Categories.FirstOrDefault();
            WebSearchData = SearchData.CreateForWikiMonsterWeb(catalog.Items);
            WebEmptyStateText = null;
            IsFallbackBannerVisible = false;
            FallbackBannerText = null;
            ApplyWebFilter();
            appliedCatalogIdentity = CatalogIdentity(catalog);
            RecordWebCatalogMetrics(catalog);
        }

        if (triggerSync)
        {
            _ = SyncWebInBackgroundAsync();
        }

        return true;
    }

    /// <summary>后台同步（不阻塞首屏）：完成后丢弃旧装配结果、用新缓存重建。</summary>
    private async Task SyncWebInBackgroundAsync(bool switchToWebOnSuccess = false)
    {
        try
        {
            // 同步结果只用于「有没有成功」；界面不再展示 source/digest 诊断，成功后统一重载目录。
            _ = await wikiSync.SyncNowAsync().ConfigureAwait(false);
            wikiEnemyCatalogService.Invalidate();
            WikiEnemyCatalogResult? catalog = await wikiEnemyCatalogService.LoadAsync().ConfigureAwait(false);

            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                await taskContext.SwitchToMainThreadAsync();
                if (catalog is not null)
                {
                    /* 关键：数据没变就**什么都别动**。
                       每次进页面都会跑一次后台同步，同步完把 ItemsSource 整个换成新 List，
                       列表会重新实现一遍、当前项也换成新实例 → 用户看到的就是「点了某个敌人突然闪一下」。
                       指纹相同（条目数 + 来源戳）说明这次同步没带回新数据，直接跳过 UI 重建，
                       顺带也不再记 WebLoad / 图片分母 —— 那是空装配，记了只会把 G6/G7b 的分母灌水。 */
                    string identity = CatalogIdentity(catalog);
                    SyncCompletions++;
                    if (!switchToWebOnSuccess && string.Equals(identity, appliedCatalogIdentity, StringComparison.Ordinal))
                    {
                        SyncSkippedNoChange++;
                        RecordNewImageFailures();
                        return;
                    }

                    rolloutMetrics.RecordWebLoad(catalog.LoadMilliseconds, catalog.TotalCount, catalog.IsOfflineBundled);

                    if (switchToWebOnSuccess)
                    {
                        // 首次启动无缓存但同步成功：从 builtin 回退切到 753 视图，并清掉横幅。
                        IsWebMode = true;
                        IsFallbackBannerVisible = false;
                        FallbackBannerText = null;
                    }

                    allWebItems = catalog.Items;
                    WebCategories = catalog.Categories;
                    WebEmptyStateText = null;
                    WebSearchData = SearchData.CreateForWikiMonsterWeb(catalog.Items);
                    ApplyWebFilter();
                    appliedCatalogIdentity = identity;
                    RecordWebCatalogMetrics(catalog);
                    RecordNewImageFailures();
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            rolloutMetrics.RecordSyncFailure();
            await taskContext.SwitchToMainThreadAsync();
            WebEmptyStateText = $"后台同步失败（显式）：{ex.Message}";
            if (IsFallbackBannerVisible)
            {
                FallbackBannerText = $"ellen.wiki 同步失败（{ex.Message}），继续显示内置 722 图鉴；恢复网络后点「立即同步」";
            }
            else if (!string.IsNullOrEmpty(WebEmptyStateText))
            {
                // 首次同步中失败：保留错误说明与重试入口，不留空白列表。
                WebEmptyStateText = $"同步失败：{ex.Message}（点「立即同步」重试，或检查网络）";
            }
        }
    }

    private int recordedImageFailures;
    private int recordedImageNetworkFailures;
    private int recordedImageDecodeFailures;
    private int recordedImageUiDownloads;
    private int recordedImageUiDownloadFailures;

    /// <summary>
    /// 图片失败按增量累计到灰度指标，避免每次刷新重复计数。
    /// 解析口径分子 = 下载失败 + hash 校验失败 + 同步异常 + 无 URL 占位 + UI 解码失败
    /// （前四类来自诊断日志，解码失败来自 CachedImage.ImageFailed，UI 下载失败来自 WikiImageDownloadCounter，
    /// 均仅 Wiki 图源），与 ImageAttempts（解析引用 + UI 下载尝试）同口径。
    /// 网络口径分子单列，= image-download-failure + UI 下载失败，避免与解析口径混算。
    /// </summary>
    private void RecordNewImageFailures()
    {
        int total = 0;
        int networkTotal = 0;
        foreach (WikiDiagnostic diagnostic in wikiEnemyCatalogService.Diagnostics)
        {
            switch (diagnostic.Kind)
            {
                case "image-download-failure":
                    total++;
                    networkTotal++;
                    break;
                case "image-hash-mismatch":
                case "image-sync-failure":
                case "image-fallback":
                    total++;
                    break;
            }
        }

        int delta = total - recordedImageFailures;
        if (delta > 0)
        {
            recordedImageFailures = total;
        }

        // UI 层解码失败（仅敌人图鉴图源）：单独游标，与诊断增量相加后一次性记账。
        int decodeTotal = Service.EnemyArchive.WikiImageDecodeCounter.Count;
        int decodeDelta = decodeTotal - recordedImageDecodeFailures;
        if (decodeDelta > 0)
        {
            recordedImageDecodeFailures = decodeTotal;
        }

        /* UI 层真实下载（ImageCache 管线）：成功/失败各自增量记账。
           失败同时计入 ImageFailures 与 ImageNetworkFailures —— 这条链路以前完全不入账
           （CachedImage catch 后降级占位图，Sentry 默认未启用），G7b 的「图片失败率」因此在盲区里。
           顺序有讲究：必须先把失败并进 networkTotal，再统一算 networkDelta；
           先前写成「先 RecordImageNetworkFailures、后 networkTotal += uiFailureDelta」，
           那句 += 是死代码，UI 下载失败其实没进网络口径。 */
        int uiDownloadTotal = Service.EnemyArchive.WikiImageDownloadCounter.Successes;
        int uiDownloadDelta = uiDownloadTotal - recordedImageUiDownloads;
        if (uiDownloadDelta > 0)
        {
            recordedImageUiDownloads = uiDownloadTotal;
        }

        int uiDownloadFailureTotal = Service.EnemyArchive.WikiImageDownloadCounter.Failures;
        int uiDownloadFailureDelta = uiDownloadFailureTotal - recordedImageUiDownloadFailures;
        if (uiDownloadFailureDelta > 0)
        {
            recordedImageUiDownloadFailures = uiDownloadFailureTotal;
            // 同一网络口径：解析层下载失败 + UI 层下载失败，都算「真实下载失败」。
            networkTotal += uiDownloadFailureDelta;
        }

        // 网络口径分子在这之后统一落账（含 UI 下载失败）。
        int networkDelta = networkTotal - recordedImageNetworkFailures;
        if (networkDelta > 0)
        {
            recordedImageNetworkFailures = networkTotal;
            rolloutMetrics.RecordImageNetworkFailures(networkDelta);
        }

        /* 一次调用同时更新：ImageUiDownloads / ImageUiDownloadFailures / ImageAttempts（分母扩容）。 */
        rolloutMetrics.RecordImageUiDownloads(Math.Max(0, uiDownloadDelta), Math.Max(0, uiDownloadFailureDelta));

        int add = Math.Max(0, delta) + Math.Max(0, decodeDelta) + Math.Max(0, uiDownloadFailureDelta);
        if (add > 0)
        {
            rolloutMetrics.RecordImageFailures(add);
        }
    }

    /// <summary>
    /// 目录装配后的指标记账（图片分母 / 随包命中 / M1 打点）。
    /// 界面不再展示诊断卡片，但灰度 gate 依赖这些计数，别连同 UI 一起删掉。
    /// </summary>
    private void RecordWebCatalogMetrics(WikiEnemyCatalogResult catalog)
    {
        int images = catalog.ImageCacheHits + catalog.ImageCacheMisses;
        // 图片失败率分母：本次装配实际解析的图片引用数（命中+未命中），跨装配累计。
        rolloutMetrics.RecordImageAttempts(images);
        // 网络口径分母：只有 miss 才真正发起下载；单列避免「缓存命中稀释网络失败率」的争议。
        rolloutMetrics.RecordImageNetworkAttempts(catalog.ImageCacheMisses);
        // P6 随包口径：随包原图命中 / 离线缩略图预览分开记账，用于真机取证（不在 metrics 里就说不清）。
        rolloutMetrics.RecordBundledImageStats(catalog.BundledImageHits, catalog.BundledThumbPreviews);
        // M1（web 视图）：StartupProbe 开启时记录「753 web 列表就绪」时间点；未开启时为 no-op。
        StartupProbe.Mark("enemy-web-loaded");
    }

    // ---------------- 等级换算（拖动滑块看具体血量） ----------------
    // 源里给的是「基础值 + 曲线名」，逐级倍率只能从随包 MonsterCurve.json 取。

    /// <summary>Level → 各曲线倍率。只读一次；缺文件时保持 null，滑块整块隐藏而不是崩列表。</summary>
    private ImmutableDictionary<Level, TypeValueCollection<GrowCurveType, float>>? webCurveMap;

    private ImmutableDictionary<int, TypeValueCollection<GrowCurveType, float>>? webCurveByLevel;

    private async ValueTask EnsureWebCurveMapAsync(CancellationToken token)
    {
        if (webCurveMap is not null)
        {
            return;
        }

        try
        {
            if (await metadataService.InitializeAsync().ConfigureAwait(false))
            {
                webCurveMap = await metadataService.GetLevelToMonsterCurveMapAsync(token).ConfigureAwait(false);
                webCurveByLevel = webCurveMap.ToImmutableDictionary(static p => Convert.ToInt32(p.Key.Value), static p => p.Value);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 元数据缺失只影响等级换算，不能让 753 列表打不开。
            webCurveMap = null;
            webCurveByLevel = null;
        }
    }

    partial void OnWebCurrentItemChanged(WikiEnemyViewItem? value)
    {
        UpdateWebBaseValueInfo(value);
    }

    /// <summary>
    /// 按当前条目重建等级滑块：数值 = 基础值 × 曲线倍率 × 该条目的实战系数。
    /// <para>系数由源里 levelRows 的参考行反解（一部分敌人带 4x / 0.5x 之类的实战倍率），
    /// 这样滑块停在参考等级时与源数据完全一致；没有参考行时系数记为 1。</para>
    /// </summary>
    private void UpdateWebBaseValueInfo(WikiEnemyViewItem? item)
    {
        if (item?.BaseValue is not { Hp: > 0 } baseValue || webCurveByLevel is not { Count: > 0 })
        {
            WebBaseValueInfo = null;
            return;
        }

        GrowCurveType hpCurve = ResolveHpCurve(baseValue.HpCurve);
        double hpScale = ResolveScale(baseValue.Hp, item.LevelRows, hpCurve, static row => row.Hp);
        double atkScale = baseValue.Atk > 0
            ? ResolveScale(baseValue.Atk, item.LevelRows, GrowCurveType.GROW_CURVE_ATTACK, static row => row.Atk)
            : 0;

        List<PropertyCurveValue> props =
        [
            new(FightProperty.FIGHT_PROP_BASE_HP, hpCurve, (float)(baseValue.Hp * hpScale)),
        ];

        if (atkScale > 0)
        {
            props.Add(new PropertyCurveValue(FightProperty.FIGHT_PROP_BASE_ATTACK, GrowCurveType.GROW_CURVE_ATTACK, (float)(baseValue.Atk * atkScale)));
        }

        // 防御力不展示：源里 def 只在 Lv.1 有意义（实战 def = 500 × 曲线），对玩家判断没帮助，2026-11-09 按要求整项去掉。

        BaseValueInfoMetadataContext context = new()
        {
            GrowCurveMap = webCurveMap!,
            PromoteMap = default,
        };

        BaseValueInfo info = new(MaxMonsterLevel, [.. props], context);

        // 默认停在源里给出的参考等级（通常是 100）。CurrentLevel 与构造时的 MaxLevel 相同不会触发刷新，
        // 所以 Refresh() 兜底一次，避免「滑块有等级、数值区是空的」。
        int referenceLevel = item.LevelRows.FirstOrDefault(static row => row.Level > 0)?.Level ?? (int)MaxMonsterLevel;
        info.CurrentLevel = (uint)Math.Clamp(referenceLevel, 1, (int)MaxMonsterLevel);
        info.Refresh();

        WebBaseValueInfo = info;
    }

    /// <summary>
    /// ellen.wiki 曲线名 → 游戏曲线类型。映射是对 753 条实测反推的（不是猜）：
    /// HP_1→HP(1) 147/156、HP_2→HP_2(23) 383/424、HP_3→HP_ENVIRONMENT(25) 22/23；
    /// ATK_1 / ATK_2 都落到 ATTACK(2)。未识别的名字用标准 HP 曲线，系数会把参考等级拉回原值。
    /// </summary>
    private static GrowCurveType ResolveHpCurve(string? curveName) => curveName switch
    {
        "HP_2" => GrowCurveType.GROW_CURVE_HP_2,
        "HP_3" => GrowCurveType.GROW_CURVE_HP_ENVIRONMENT,
        "HP_0" => GrowCurveType.GROW_CURVE_HP_LITTLEMONSTER,
        _ => GrowCurveType.GROW_CURVE_HP,
    };

    /// <summary>用 levelRows 的参考行反解「实战系数」：observed / (基础值 × 该等级曲线倍率)。</summary>
    private double ResolveScale(double baseValue, IReadOnlyList<WikiLevelRow> rows, GrowCurveType type, Func<WikiLevelRow, double?> select)
    {
        WikiLevelRow? reference = rows.FirstOrDefault(static row => row.Level > 0);
        if (reference is null || select(reference) is not > 0 || baseValue <= 0)
        {
            return 1.0;
        }

        if (!webCurveByLevel.TryGetValue(reference.Level, out TypeValueCollection<GrowCurveType, float>? curve))
        {
            return 1.0;
        }

        float multiplier = curve.GetValueOrDefault(type);
        return multiplier > 0 ? select(reference)!.Value / (baseValue * multiplier) : 1.0;
    }

    [Command("WebSyncCommand")]
    private void SyncWeb()
    {
        _ = SyncWebInBackgroundAsync();
    }

    /// <summary>
    /// P5b「一键回滚」/ 切换数据源：**生产代码里唯一写持久化 UiSource 的入口**。
    /// <para>为什么必须有它：G7a 的「切换重启保持」要证明「用户手动选过 753，重启后还在」。
    /// 如果没有任何 UI 调 <see cref="WikiSourceFeatureFlag.Switch"/>，<c>persistedUiSource</c>
    /// 永远为空，探针只能看到灰度放量，四项结论里这一项永远为 false —— G7a 不可能达标。</para>
    /// </summary>
    [Command("SwitchToWebCommand")]
    private void SwitchToWeb()
    {
        SwitchSource(WikiSourceFeatureFlag.EnemyWeb753);
    }

    /// <summary>回滚到随包 722。与切换共用一条路径，保证两者写的是同一个持久化键。</summary>
    [Command("RollbackToLegacyCommand")]
    private void RollbackToLegacy()
    {
        SwitchSource(WikiSourceFeatureFlag.Legacy722Bundled);
    }

    private void SwitchSource(string source)
    {
        if (!wikiSourceFlag.Switch(source))
        {
            return;
        }

        // LoadCommand 的 initializing 闸门在 finally 里复位，可以重复执行。
        LoadCommand.Execute(null);
    }

    [Command("WebFilterCommand")]
    private void ApplyWebFilter()
    {
        if (WebMonsters is null)
        {
            return;
        }
        if (allWebItems.Count == 0)
        {
            return;
        }

        IReadOnlyList<string> tokens = WebSearchData?.FilterTokens.Select(static t => t.Value).ToArray() ?? [];
        string? category = WebSelectedCategory?.Key;
        IReadOnlyList<WikiEnemyViewItem> filtered = WikiEnemyCatalog.Filter(allWebItems, string.IsNullOrEmpty(category) ? null : category, tokens);
        WebMonsters = filtered;
        CatalogRebuilds++;
        // 当前项若已被筛掉，回落到第一条；否则保持不变（避免每次筛选都跳回首项）。
        WebCurrentItem = filtered.Count == 0
            ? null
            : filtered.FirstOrDefault(i => string.Equals(i.Id, WebCurrentItem?.Id, StringComparison.Ordinal)) ?? filtered[0];
        UpdateWebResistanceStates();
        UpdateWebResistanceCells();
    }

    private IReadOnlyList<WikiEnemyViewItem> allWebItems = [];

    /// <summary>
    /// 当前已经画到界面上的目录指纹（条目数 + 来源戳）。后台同步回来先比它：
    /// 一样就说明没新数据，不重建列表 —— 重建会让 ListView 重实现、当前项换实例，表现为闪一下。
    /// </summary>
    private string appliedCatalogIdentity = string.Empty;

    /// <summary>诊断：列表被重建的次数（1 = 只有首屏那次）。给取证脚本确认后台同步没再动 UI。</summary>
    public int CatalogRebuilds { get; private set; }

    /// <summary>诊断：后台同步成功拿到目录的次数。</summary>
    public int SyncCompletions { get; private set; }

    /// <summary>诊断：其中因「指纹没变」被跳过、没动 UI 的次数。</summary>
    public int SyncSkippedNoChange { get; private set; }

    private static string CatalogIdentity(WikiEnemyCatalogResult catalog)
        => $"{catalog.TotalCount}|{catalog.SourceStampText}";

    private void UpdateWebResistanceStates()
    {
        WebResistanceStates = WebCurrentItem?.ResistanceStates ?? [];
        WebHasResistanceStates = WebResistanceStates.Count > 1;
        WebSelectedResistanceStateIndex = 0;
    }

    private void UpdateWebResistanceCells()
    {
        IReadOnlyList<WikiResistanceState>? states = WebResistanceStates;
        if (states is null || states.Count == 0)
        {
            WebResistanceCells = [];
            return;
        }

        int index = WebSelectedResistanceStateIndex;
        if (index < 0 || index >= states.Count)
        {
            index = 0;
        }

        WebResistanceCells = states[index].Cells;
    }

    /// <summary>
    /// 按「分类首次出现的顺序」建组，首项是「全部」。
    /// <para>条目在 ETL 里已经按分类顺序排过一次，这里只做聚合，不再排序。</para>
    /// </summary>
    private static ImmutableArray<MonsterCategoryGroup> BuildCategories(ImmutableArray<MonsterCatalogEntry> entries)
    {
        ImmutableArray<MonsterCategoryGroup>.Builder builder = ImmutableArray.CreateBuilder<MonsterCategoryGroup>();
        builder.Add(new MonsterCategoryGroup
        {
            Key = string.Empty,
            Name = SH.ViewPageWikiMonsterCategoryAll,
            Count = entries.Length,
        });

        foreach (IGrouping<string, MonsterCatalogEntry> group in entries
            .Where(static e => !string.IsNullOrEmpty(e.Category))
            .GroupBy(static e => e.Category!))
        {
            MonsterCatalogEntry first = group.First();
            builder.Add(new MonsterCategoryGroup
            {
                Key = group.Key,
                Name = first.CategoryName ?? group.Key,
                Count = group.Count(),
                Monsters = [.. group],
            });
        }

        return builder.ToImmutable();
    }

    private void OnCurrentMonsterChanged(object? sender, object? e)
    {
        UpdateBaseValueInfo(Monsters?.CurrentItem);
        SelectedResistanceStateIndex = 0;
        UpdateResistanceStates();
        UpdateResistanceCells();
    }

    /// <summary>
    /// 切换分类后重设过滤条件（搜索 token 仍然生效）。
    /// </summary>
    partial void OnSelectedCategoryChanged(MonsterCategoryGroup? value)
    {
        if (Monsters is not null)
        {
            ApplyFilter();
            Monsters.MoveCurrentToFirst();
        }
    }

    partial void OnSelectedResistanceStateIndexChanged(int value)
    {
        if (value >= 0)
        {
            UpdateResistanceCells();
        }
    }

    private void UpdateResistanceStates()
    {
        ImmutableArray<EnemyArchiveResistanceState> states = Monsters?.CurrentItem?.Archive?.Resistances?.States ?? [];
        ResistanceStates = states.IsDefaultOrEmpty
            ? []
            : [new() { Name = SH.ViewPageWikiMonsterArchiveResistanceDefault }, .. states];
    }

    private void UpdateResistanceCells()
    {
        EnemyArchiveResistances? resistances = Monsters?.CurrentItem?.Archive?.Resistances;
        if (resistances?.Values is null)
        {
            ResistanceCells = [];
            UpdateResistanceEvidence(null);
            return;
        }

        EnemyArchiveResistanceValues values = resistances.Values;
        string? evidence = resistances.Evidence;
        if (SelectedResistanceStateIndex > 0 && SelectedResistanceStateIndex <= resistances.States.Length)
        {
            EnemyArchiveResistanceState state = resistances.States[SelectedResistanceStateIndex - 1];
            values = state.Values ?? values;
            evidence = state.Evidence;
        }

        ResistanceCells = values.ToCells();
        UpdateResistanceEvidence(evidence);
    }

    private void UpdateResistanceEvidence(string? level)
    {
        if (level is null || !EvidenceStyles.TryGetValue(level, out (string Text, string Background, string Foreground) style))
        {
            ResistanceEvidenceText = null;
            ResistanceEvidenceBackground = null;
            ResistanceEvidenceForeground = null;
            return;
        }

        ResistanceEvidenceText = style.Text;
        ResistanceEvidenceBackground = ThemeBrush(style.Background);
        ResistanceEvidenceForeground = ThemeBrush(style.Foreground);
    }

    private static Brush? ThemeBrush(string key)
    {
        return Application.Current.Resources.TryGetValue(key, out object? value) ? value as Brush : null;
    }

    [Command("FilterCommand")]
    private void ApplyFilter()
    {
        if (Monsters is null)
        {
            return;
        }

        Predicate<MonsterCatalogEntry>? search = MonsterFilter.Compile(SearchData);

        // 有关键词时跨越分类搜索（否则在「地方传奇」里搜不到幽境危战那一条，
        // 用户还得先自己切分类，反而更难找）。
        bool searchActive = SearchData is { FilterTokens.Count: > 0 };
        string? category = searchActive ? null : SelectedCategory?.Key;
        bool all = string.IsNullOrEmpty(category);

        Monsters.Filter = all && search is null
            ? null
            : entry => (all || entry.Category == category) && (search?.Invoke(entry) ?? true);

        if (Monsters.CurrentItem is null)
        {
            Monsters.MoveCurrentToFirst();
        }
    }

    private void UpdateBaseValueInfo(MonsterCatalogEntry? entry)
    {
        if (entry is not { GrowCurves: { } curves, BaseValue: { } baseValue })
        {
            BaseValueInfo = null;
            return;
        }

        BaseValueInfoMetadataContext context = new()
        {
            GrowCurveMap = metadataContext?.LevelDictionaryMonsterGrowCurveMap ?? default,
            PromoteMap = default,
        };

        BaseValueInfo = new(MaxMonsterLevel, curves.ToPropertyCurveValues(baseValue), context);
    }
}
