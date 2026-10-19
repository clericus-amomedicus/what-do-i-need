namespace WhatDoINeed.Core.Models;

public sealed record Ingredient(uint ItemId, int Amount);

public sealed record RecipeNode(uint RecipeId, uint ResultItemId, int Yield, uint CraftTypeId, string CraftTypeName, int Level, IReadOnlyList<Ingredient> Ingredients);
