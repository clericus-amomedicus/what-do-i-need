using WhatDoINeed.Core.Crafting;
using WhatDoINeed.Core.Models;
using static WhatDoINeed.Tests.FixtureCraftingData;

namespace WhatDoINeed.Tests;

public class LocationPlannerTests
{
    private readonly RecipeIndex index = new(FixtureCraftingData.Create());
    private readonly MaterialPlanner materials;
    private readonly LocationPlanner locations;

    public LocationPlannerTests()
    {
        materials = new MaterialPlanner(index);
        locations = new LocationPlanner(index);
    }

    private LocationPlan PlanFor(uint recipeId) => locations.Plan(materials.Plan([new PlanTarget(recipeId, 1)]));

    [Fact]
    public void FewestZonesCoverEveryGatheredMaterial()
    {
        var plan = PlanFor(HatchetRecipe);

        Assert.Equal([CentralThanalan, CentralShroud], plan.Stops.Select(s => s.TerritoryId).Order());
        var thanalan = plan.Stops.Single(s => s.TerritoryId == CentralThanalan);
        var visit = Assert.Single(thanalan.Spots);
        Assert.Equal("node:spineless", visit.Spot.Key);
        Assert.Equal([CopperOre, TinOre], visit.Items.Select(i => i.Item.ItemId).Order());
        Assert.Equal(2, visit.Items.Single(i => i.Item.ItemId == CopperOre).Amount);
    }

    [Fact]
    public void UntimedSpotsArePreferredOverTimedOnes()
    {
        var plan = PlanFor(LumberRecipe);

        var visit = Assert.Single(Assert.Single(plan.Stops).Spots);
        Assert.Equal("node:bentbranch", visit.Spot.Key);
    }

    [Fact]
    public void CrystalsAreNotRouted()
    {
        var plan = PlanFor(HatchetRecipe);

        Assert.DoesNotContain(plan.Stops.SelectMany(s => s.Spots).SelectMany(v => v.Items), i => i.Item.Source == MaterialSource.Crystal);
        Assert.Empty(plan.Unlocated);
    }

    [Fact]
    public void GatheredItemsWithoutKnownSpotsAreReported()
    {
        var hidden = new ItemNode(777, "Hidden Ore", MaterialSource.Miner);
        var plan = locations.Plan(new MaterialPlan([new MaterialNeed(hidden, 3)], []));

        Assert.Empty(plan.Stops);
        Assert.Equal(777u, Assert.Single(plan.Unlocated).Item.ItemId);
    }

    [Fact]
    public void FishingSpotsAreIncluded()
    {
        var minnow = index.GetItem(Minnow);
        var plan = locations.Plan(new MaterialPlan([new MaterialNeed(minnow, 2)], []));

        Assert.Equal("fish:moraby", Assert.Single(Assert.Single(plan.Stops).Spots).Spot.Key);
    }
}

public class MapAndTimeTests
{
    [Fact]
    public void WorldCoordinatesConvertToMapCoordinates()
    {
        Assert.Equal(19.2f, MapCoordinates.FromWorld(-115.92f, 0, 100), 1);
        Assert.Equal(25.7f, MapCoordinates.FromWorld(209.15f, 0, 100), 1);
    }

    [Fact]
    public void PixelCoordinatesConvertToMapCoordinates()
    {
        Assert.Equal(21.1f, MapCoordinates.FromPixel(1004, 100), 1);
        Assert.Equal(7.7f, MapCoordinates.FromPixel(668, 200), 1);
    }

    [Fact]
    public void GameClockValuesBecomeMinutes()
    {
        Assert.Equal(120, EorzeaTime.FromGameClock(160));
        Assert.Equal(20 * 60, EorzeaTime.FromGameClock(2000));
        Assert.Equal("04:00", EorzeaTime.Format(240));
    }

    [Fact]
    public void EorzeaHourLastsTwoMinutesFiftyFiveSeconds()
    {
        Assert.Equal(0, EorzeaTime.MinuteOfDay(DateTimeOffset.FromUnixTimeMilliseconds(0)));
        Assert.Equal(60, EorzeaTime.MinuteOfDay(DateTimeOffset.FromUnixTimeMilliseconds(175_000)));
    }

    [Fact]
    public void WindowsWrapPastMidnight()
    {
        var window = new TimeWindow(23 * 60, 120);

        Assert.True(window.Contains(30));
        Assert.True(window.Contains(23 * 60));
        Assert.False(window.Contains(60));
    }
}
