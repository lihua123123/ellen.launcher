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
            return;
        }

        EnemyArchiveResistanceValues values = resistances.Values;
        if (SelectedResistanceStateIndex > 0 && SelectedResistanceStateIndex <= resistances.States.Length)
        {
            values = resistances.States[SelectedResistanceStateIndex - 1].Values ?? values;
        }

        ResistanceCells = values.ToCells();
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