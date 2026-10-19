using WhatDoINeed.Core.Crafting;
using WhatDoINeed.Core.Models;

namespace WhatDoINeed.Tests;

public sealed class FixtureCraftingData
{
    public const uint FireShard = 2;
    public const uint IceShard = 3;
    public const uint WindShard = 4;
    public const uint CopperOre = 5106;
    public const uint TinOre = 5107;
    public const uint MapleLog = 5380;
    public const uint Minnow = 4869;
    public const uint BronzeIngot = 5056;
    public const uint BronzeRivets = 5096;
    public const uint MapleLumber = 5361;
    public const uint BronzeHatchet = 2577;
    public const uint BronzeBuckler = 2290;
    public const uint LoopA = 90001;
    public const uint LoopB = 90002;

    public const uint CentralThanalan = 141;
    public const uint WesternThanalan = 140;
    public const uint CentralShroud = 148;
    public const uint LowerLaNoscea = 135;

    public const uint IngotBySmithing = 1;
    public const uint IngotByArmorcraft = 170;
    public const uint RivetsRecipe = 171;
    public const uint LumberRecipe = 400;
    public const uint HatchetRecipe = 20;
    public const uint BucklerRecipe = 180;
    public const uint LoopARecipe = 9001;
    public const uint LoopBRecipe = 9002;

    private const uint Carpentry = 0;
    private const uint Smithing = 1;
    private const uint Armorcraft = 2;

    private readonly List<ItemNode> items = [];
    private readonly List<RecipeNode> recipes = [];
    private readonly List<GatheringSpot> spots = [];

    private FixtureCraftingData()
    {
        Item(FireShard, "Fire Shard", MaterialSource.Crystal);
        Item(IceShard, "Ice Shard", MaterialSource.Crystal);
        Item(WindShard, "Wind Shard", MaterialSource.Crystal);
        Item(CopperOre, "Copper Ore", MaterialSource.Miner);
        Item(TinOre, "Tin Ore", MaterialSource.Miner);
        Item(MapleLog, "Maple Log", MaterialSource.Botanist);
        Item(Minnow, "Minnow", MaterialSource.Fisher);
        Item(BronzeIngot, "Bronze Ingot", MaterialSource.Other);
        Item(BronzeRivets, "Bronze Rivets", MaterialSource.Other);
        Item(MapleLumber, "Maple Lumber", MaterialSource.Other);
        Item(BronzeHatchet, "Bronze Hatchet", MaterialSource.Other);
        Item(BronzeBuckler, "Bronze Buckler", MaterialSource.Other);
        Item(LoopA, "Loop A", MaterialSource.Other);
        Item(LoopB, "Loop B", MaterialSource.Other);

        Recipe(IngotBySmithing, BronzeIngot, 1, Smithing, (CopperOre, 2), (TinOre, 1), (FireShard, 1));
        Recipe(IngotByArmorcraft, BronzeIngot, 1, Armorcraft, (CopperOre, 2), (TinOre, 1), (IceShard, 1));
        Recipe(RivetsRecipe, BronzeRivets, 3, Armorcraft, (BronzeIngot, 1), (IceShard, 1));
        Recipe(LumberRecipe, MapleLumber, 1, Carpentry, (MapleLog, 3), (WindShard, 1));
        Recipe(HatchetRecipe, BronzeHatchet, 1, Smithing, (BronzeIngot, 1), (MapleLumber, 1), (FireShard, 2));
        Recipe(BucklerRecipe, BronzeBuckler, 1, Armorcraft, (BronzeIngot, 2), (BronzeRivets, 4), (IceShard, 2));
        Recipe(LoopARecipe, LoopA, 1, Smithing, (LoopB, 1), (FireShard, 1));
        Recipe(LoopBRecipe, LoopB, 1, Smithing, (LoopA, 1), (Minnow, 2));

        Spot("node:spineless", MaterialSource.Miner, CentralThanalan, "Central Thanalan", "Spineless Basin", 5, [], CopperOre, TinOre);
        Spot("node:hammerlea", MaterialSource.Miner, WesternThanalan, "Western Thanalan", "Hammerlea", 5, [], CopperOre);
        Spot("node:bentbranch", MaterialSource.Botanist, CentralShroud, "Central Shroud", "Bentbranch", 10, [], MapleLog);
        Spot("node:timedgrove", MaterialSource.Botanist, CentralShroud, "Central Shroud", "Timed Grove", 1, [new TimeWindow(0, 120)], MapleLog);
        Spot("fish:moraby", MaterialSource.Fisher, LowerLaNoscea, "Lower La Noscea", "Moraby Bay", 5, [], Minnow);
    }

    public static CraftingDataSnapshot Create()
    {
        var fixture = new FixtureCraftingData();
        return new CraftingDataSnapshot(fixture.items, fixture.recipes, fixture.spots);
    }

    private void Item(uint id, string name, MaterialSource source) => items.Add(new ItemNode(id, name, source));

    private void Spot(string key, MaterialSource job, uint territory, string zone, string area, int level, TimeWindow[] windows, params uint[] itemIds) =>
        spots.Add(new GatheringSpot(key, job, territory, territory, zone, area, 10f, 10f, level, null, windows, itemIds));

    private void Recipe(uint id, uint result, int yield, uint craftType, params (uint Item, int Amount)[] ingredients) =>
        recipes.Add(new RecipeNode(id, result, yield, craftType, $"Craft {craftType}", 1,
            ingredients.Select(i => new Ingredient(i.Item, i.Amount)).ToList()));
}
