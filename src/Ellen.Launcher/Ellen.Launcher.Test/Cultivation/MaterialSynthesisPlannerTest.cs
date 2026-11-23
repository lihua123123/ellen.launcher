using System;
using System.Collections.Generic;
using Ellen.Launcher.Domain.Cultivation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.Cultivation;

[TestClass]
public class MaterialSynthesisPlannerTest
{
    private const uint SeriesId = 7001;

    [TestMethod]
    public void ThreeToOneShortageUsesAvailableSurplusAndKeepsUnfilledRemainders()
    {
        MaterialSynthesisPreview preview = CreateEnhancementPreview(firstLevelCount: 16, secondLevelCount: 8, thirdLevelRequired: 5);

        Assert.AreEqual(1U, preview.CurrentCounts[1]);
        Assert.AreEqual(1U, preview.CurrentCounts[2]);
        Assert.AreEqual(4U, preview.CurrentCounts[3]);
        Assert.AreEqual(325UL, preview.MoraCost);
    }

    [TestMethod]
    public void ThreeToOneShortageFullyResolvesAndLeavesPartialBaseSurplus()
    {
        MaterialSynthesisPreview preview = CreateEnhancementPreview(firstLevelCount: 26, secondLevelCount: 8, thirdLevelRequired: 5);

        Assert.AreEqual(5U, preview.CurrentCounts[1]);
        Assert.AreEqual(0U, preview.CurrentCounts[2]);
        Assert.AreEqual(5U, preview.CurrentCounts[3]);
        Assert.AreEqual(425UL, preview.MoraCost);
    }

    [TestMethod]
    public void SynthesisPreservesRequiredStockAndDoesNotSpendPartialBatches()
    {
        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(
        [
            new(1, SeriesId, 0, 2),
            new(2, SeriesId, 2, 4),
            new(3, SeriesId, 1, 0),
        ],
        EnhancementRecipes());

        Assert.AreEqual(2U, preview.CurrentCounts[1]);
        Assert.AreEqual(4U, preview.CurrentCounts[2]);
        Assert.AreEqual(0U, preview.CurrentCounts[3]);
        Assert.AreEqual(0UL, preview.MoraCost);
    }

    [TestMethod]
    public void ExactThreeItemSurplusSynthesizesOneItemAndPreservesRequiredCount()
    {
        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(
        [
            new(1, SeriesId, 0, 9),
            new(2, SeriesId, 2, 5),
            new(3, SeriesId, 1, 0),
        ],
        EnhancementRecipes());

        Assert.AreEqual(9U, preview.CurrentCounts[1]);
        Assert.AreEqual(2U, preview.CurrentCounts[2]);
        Assert.AreEqual(1U, preview.CurrentCounts[3]);
        Assert.AreEqual(50UL, preview.MoraCost);
    }

    [TestMethod]
    public void HighTierStockIsNeverBrokenDownToFillLowerTierShortage()
    {
        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(
        [
            new(1, SeriesId, 0, 0),
            new(2, SeriesId, 2, 0),
            new(3, SeriesId, 0, 1),
        ],
        EnhancementRecipes());

        Assert.AreEqual(0U, preview.CurrentCounts[1]);
        Assert.AreEqual(0U, preview.CurrentCounts[2]);
        Assert.AreEqual(1U, preview.CurrentCounts[3]);
        Assert.AreEqual(0UL, preview.MoraCost);
    }

    [TestMethod]
    public void FourTierGemChainSynthesizesIntermediatesFromDeepSurplus()
    {
        // Reproduces the reported new-element gem scenario:
        // 碎屑 444/1, 断片 96/9, 块 4/9, 玉 0/6
        // -> 玉 6/6, 块 9, 断片 27, Mora = 23 x 900 + 6 x 2700 = 36900.
        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(
        [
            new(1, SeriesId, 1, 444),
            new(2, SeriesId, 9, 96),
            new(3, SeriesId, 9, 4),
            new(4, SeriesId, 6, 0),
        ],
        GemRecipes());

        Assert.AreEqual(6U, preview.CurrentCounts[4]);
        Assert.AreEqual(9U, preview.CurrentCounts[3]);
        Assert.AreEqual(27U, preview.CurrentCounts[2]);
        Assert.AreEqual(444U, preview.CurrentCounts[1]);
        Assert.AreEqual(36900UL, preview.MoraCost);
    }

