using System;
using System.Collections.Generic;
using System.Linq;

namespace Ellen.Launcher.Domain.Cultivation;

public enum MaterialSynthesisCategory : byte
{
    None = 0,
    ElementalCrystal,
    CharacterTalent,
    WeaponAscension,
    CharacterAndWeaponEnhancement,
}

public readonly record struct MaterialSynthesisStock(
    uint ItemId,
    uint SeriesId,
    uint RequiredCount,
    uint CurrentCount);

public readonly record struct MaterialSynthesisRecipe(
    uint ResultId,
    uint MaterialId,
    uint MaterialCount,
    uint ResultCount,
    uint MoraCost,
    MaterialSynthesisCategory Category);

public sealed class MaterialSynthesisPreview
{
    internal MaterialSynthesisPreview(Dictionary<uint, uint> currentCounts, ulong moraCost)
    {
        CurrentCounts = currentCounts;
        MoraCost = moraCost;
    }

    public IReadOnlyDictionary<uint, uint> CurrentCounts { get; }

    public ulong MoraCost { get; }
}

public static class MaterialSynthesisPlanner
{
    public static MaterialSynthesisPreview CreatePreview(
        IEnumerable<MaterialSynthesisStock> stocks,
        IEnumerable<MaterialSynthesisRecipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(stocks);
        ArgumentNullException.ThrowIfNull(recipes);

        Dictionary<uint, StockState> stockById = [];
        foreach (MaterialSynthesisStock stock in stocks)
        {
            stockById[stock.ItemId] = new(stock);
        }

        Dictionary<uint, MaterialSynthesisRecipe> candidatesByResult = [];
        HashSet<uint> ambiguousResults = [];
        foreach (MaterialSynthesisRecipe recipe in recipes)
        {
            // An ambiguous result is safer to leave unchanged than to synthesize using an arbitrary recipe.
            if (ambiguousResults.Contains(recipe.ResultId))
            {
                continue;
            }

            if (!candidatesByResult.TryAdd(recipe.ResultId, recipe))
            {
                candidatesByResult.Remove(recipe.ResultId);
                ambiguousResults.Add(recipe.ResultId);
            }
        }

        Dictionary<uint, MaterialSynthesisRecipe> validRecipes = [];
        foreach ((uint resultId, MaterialSynthesisRecipe recipe) in candidatesByResult)
        {
            if (!IsStructurallyValid(recipe, stockById)
                || recipe.Category is MaterialSynthesisCategory.None
                || !TryGetSynthesisDepth(recipe.ResultId, recipe.Category, candidatesByResult, stockById, [], out int depth)
                || !IsMoraCostValid(recipe.Category, depth, recipe.MoraCost))
            {
                continue;
            }

            validRecipes[resultId] = recipe;
        }

        ulong totalMora = 0;
        HashSet<uint> recursionPath = [];
        foreach (StockState target in stockById.Values
            .OrderByDescending(item => GetSynthesisDepth(item.Stock.ItemId, validRecipes, stockById))
            .ThenBy(static item => item.Stock.ItemId))
        {
            if (target.CurrentCount < target.Stock.RequiredCount
                && TryGetRecipe(target.Stock.ItemId, validRecipes, stockById, out MaterialSynthesisRecipe recipe, out StockState ingredient))
            {
                ulong shortage = target.Stock.RequiredCount - target.CurrentCount;
                ulong batchesNeeded = DivideRoundUp(shortage, recipe.ResultCount);
                batchesNeeded = Math.Min(batchesNeeded, uint.MaxValue - target.CurrentCount);

                ulong availableIngredient = EnsureFreeIngredients(
                    ingredient,
                    batchesNeeded * recipe.MaterialCount,
                    validRecipes,
                    stockById,
                    recursionPath,
                    ref totalMora);
                ulong batches = Math.Min(batchesNeeded, availableIngredient / recipe.MaterialCount);
                if (batches > 0)
                {
                    ingredient.CurrentCount -= (uint)(batches * recipe.MaterialCount);
                    target.CurrentCount += (uint)(batches * recipe.ResultCount);
                    totalMora = AddMoraCost(totalMora, batches, recipe.MoraCost);
                }
            }
        }

        Dictionary<uint, uint> results = [];
        foreach ((uint itemId, StockState stock) in stockById)
        {
            results[itemId] = stock.CurrentCount;
        }

        return new(results, totalMora);
    }

    private static bool IsStructurallyValid(
        MaterialSynthesisRecipe recipe,
        Dictionary<uint, StockState> stocks)
    {
        return recipe.ResultCount is 1
            && recipe.MaterialCount is 3
            && recipe.MoraCost > 0
            && stocks.TryGetValue(recipe.ResultId, out StockState? result)
            && stocks.TryGetValue(recipe.MaterialId, out StockState? material)
            && (result.Stock.SeriesId is 0
                || material.Stock.SeriesId is 0
                || result.Stock.SeriesId == material.Stock.SeriesId);
    }

    private static bool TryGetSynthesisDepth(
        uint resultId,
        MaterialSynthesisCategory expectedCategory,
        Dictionary<uint, MaterialSynthesisRecipe> recipes,
        Dictionary<uint, StockState> stocks,
        HashSet<uint> recursionPath,
        out int depth)
    {
        depth = 0;
        if (!recipes.TryGetValue(resultId, out MaterialSynthesisRecipe recipe)
            || !IsStructurallyValid(recipe, stocks)
            || recipe.Category != expectedCategory
            || !recursionPath.Add(resultId))
        {
            return false;
        }

        try
        {
            depth = 1;
            if (recipes.ContainsKey(recipe.MaterialId))
            {
                if (!TryGetSynthesisDepth(recipe.MaterialId, expectedCategory, recipes, stocks, recursionPath, out int sourceDepth))
                {
                    depth = 0;
                    return false;
                }

                depth += sourceDepth;
            }

            return true;
        }
        finally
        {
            recursionPath.Remove(resultId);
        }
    }

