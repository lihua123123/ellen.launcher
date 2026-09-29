// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Model.EnemyArchive;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Metadata.Monster;
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
    private readonly IMetadataService metadataService;
    private readonly IEnemyArchiveService enemyArchiveService;
    private readonly ITaskContext taskContext;

    private WikiMonsterMetadataContext? metadataContext;

    /// <summary>
    /// 证据等级（<c>EnemyArchives.json</c> 的 <c>evidence</c> 值）→ 胶囊的文案 / 底色 / 字色。
    /// <para>⚠️ 刷子键必须用**仓库里已在用的主题资源**：引用不存在的键会在渲染期抛
    /// <c>XamlParseException</c> 导致崩溃，而且编译期与 XamlStyleAudit 都查不出来。</para>
    /// <para>文案与配色的分工：「双读一致」是正常基线所以用中性色；两头的极端才用彩色 ——
    /// <c>cal-gold</c>（最强，人眼复核过）用绿、<c>single-read</c>（最弱，没走过第二通道）用红。
    /// 红绿必须分开，否则 <c>single-read</c> 会与 <c>2geo</c> 混在一起被忽略。</para>
    /// <para>⚠️ <c>single-read</c> 的文案是「<b>未双读</b>」而不是「单读」：这一档的成因是
    /// <b>通道能力边界</b>而非值可疑 —— 卡片第 4 行以后没有对应的单行横条
    /// （<c>extract_fast.MAX_ROWS = 3</c>），所以拿不到第二读。
    /// 文案必须让人一眼看出「这是没比过」，而不是「这行有问题」。</para>
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
        // 已知成因：① 卡上行位 >= 4，没有对应横条；② 两几何分歧但未目视裁决。
        ["single-read"] = ("未双读", "SystemFillColorCriticalBrush", "TextOnAccentFillColorPrimaryBrush"),
    };

    [GeneratedConstructor]
    public partial WikiMonsterViewModel(IServiceProvider serviceProvider);

    public IAdvancedCollectionView<Monster>? Monsters
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
    /// <para>只有多套抗性的敌人才会填充，界面据此决定是否显示切换控件。</para>
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<EnemyArchiveResistanceState> ResistanceStates { get; set; }

    /// <summary>
    /// 当前状态下展开好的八项抗性，界面直接绑定，避免在 XAML 里做条件判断。
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<EnemyArchiveResistanceCell> ResistanceCells { get; set; }

    /// <summary>
    /// 当前选中抗性行的读值证据等级**文案**（如「已复核」）；<see langword="null"/> = 未评级，
    /// 界面据此隐藏胶囊。
    /// <para>⚠️ 「未评级」不等于「不可信」：它只表示这行**没走 VLM 第二通道**
    /// （即来自本地通道的高置信行——校准批实测那批 48/48 = 100% 正确）。
    /// 界面上「无胶囊」不应被理解为告警。</para>
    /// </summary>
    [ObservableProperty]
    public partial string? ResistanceEvidenceText { get; set; }

    /// <summary>胶囊底色；由 <see cref="EvidenceStyles"/> 映射而来，取不到时为 <see langword="null"/>。</summary>
    [ObservableProperty]
    public partial Brush? ResistanceEvidenceBackground { get; set; }

    /// <summary>胶囊字色；与底色成对，避免出现低对比度组合。</summary>
    [ObservableProperty]
    public partial Brush? ResistanceEvidenceForeground { get; set; }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        if (await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            try
            {
                metadataContext = await metadataService.GetContextAsync<WikiMonsterMetadataContext>(token).ConfigureAwait(false);

                foreach (Monster monster in metadataContext.Monsters)
                {
                    monster.DropsView ??= monster.Drops.EmptyIfDefault().SelectAsArray(static (i, context) => context.IdDisplayItemAndMaterialMap.GetValueOrDefault(i, Material.Default), metadataContext);
                }

                List<Monster> ordered = [.. metadataContext.Monsters.OrderBy(m => m.DescribeId.Value)];
                SearchData searchData = SearchData.CreateForWikiMonster([.. ordered]);

                ImmutableArray<EnemyArchiveEntry> archives = await enemyArchiveService.GetEntriesAsync().ConfigureAwait(false);
                if (!archives.IsDefaultOrEmpty)
                {
                    foreach (Monster monster in ordered)
                    {
                        monster.Archive = archives.FirstOrDefault(a => a.Matches(monster.MonsterName, monster.Name, monster.Title));
                    }
                }

                using (await EnterCriticalSectionAsync().ConfigureAwait(false))
                {
                    IAdvancedCollectionView<Monster> monstersView = ordered.AsAdvancedCollectionView();

                    await taskContext.SwitchToMainThreadAsync();
                    SearchData = searchData;
                    Monsters = monstersView;
                    Monsters.MoveCurrentToFirst();
                }

                return true;
            }
            catch (OperationCanceledException)
            {
            }
        }

        return false;
    }

    private void OnCurrentMonsterChanged(object? sender, object? e)
    {
        UpdateBaseValueInfo(Monsters?.CurrentItem);
        SelectedResistanceStateIndex = 0;
        UpdateResistanceStates();
        UpdateResistanceCells();
    }

    /// <summary>
    /// 切换抗性状态后重新展开单元格。
    /// </summary>
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

    /// <summary>
    /// 把证据等级展开成胶囊的三项展示数据（文案 + 底色 + 字色）；未评级时三项置空。
    /// 跟 <see cref="UpdateResistanceCells"/> 同步（含常态行与各状态行），
    /// 所以切换状态时胶囊会跟着换。
    /// </summary>
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

    /// <summary>取主题刷子；键不存在时返回 <see langword="null"/>（宁可不显色，也不因缺键崩溃）。</summary>
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

        Monsters.Filter = MonsterFilter.Compile(SearchData);

        if (Monsters.CurrentItem is null)
        {
            Monsters.MoveCurrentToFirst();
        }
    }

    private void UpdateBaseValueInfo(Monster? monster)
    {
        if (metadataContext is null || monster is not { GrowCurves: not null, BaseValue: not null })
        {
            BaseValueInfo = null;
            return;
        }

        BaseValueInfoMetadataContext context = new()
        {
            GrowCurveMap = metadataContext.LevelDictionaryMonsterGrowCurveMap,
            PromoteMap = default,
        };

        BaseValueInfo = new(Monster.MaxLevel, monster.GrowCurves.ToPropertyCurveValues(monster.BaseValue), context);
    }
}