    [TestMethod]
    public void EliteMonsterMaterialFamilyUsesFiftyAndOneHundredTwentyFiveFeeSchedule()
    {
        // 精英怪掉落（号角/混沌/刻像等家族）的合成费用是 50/125，而非普通怪的 25/50。
        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(
        [
            new(1, SeriesId, 0, 12),
            new(2, SeriesId, 1, 0),
            new(3, SeriesId, 1, 0),
        ],
        [
            new(2, 1, 3, 1, 50, MaterialSynthesisCategory.CharacterAndWeaponEnhancement),
            new(3, 2, 3, 1, 125, MaterialSynthesisCategory.CharacterAndWeaponEnhancement),
        ]);

        Assert.AreEqual(1U, preview.CurrentCounts[3]);
        Assert.AreEqual(1U, preview.CurrentCounts[2]);
        Assert.AreEqual(0U, preview.CurrentCounts[1]);
        Assert.AreEqual(325UL, preview.MoraCost);
    }

    [DataTestMethod]
    [DataRow(MaterialSynthesisCategory.ElementalCrystal, 8100UL)]
    [DataRow(MaterialSynthesisCategory.WeaponAscension, 3250UL)]
    [DataRow(MaterialSynthesisCategory.CharacterTalent, 1075UL)]
    [DataRow(MaterialSynthesisCategory.CharacterAndWeaponEnhancement, 500UL)]
    public void MoraRulesMatchCategoryAndSynthesisTier(MaterialSynthesisCategory category, ulong expectedMora)
    {
        uint[] stageCosts = category switch
        {
            MaterialSynthesisCategory.ElementalCrystal => [300, 900, 2700],
            MaterialSynthesisCategory.CharacterTalent => [175, 550],
            MaterialSynthesisCategory.WeaponAscension => [125, 350, 1075],
            MaterialSynthesisCategory.CharacterAndWeaponEnhancement => [25, 50, 125],
            _ => throw new AssertFailedException($"Unexpected synthesis category {category}."),
        };

        List<MaterialSynthesisStock> stocks = [];
        List<MaterialSynthesisRecipe> recipes = [];
        for (uint id = 1; id <= stageCosts.Length + 1; id++)
        {
            stocks.Add(new(id, SeriesId, id == stageCosts.Length + 1 ? 1U : 0U, id == 1 ? (uint)Math.Pow(3, stageCosts.Length) : 0U));
        }

        for (int index = 0; index < stageCosts.Length; index++)
        {
            recipes.Add(new((uint)(index + 2), (uint)(index + 1), 3, 1, stageCosts[index], category));
        }

        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(stocks, recipes);

        Assert.AreEqual(1U, preview.CurrentCounts[(uint)(stageCosts.Length + 1)]);
        Assert.AreEqual(0U, preview.CurrentCounts[1]);
        Assert.AreEqual(expectedMora, preview.MoraCost);
    }

    [TestMethod]
    public void MaximumStockCountDoesNotOverflowDuringPartialSynthesis()
    {
        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(
        [
            new(1, SeriesId, 0, uint.MaxValue),
            new(2, SeriesId, 0, 0),
            new(3, SeriesId, 1, 0),
        ],
        EnhancementRecipes());

        Assert.AreEqual(uint.MaxValue - 9, preview.CurrentCounts[1]);
        Assert.AreEqual(0U, preview.CurrentCounts[2]);
        Assert.AreEqual(1U, preview.CurrentCounts[3]);
        Assert.AreEqual(125UL, preview.MoraCost);
    }

