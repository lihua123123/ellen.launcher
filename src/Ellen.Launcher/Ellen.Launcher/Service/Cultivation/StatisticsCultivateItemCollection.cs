// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Domain.Cultivation;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.ViewModel.Cultivation;
using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.Cultivation;

public sealed partial class StatisticsCultivateItemCollection : ICollection<StatisticsCultivateItem>
{
    private readonly List<StatisticsCultivateItem> items = [];
    private readonly Dictionary<uint, StatisticsCultivateItem> itemsById;

    public StatisticsCultivateItemCollection(Dictionary<uint, StatisticsCultivateItem> items)
    {
        itemsById = items;
        foreach ((_, StatisticsCultivateItem item) in items)
        {
            this.items.Add(item);
        }

        this.items.Sort(StatisticsCultivateItemComparer.Shared);
    }

    public ulong AutoSynthesize(ImmutableDictionary<MaterialId, Combine> combineMap)
    {
        ImmutableArray<MaterialSynthesisStock>.Builder stocks = ImmutableArray.CreateBuilder<MaterialSynthesisStock>(itemsById.Count);
        foreach (StatisticsCultivateItem item in itemsById.Values)
        {
            stocks.Add(new(item.Inner.Id, item.Inner.Rank, item.Count, item.Current));
        }

        ImmutableArray<MaterialSynthesisRecipe>.Builder recipes = ImmutableArray.CreateBuilder<MaterialSynthesisRecipe>();
        foreach (Combine candidate in combineMap.Values)
        {
            if (candidate.RecipeType is RecipeType.RECIPE_TYPE_COMBINE && candidate.Materials.Length is 1)
            {
                // Prefer the result material's category; fall back to the ingredient's category so that
                // materials missing from the local metadata (e.g. newly added element gems) still synthesize.
                MaterialSynthesisCategory category = MapSynthesisCategory(GetTypeDescription(candidate.Result.Id))
                    ?? MapSynthesisCategory(GetTypeDescription(candidate.Materials[0].Id))
                    ?? MaterialSynthesisCategory.None;
                recipes.Add(new(
                    candidate.Result.Id,
                    candidate.Materials[0].Id,
                    candidate.Materials[0].Count,
                    candidate.Result.Count,
                    candidate.Cost,
                    category));
            }
        }

        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(stocks, recipes);
        foreach (StatisticsCultivateItem item in itemsById.Values)
        {
            if (preview.CurrentCounts.TryGetValue(item.Inner.Id, out uint currentCount))
            {
                item.Current = currentCount;
            }
        }

        if (preview.MoraCost > 0 && itemsById.TryGetValue(MaterialIds.Mora, out StatisticsCultivateItem? moraItem))
        {
            uint extraMora = preview.MoraCost > uint.MaxValue ? uint.MaxValue : (uint)preview.MoraCost;
            moraItem.Count = extraMora >= uint.MaxValue - moraItem.Count ? uint.MaxValue : moraItem.Count + extraMora;
        }

        return preview.MoraCost;
    }

    private string? GetTypeDescription(uint itemId)
    {
        return itemsById.TryGetValue(itemId, out StatisticsCultivateItem? item) ? item.Inner.TypeDescription : null;
    }

    private static MaterialSynthesisCategory? MapSynthesisCategory(string? typeDescription)
    {
        return typeDescription switch
        {
            "角色突破素材" => MaterialSynthesisCategory.ElementalCrystal,
            "角色天赋素材" => MaterialSynthesisCategory.CharacterTalent,
            "武器突破素材" => MaterialSynthesisCategory.WeaponAscension,
            "角色与武器培养素材" => MaterialSynthesisCategory.CharacterAndWeaponEnhancement,
            _ => null,
        };
    }

    public int Count
    {
        get => items.Count;
    }

    public bool IsReadOnly
    {
        get => ((ICollection<StatisticsCultivateItem>)items).IsReadOnly;
    }

    public void SortAsIncompleteFirst()
    {
        // .OrderBy(item => item.IsFinished)
        // .ThenByDescending(item => item.IsToday)
        // .ThenBy(item => item.Inner.Id, MaterialIdComparer.Shared)
        items.Sort(static (x, y) =>
        {
            int result = x.IsFinished.CompareTo(y.IsFinished);
            if (result is not 0)
            {
                return result;
            }

            result = y.IsToday.CompareTo(x.IsToday);
            if (result is not 0)
            {
                return result;
            }

            return MaterialIdComparer.Shared.Compare(x.Inner.Id, y.Inner.Id);
        });
    }

    public ObservableCollection<StatisticsCultivateItem> ToObservableCollection()
    {
        List<StatisticsCultivateItem> results = [];
        foreach (StatisticsCultivateItem item in items)
        {
            if (item.ExcludedFromPresentation)
            {
                continue;
            }

            results.Add(item);
        }

        return results.ToObservableCollection();
    }

    public IEnumerator<StatisticsCultivateItem> GetEnumerator()
    {
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)items).GetEnumerator();
    }

    public void Add(StatisticsCultivateItem item)
    {
        items.Add(item);
    }

    public void Clear()
    {
        items.Clear();
    }

    public bool Contains(StatisticsCultivateItem item)
    {
        return items.Contains(item);
    }

    public void CopyTo(StatisticsCultivateItem[] array, int arrayIndex)
    {
        items.CopyTo(array, arrayIndex);
    }

    public bool Remove(StatisticsCultivateItem item)
    {
        return items.Remove(item);
    }
}