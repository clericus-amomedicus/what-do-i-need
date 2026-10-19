using WhatDoINeed.Core.Models;

namespace WhatDoINeed.Core.Crafting;

public sealed class RecipeIndex
{
    private static readonly IReadOnlyList<RecipeNode> NoRecipes = [];
    private static readonly IReadOnlyList<GatheringSpot> NoSpots = [];

    private readonly Dictionary<uint, ItemNode> items;
    private readonly Dictionary<uint, RecipeNode> recipes;
    private readonly Dictionary<uint, IReadOnlyList<RecipeNode>> recipesByItem;
    private readonly Dictionary<uint, IReadOnlyList<GatheringSpot>> spotsByItem;

    public RecipeIndex(CraftingDataSnapshot data)
    {
        items = data.Items.ToDictionary(i => i.ItemId);
        recipes = data.Recipes
            .Where(r => items.ContainsKey(r.ResultItemId) && r.Yield > 0)
            .ToDictionary(r => r.RecipeId);
        recipesByItem = recipes.Values
            .GroupBy(r => r.ResultItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RecipeNode>)g.OrderBy(r => r.RecipeId).ToList());
        spotsByItem = data.Spots
            .SelectMany(s => s.ItemIds.Distinct().Select(id => (ItemId: id, Spot: s)))
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GatheringSpot>)g.Select(x => x.Spot).ToList());
    }

    public int ItemCount => items.Count;

    public int RecipeCount => recipes.Count;

    public ItemNode GetItem(uint itemId) =>
        items.GetValueOrDefault(itemId) ?? new ItemNode(itemId, $"Item #{itemId}", MaterialSource.Other);

    public RecipeNode? GetRecipe(uint recipeId) => recipes.GetValueOrDefault(recipeId);

    public IReadOnlyList<RecipeNode> GetRecipesFor(uint itemId) => recipesByItem.GetValueOrDefault(itemId) ?? NoRecipes;

    public IReadOnlyList<GatheringSpot> GetSpots(uint itemId) => spotsByItem.GetValueOrDefault(itemId) ?? NoSpots;

    public RecipeNode? ChooseRecipe(uint itemId, uint? preferredCraftTypeId)
    {
        var options = GetRecipesFor(itemId);
        if (options.Count == 0)
            return null;

        return options.FirstOrDefault(r => r.CraftTypeId == preferredCraftTypeId) ?? options[0];
    }

    public IReadOnlyList<RecipeNode> FindByName(string query, int limit = 30)
    {
        var needle = query.Trim();
        if (needle.Length == 0)
            return [];

        return recipes.Values
            .Select(r => (Recipe: r, Name: items[r.ResultItemId].Name))
            .Select(m => (m.Recipe, m.Name, Rank: Rank(m.Name, needle)))
            .Where(m => m.Rank >= 0)
            .OrderBy(m => m.Rank)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Recipe.RecipeId)
            .Take(limit)
            .Select(m => m.Recipe)
            .ToList();
    }

    private static int Rank(string name, string needle)
    {
        if (name.Equals(needle, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return 2;
        return -1;
    }
}