    private static ulong EnsureFreeIngredients(
        StockState item,
        ulong requiredFree,
        Dictionary<uint, MaterialSynthesisRecipe> recipes,
        Dictionary<uint, StockState> stocks,
        HashSet<uint> recursionPath,
        ref ulong totalMora)
    {
        ulong free = item.CurrentCount > item.Stock.RequiredCount
            ? item.CurrentCount - item.Stock.RequiredCount
            : 0;
        if (free >= requiredFree
            || !TryGetRecipe(item.Stock.ItemId, recipes, stocks, out MaterialSynthesisRecipe recipe, out StockState ingredient)
            || !recursionPath.Add(item.Stock.ItemId))
        {
            return free;
        }

        try
        {
            ulong ownShortage = item.Stock.RequiredCount > item.CurrentCount
                ? item.Stock.RequiredCount - item.CurrentCount
                : 0;
            ulong outputsToCreate = ownShortage + requiredFree - free;
            ulong batchesNeeded = DivideRoundUp(outputsToCreate, recipe.ResultCount);
            batchesNeeded = Math.Min(batchesNeeded, uint.MaxValue - item.CurrentCount);

            ulong availableIngredient = EnsureFreeIngredients(
                ingredient,
                batchesNeeded * recipe.MaterialCount,
                recipes,
                stocks,
                recursionPath,
                ref totalMora);
            ulong batches = Math.Min(batchesNeeded, availableIngredient / recipe.MaterialCount);
            if (batches > 0)
            {
                ingredient.CurrentCount -= (uint)(batches * recipe.MaterialCount);
                item.CurrentCount += (uint)(batches * recipe.ResultCount);
                totalMora = AddMoraCost(totalMora, batches, recipe.MoraCost);
            }

            return item.CurrentCount > item.Stock.RequiredCount
                ? item.CurrentCount - item.Stock.RequiredCount
                : 0;
        }
        finally
        {
            recursionPath.Remove(item.Stock.ItemId);
        }
    }

    private static bool TryGetRecipe(
        uint resultId,
        Dictionary<uint, MaterialSynthesisRecipe> recipes,
        Dictionary<uint, StockState> stocks,
        out MaterialSynthesisRecipe recipe,
        out StockState ingredient)
    {
        recipe = default;
        ingredient = default!;

        if (!recipes.TryGetValue(resultId, out MaterialSynthesisRecipe candidate)
            || !stocks.TryGetValue(candidate.MaterialId, out StockState? source)
            || !stocks.TryGetValue(candidate.ResultId, out StockState? result)
            || (result.Stock.SeriesId is not 0
                && source.Stock.SeriesId is not 0
                && result.Stock.SeriesId != source.Stock.SeriesId))
        {
            return false;
        }

        recipe = candidate;
        ingredient = source;
        return true;
    }

    private static int GetSynthesisDepth(
        uint resultId,
        Dictionary<uint, MaterialSynthesisRecipe> recipes,
        Dictionary<uint, StockState> stocks)
    {
        return recipes.TryGetValue(resultId, out MaterialSynthesisRecipe recipe)
            && recipe.Category is not MaterialSynthesisCategory.None
            && TryGetSynthesisDepth(resultId, recipe.Category, recipes, stocks, [], out int depth)
                ? depth
                : 0;
    }

    private static bool IsMoraCostValid(MaterialSynthesisCategory category, int depth, uint moraCost)
    {
        /* 角色与武器培养素材在游戏内有两套费用表：普通怪物掉落为 25/50（深度 1/2），
           精英怪物掉落为 50/125；四阶家族（深度 3）均为 125。
           因此按深度校验时允许对应档位的两个合法取值，其余类别费用唯一。 */
        return (category, depth) switch
        {
            (MaterialSynthesisCategory.ElementalCrystal, 1) => moraCost is 300,
            (MaterialSynthesisCategory.ElementalCrystal, 2) => moraCost is 900,
            (MaterialSynthesisCategory.ElementalCrystal, 3) => moraCost is 2700,
            (MaterialSynthesisCategory.CharacterTalent, 1) => moraCost is 175,
            (MaterialSynthesisCategory.CharacterTalent, 2) => moraCost is 550,
            (MaterialSynthesisCategory.WeaponAscension, 1) => moraCost is 125,
            (MaterialSynthesisCategory.WeaponAscension, 2) => moraCost is 350,
            (MaterialSynthesisCategory.WeaponAscension, 3) => moraCost is 1075,
            (MaterialSynthesisCategory.CharacterAndWeaponEnhancement, 1) => moraCost is 25 or 50,
            (MaterialSynthesisCategory.CharacterAndWeaponEnhancement, 2) => moraCost is 50 or 125,
            (MaterialSynthesisCategory.CharacterAndWeaponEnhancement, 3) => moraCost is 125,
            _ => false,
        };
    }

    private static ulong AddMoraCost(ulong current, ulong batches, uint unitCost)
    {
        ulong cost = batches * unitCost;
        return ulong.MaxValue - current < cost ? ulong.MaxValue : current + cost;
    }

    private static ulong DivideRoundUp(ulong value, uint divisor)
    {
        return (value + divisor - 1) / divisor;
    }

    private sealed class StockState(MaterialSynthesisStock stock)
    {
        public MaterialSynthesisStock Stock { get; } = stock;

        public uint CurrentCount { get; set; } = stock.CurrentCount;
    }
}
