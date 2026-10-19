using WhatDoINeed.Core.Models;

namespace WhatDoINeed.Core.Crafting;

public sealed record CraftingDataSnapshot(IReadOnlyList<ItemNode> Items, IReadOnlyList<RecipeNode> Recipes, IReadOnlyList<GatheringSpot> Spots);
