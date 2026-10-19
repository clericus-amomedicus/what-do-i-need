namespace WhatDoINeed.Core.Models;

public sealed record PlanTarget(uint RecipeId, int Amount);

public sealed record MaterialNeed(ItemNode Item, int Amount);

public sealed record CraftStep(RecipeNode Recipe, ItemNode Result, int Needed, int Crafts)
{
    public int Produced => Crafts * Recipe.Yield;
}

public sealed record MaterialPlan(IReadOnlyList<MaterialNeed> Materials, IReadOnlyList<CraftStep> Crafts)
{
    public static readonly MaterialPlan Empty = new([], []);

    public bool IsEmpty => Materials.Count == 0 && Crafts.Count == 0;
}

public sealed record TreeNode(ItemNode Item, int Amount, RecipeNode? Recipe, int Crafts, IReadOnlyList<TreeNode> Children);
