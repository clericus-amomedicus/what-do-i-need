using WhatDoINeed.Core.Models;

namespace WhatDoINeed.Core.Crafting;

public sealed class MaterialPlanner
{
    private readonly RecipeIndex index;

    public MaterialPlanner(RecipeIndex index)
    {
        this.index = index;
    }

    public MaterialPlan Plan(IReadOnlyList<PlanTarget> targets)
    {
        var roots = targets
            .Where(t => t.Amount > 0)
            .Select(t => (Recipe: index.GetRecipe(t.RecipeId), t.Amount))
            .Where(t => t.Recipe is not null)
            .Select(t => (Recipe: t.Recipe!, t.Amount))
            .ToList();

        if (roots.Count == 0)
            return MaterialPlan.Empty;

        var forcedBase = new HashSet<uint>();
        while (true)
        {
            var ordering = new Ordering(index, forcedBase);
            foreach (var (recipe, _) in roots)
                ordering.Choose(recipe);

            if (ordering.Build(roots.Select(r => r.Recipe.ResultItemId)) is { } cycleItem)
            {
                forcedBase.Add(cycleItem);
                continue;
            }

            return Propagate(roots, ordering);
        }
    }

    public TreeNode BuildTree(uint recipeId, int amount)
    {
        var recipe = index.GetRecipe(recipeId);
        if (recipe is null)
            return new TreeNode(index.GetItem(0), amount, null, 0, []);

        return Expand(recipe.ResultItemId, amount, recipe, new HashSet<uint>());
    }

    private TreeNode Expand(uint itemId, int amount, RecipeNode? recipe, HashSet<uint> path)
    {
        var item = index.GetItem(itemId);
        if (recipe is null || !path.Add(itemId))
            return new TreeNode(item, amount, null, 0, []);

        var crafts = CraftsFor(amount, recipe.Yield);
        var children = recipe.Ingredients
            .Select(i => Expand(i.ItemId, i.Amount * crafts, index.ChooseRecipe(i.ItemId, recipe.CraftTypeId), path))
            .ToList();
        path.Remove(itemId);

        return new TreeNode(item, amount, recipe, crafts, children);
    }

    private MaterialPlan Propagate(IReadOnlyList<(RecipeNode Recipe, int Amount)> roots, Ordering ordering)
    {
        var demand = new Dictionary<uint, long>();
        foreach (var (recipe, amount) in roots)
            demand[recipe.ResultItemId] = demand.GetValueOrDefault(recipe.ResultItemId) + amount;

        var crafts = new List<CraftStep>();
        var materials = new List<MaterialNeed>();

        foreach (var itemId in ordering.ConsumersFirst)
        {
            var needed = demand.GetValueOrDefault(itemId);
            if (needed <= 0)
                continue;

            var item = index.GetItem(itemId);
            if (ordering.RecipeFor(itemId) is not { } recipe)
            {
                materials.Add(new MaterialNeed(item, ToInt(needed)));
                continue;
            }

            var count = CraftsFor(needed, recipe.Yield);
            crafts.Add(new CraftStep(recipe, item, ToInt(needed), ToInt(count)));
            foreach (var ingredient in recipe.Ingredients)
                demand[ingredient.ItemId] = demand.GetValueOrDefault(ingredient.ItemId) + (count * ingredient.Amount);
        }

        crafts.Reverse();
        materials.Sort((a, b) =>
        {
            var bySource = a.Item.Source.CompareTo(b.Item.Source);
            return bySource != 0 ? bySource : StringComparer.OrdinalIgnoreCase.Compare(a.Item.Name, b.Item.Name);
        });

        return new MaterialPlan(materials, crafts);
    }

    private static int CraftsFor(int amount, int yield) => (amount + yield - 1) / yield;

    private static long CraftsFor(long amount, int yield) => (amount + yield - 1) / yield;

    private static int ToInt(long value) => value > int.MaxValue ? int.MaxValue : (int)value;

    private sealed class Ordering
    {
        private readonly RecipeIndex index;
        private readonly IReadOnlySet<uint> forcedBase;
        private readonly Dictionary<uint, RecipeNode?> chosen = [];
        private readonly HashSet<uint> visiting = [];
        private readonly HashSet<uint> done = [];
        private readonly List<uint> postOrder = [];

        public Ordering(RecipeIndex index, IReadOnlySet<uint> forcedBase)
        {
            this.index = index;
            this.forcedBase = forcedBase;
        }

        public IEnumerable<uint> ConsumersFirst => Enumerable.Reverse(postOrder);

        public RecipeNode? RecipeFor(uint itemId) => chosen.GetValueOrDefault(itemId);

        public void Choose(RecipeNode recipe) => chosen.TryAdd(recipe.ResultItemId, forcedBase.Contains(recipe.ResultItemId) ? null : recipe);

        public uint? Build(IEnumerable<uint> rootItems)
        {
            foreach (var itemId in rootItems)
            {
                if (Visit(itemId, null) is { } cycleItem)
                    return cycleItem;
            }

            return null;
        }

        private uint? Visit(uint itemId, uint? preferredCraftTypeId)
        {
            if (done.Contains(itemId))
                return null;

            visiting.Add(itemId);
            if (!chosen.ContainsKey(itemId))
                chosen[itemId] = forcedBase.Contains(itemId) ? null : index.ChooseRecipe(itemId, preferredCraftTypeId);

            if (chosen[itemId] is { } recipe)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (visiting.Contains(ingredient.ItemId))
                        return itemId;

                    if (Visit(ingredient.ItemId, recipe.CraftTypeId) is { } cycleItem)
                        return cycleItem;
                }
            }

            visiting.Remove(itemId);
            done.Add(itemId);
            postOrder.Add(itemId);
            return null;
        }
    }
}
