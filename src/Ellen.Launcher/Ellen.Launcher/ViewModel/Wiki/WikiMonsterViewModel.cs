// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Model.EnemyArchive;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Service.EnemyArchive;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;
using Ellen.Launcher.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Wiki;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class WikiMonsterViewModel : Abstraction.ViewModel
{
    private const uint MaxMonsterLevel = 110U;

    private readonly IMonsterCatalogService monsterCatalogService;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;

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

        return true;
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