    [TestMethod]
    public void DuplicateRecipeAndCyclicRecipesAreRejectedSafely()
    {
        MaterialSynthesisPreview duplicate = MaterialSynthesisPlanner.CreatePreview(
        [new(1, SeriesId, 0, 6), new(2, SeriesId, 0, 0)],
        [
            new(2, 1, 3, 1, 25, MaterialSynthesisCategory.CharacterAndWeaponEnhancement),
            new(2, 1, 3, 1, 25, MaterialSynthesisCategory.CharacterAndWeaponEnhancement),
        ]);
        MaterialSynthesisPreview cycle = MaterialSynthesisPlanner.CreatePreview(
        [new(1, SeriesId, 0, 3), new(2, SeriesId, 0, 0)],
        [
            new(1, 2, 3, 1, 25, MaterialSynthesisCategory.CharacterAndWeaponEnhancement),
            new(2, 1, 3, 1, 50, MaterialSynthesisCategory.CharacterAndWeaponEnhancement),
        ]);

        Assert.AreEqual(0U, duplicate.CurrentCounts[2]);
        Assert.AreEqual(0UL, duplicate.MoraCost);
        Assert.AreEqual(0U, cycle.CurrentCounts[2]);
        Assert.AreEqual(0UL, cycle.MoraCost);
    }

    [TestMethod]
    public void UnsupportedCategoryWrongCostAndDifferentMaterialSeriesAreNotSynthesized()
    {
        MaterialSynthesisPreview unsupported = MaterialSynthesisPlanner.CreatePreview(
        [new(1, SeriesId, 0, 3), new(2, SeriesId, 0, 0)],
        [new(2, 1, 3, 1, 25, MaterialSynthesisCategory.None)]);
        MaterialSynthesisPreview wrongCost = MaterialSynthesisPlanner.CreatePreview(
        [new(1, SeriesId, 0, 3), new(2, SeriesId, 0, 0)],
        [new(2, 1, 3, 1, 26, MaterialSynthesisCategory.CharacterAndWeaponEnhancement)]);
        MaterialSynthesisPreview differentSeries = MaterialSynthesisPlanner.CreatePreview(
        [new(1, SeriesId + 1, 0, 3), new(2, SeriesId, 0, 0)],
        [new(2, 1, 3, 1, 25, MaterialSynthesisCategory.CharacterAndWeaponEnhancement)]);

        Assert.AreEqual(0U, unsupported.CurrentCounts[2]);
        Assert.AreEqual(0UL, unsupported.MoraCost);
        Assert.AreEqual(0U, wrongCost.CurrentCounts[2]);
        Assert.AreEqual(0UL, wrongCost.MoraCost);
        Assert.AreEqual(0U, differentSeries.CurrentCounts[2]);
        Assert.AreEqual(0UL, differentSeries.MoraCost);
    }

    [TestMethod]
    public void UnknownSeriesIdFromIncompleteMetadataStillSynthesizes()
    {
        // New-element gems may be missing from the local metadata so their family id (Rank)
        // can be 0; such recipes must still be accepted as long as costs match the rules.
        MaterialSynthesisPreview preview = MaterialSynthesisPlanner.CreatePreview(
        [
            new(1, 0, 0, 3),
            new(2, SeriesId, 1, 0),
        ],
        [new(2, 1, 3, 1, 25, MaterialSynthesisCategory.CharacterAndWeaponEnhancement)]);

        Assert.AreEqual(1U, preview.CurrentCounts[2]);
        Assert.AreEqual(25UL, preview.MoraCost);
    }

    private static MaterialSynthesisPreview CreateEnhancementPreview(uint firstLevelCount, uint secondLevelCount, uint thirdLevelRequired)
    {
        return MaterialSynthesisPlanner.CreatePreview(
        [
            new(1, SeriesId, 0, firstLevelCount),
            new(2, SeriesId, 0, secondLevelCount),
            new(3, SeriesId, thirdLevelRequired, 0),
        ],
        EnhancementRecipes());
    }

    private static MaterialSynthesisRecipe[] EnhancementRecipes()
    {
        return
        [
            new(2, 1, 3, 1, 25, MaterialSynthesisCategory.CharacterAndWeaponEnhancement),
            new(3, 2, 3, 1, 50, MaterialSynthesisCategory.CharacterAndWeaponEnhancement),
        ];
    }

    private static MaterialSynthesisRecipe[] GemRecipes()
    {
        return
        [
            new(2, 1, 3, 1, 300, MaterialSynthesisCategory.ElementalCrystal),
            new(3, 2, 3, 1, 900, MaterialSynthesisCategory.ElementalCrystal),
            new(4, 3, 3, 1, 2700, MaterialSynthesisCategory.ElementalCrystal),
        ];
    }
}
