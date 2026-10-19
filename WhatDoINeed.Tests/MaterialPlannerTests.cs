using WhatDoINeed.Core.Crafting;
using WhatDoINeed.Core.Models;
using static WhatDoINeed.Tests.FixtureCraftingData;

namespace WhatDoINeed.Tests;

public class MaterialPlannerTests
{
    private readonly RecipeIndex index = new(FixtureCraftingData.Create());
    private readonly MaterialPlanner planner;

    public MaterialPlannerTests()
    {
        planner = new MaterialPlanner(index);
    }

    private static Dictionary<uint, int> Materials(MaterialPlan plan) =>
        plan.Materials.ToDictionary(m => m.Item.ItemId, m => m.Amount);

    private static Dictionary<uint, int> Crafts(MaterialPlan plan) =>
        plan.Crafts.ToDictionary(c => c.Recipe.RecipeId, c => c.Crafts);

    [Fact]
    public void IntermediatesAreExpandedIntoBaseMaterials()
    {
        var plan = planner.Plan([new PlanTarget(HatchetRecipe, 1)]);

        Assert.Equal(new Dictionary<uint, int>
        {
            [CopperOre] = 2,
            [TinOre] = 1,
            [MapleLog] = 3,
            [FireShard] = 3,
            [WindShard] = 1,
        }, Materials(plan));
    }

    [Fact]
    public void CraftsAreListedIngredientsFirst()
    {
        var plan = planner.Plan([new PlanTarget(HatchetRecipe, 1)]);

        Assert.Equal(HatchetRecipe, plan.Crafts[^1].Recipe.RecipeId);
        Assert.Contains(plan.Crafts.Take(2), c => c.Recipe.RecipeId == IngotBySmithing);
        Assert.Contains(plan.Crafts.Take(2), c => c.Recipe.RecipeId == LumberRecipe);
    }

    [Fact]
    public void IntermediatePrefersTheSameJobAsItsConsumer()
    {
        var plan = planner.Plan([new PlanTarget(BucklerRecipe, 1)]);

        Assert.True(Crafts(plan).ContainsKey(IngotByArmorcraft));
        Assert.False(Crafts(plan).ContainsKey(IngotBySmithing));
    }

    [Fact]
    public void YieldRoundsCraftsUp()
    {
        var plan = planner.Plan([new PlanTarget(RivetsRecipe, 4)]);

        var rivets = plan.Crafts.Single(c => c.Recipe.RecipeId == RivetsRecipe);
        Assert.Equal(2, rivets.Crafts);
        Assert.Equal(6, rivets.Produced);
        Assert.Equal(2, Crafts(plan)[IngotByArmorcraft]);
    }

    [Fact]
    public void SharedIntermediatesAreBatchedAcrossTargets()
    {
        var plan = planner.Plan([new PlanTarget(RivetsRecipe, 1), new PlanTarget(RivetsRecipe, 2)]);

        Assert.Equal(1, Crafts(plan)[RivetsRecipe]);
        Assert.Equal(2, Materials(plan)[CopperOre]);
    }

    [Fact]
    public void BucklerTotalsIncludeRivetBatches()
    {
        var plan = planner.Plan([new PlanTarget(BucklerRecipe, 1)]);

        Assert.Equal(2, Crafts(plan)[RivetsRecipe]);
        Assert.Equal(4, Crafts(plan)[IngotByArmorcraft]);
        Assert.Equal(8, Materials(plan)[CopperOre]);
        Assert.Equal(4, Materials(plan)[TinOre]);
        Assert.Equal(8, Materials(plan)[IceShard]);
    }

    [Fact]
    public void MaterialsAreGroupedBySource()
    {
        var plan = planner.Plan([new PlanTarget(HatchetRecipe, 1)]);

        var sources = plan.Materials.Select(m => m.Item.Source).ToList();
        Assert.Equal(sources.OrderBy(s => s), sources);
    }

    [Fact]
    public void CyclesAreBrokenWithoutLosingTheMarkedRecipe()
    {
        var plan = planner.Plan([new PlanTarget(LoopARecipe, 1)]);

        Assert.True(Crafts(plan).ContainsKey(LoopARecipe));
        Assert.Equal(1, Materials(plan)[LoopB]);
        Assert.Equal(1, Materials(plan)[FireShard]);
    }

    [Fact]
    public void UnknownOrEmptyTargetsGiveAnEmptyPlan()
    {
        Assert.True(planner.Plan([new PlanTarget(123456, 1), new PlanTarget(HatchetRecipe, 0)]).IsEmpty);
    }

    [Fact]
    public void TreeShowsEachBranch()
    {
        var tree = planner.BuildTree(BucklerRecipe, 1);

        Assert.Equal(BronzeBuckler, tree.Item.ItemId);
        var rivets = tree.Children.Single(c => c.Item.ItemId == BronzeRivets);
        Assert.Equal(4, rivets.Amount);
        Assert.Equal(2, rivets.Crafts);
        var ingot = rivets.Children.Single(c => c.Item.ItemId == BronzeIngot);
        Assert.Equal(2, ingot.Amount);
        Assert.Equal(IngotByArmorcraft, ingot.Recipe?.RecipeId);
        Assert.Equal(4, ingot.Children.Single(c => c.Item.ItemId == CopperOre).Amount);
    }

    [Fact]
    public void TreeStopsAtCycles()
    {
        var tree = planner.BuildTree(LoopARecipe, 1);

        var loopB = tree.Children.Single(c => c.Item.ItemId == LoopB);
        var loopAAgain = loopB.Children.Single(c => c.Item.ItemId == LoopA);
        Assert.Null(loopAAgain.Recipe);
        Assert.Empty(loopAAgain.Children);
    }
}

public class RecipeIndexTests
{
    private readonly RecipeIndex index = new(FixtureCraftingData.Create());

    [Fact]
    public void SearchRanksExactThenPrefixThenContains()
    {
        var names = index.FindByName("bronze").Select(r => index.GetItem(r.ResultItemId).Name).Distinct().ToList();
        Assert.Equal(["Bronze Buckler", "Bronze Hatchet", "Bronze Ingot", "Bronze Rivets"], names);

        Assert.Equal(LumberRecipe, index.FindByName("lumber").Single().RecipeId);
    }

    [Fact]
    public void ItemsWithSeveralRecipesListThemAll()
    {
        Assert.Equal([IngotBySmithing, IngotByArmorcraft], index.GetRecipesFor(BronzeIngot).Select(r => r.RecipeId));
    }

    [Fact]
    public void UnknownItemsGetAPlaceholder()
    {
        Assert.Equal(MaterialSource.Other, index.GetItem(424242).Source);
    }
}
