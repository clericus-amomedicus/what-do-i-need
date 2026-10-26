using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using WhatDoINeed.Core.Crafting;
using WhatDoINeed.Core.Models;
using WhatDoINeed.Data;
using WhatDoINeed.Detection;

namespace WhatDoINeed.UI;

public sealed class MainWindow : Window
{
    private readonly CraftingDataService data;
    private readonly IGameGui gameGui;
    private readonly IChatGui chatGui;
    private readonly Configuration configuration;
    private readonly Action save;

    private string query = string.Empty;
    private string searchedQuery = string.Empty;
    private IReadOnlyList<RecipeNode> matches = [];
    private string? notice;
    private MaterialPlan? plan;
    private LocationPlan? locations;

    public MainWindow(CraftingDataService data, IGameGui gameGui, IChatGui chatGui, Configuration configuration, Action save) : base("What Do I Need?###WhatDoINeedMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(440, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.data = data;
        this.gameGui = gameGui;
        this.chatGui = chatGui;
        this.configuration = configuration;
        this.save = save;
    }

    public void Request(RecipeRequest request)
    {
        if (data.Index is not { } index)
            return;

        var options = index.GetRecipesFor(request.ItemId);
        if (request.RecipeId != 0)
        {
            Mark(request.RecipeId);
        }
        else if (options.Count == 1)
        {
            Mark(options[0].RecipeId);
        }
        else if (options.Count > 1)
        {
            query = index.GetItem(request.ItemId).Name;
            notice = $"{query} has {options.Count} recipes. Pick one below.";
        }

        IsOpen = true;
    }

    public void Search(string text)
    {
        query = text;
        notice = null;
        IsOpen = true;
    }

    public override void Draw()
    {
        if (data.Error is { } error)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), error);
            return;
        }

        if (data.Index is not { } index || data.Planner is not { } planner || data.Locations is not { } locationPlanner)
        {
            ImGui.TextDisabled("Loading recipe data...");
            return;
        }

        DrawSearch(index);
        ImGui.Separator();
        DrawMarked(index);
        ImGui.Separator();

        plan ??= planner.Plan(configuration.MarkedRecipes.Select(m => new PlanTarget(m.RecipeId, m.Amount)).ToList());
        if (plan.IsEmpty)
        {
            ImGui.TextDisabled("Mark a recipe to see what you need to gather.");
            return;
        }

        using var tabs = ImRaii.TabBar("##results");
        if (!tabs.Success)
            return;

        using (var tab = ImRaii.TabItem("Gather"))
        {
            if (tab.Success)
                DrawGather(plan);
        }

        using (var tab = ImRaii.TabItem("Locations"))
        {
            if (tab.Success)
                DrawLocations(index, locations ??= locationPlanner.Plan(plan));
        }

        using (var tab = ImRaii.TabItem("Craft"))
        {
            if (tab.Success)
                DrawCrafts(plan);
        }

        using (var tab = ImRaii.TabItem("Breakdown"))
        {
            if (tab.Success)
                DrawBreakdown(index, planner);
        }
    }

    private void DrawSearch(RecipeIndex index)
    {
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##query", "Search recipes by item name", ref query, 256))
            notice = null;

        if (query != searchedQuery)
        {
            searchedQuery = query;
            matches = index.FindByName(query);
        }

        if (notice is not null)
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), notice);

        if (query.Trim().Length == 0)
            return;

        using var list = ImRaii.Child("##matches", new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 6), true);
        if (!list.Success)
            return;

        foreach (var recipe in matches)
        {
            if (ImGui.Selectable($"{index.GetItem(recipe.ResultItemId).Name}  ({Job(recipe)})##{recipe.RecipeId}"))
                Mark(recipe.RecipeId);
        }

        if (matches.Count == 0)
            ImGui.TextDisabled("No recipes match.");
    }

    private void DrawMarked(RecipeIndex index)
    {
        ImGui.TextDisabled("Marked recipes");
        if (configuration.MarkedRecipes.Count == 0)
        {
            ImGui.TextDisabled("None yet. Search above, or right-click an item or Crafting Log recipe.");
            return;
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 60);
        if (ImGui.SmallButton("Clear all"))
        {
            configuration.MarkedRecipes.Clear();
            Changed();
            return;
        }

        MarkedRecipe? removed = null;
        foreach (var marked in configuration.MarkedRecipes)
        {
            using var id = ImRaii.PushId((int)marked.RecipeId);
            var recipe = index.GetRecipe(marked.RecipeId);

            var amount = marked.Amount;
            ImGui.SetNextItemWidth(90);
            if (ImGui.InputInt("##amount", ref amount) && amount >= 1)
            {
                marked.Amount = amount;
                Changed();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
                removed = marked;

            ImGui.SameLine();
            ImGui.TextUnformatted(recipe is null ? $"Unknown recipe #{marked.RecipeId}" : index.GetItem(recipe.ResultItemId).Name);
            if (recipe is not null)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(Job(recipe) + (recipe.Yield > 1 ? $", makes {recipe.Yield}" : string.Empty));
            }
        }

        if (removed is not null)
        {
            configuration.MarkedRecipes.Remove(removed);
            Changed();
        }
    }

    private void DrawGather(MaterialPlan plan)
    {
        if (ImGui.SmallButton("Copy list"))
            ImGui.SetClipboardText(GatherText(plan));
        ImGui.SameLine();
        ImGui.TextDisabled("Click an item to link it in chat.");

        foreach (var group in plan.Materials.GroupBy(m => m.Item.Source))
        {
            ImGui.Spacing();
            ImGui.TextDisabled(SourceLabel(group.Key));
            using var indent = ImRaii.PushIndent();
            foreach (var need in group)
            {
                ImGui.TextUnformatted($"{need.Amount}x {need.Item.Name}");
                LinkOnClick(need.Item);
            }
        }
    }

    private void DrawLocations(RecipeIndex index, LocationPlan route)
    {
        if (route.IsEmpty)
        {
            ImGui.TextDisabled("Nothing on this list is gathered by Miner, Botanist or Fisher.");
            return;
        }

        var now = EorzeaTime.MinuteOfDay(DateTimeOffset.UtcNow);
        ImGui.TextDisabled($"{route.Stops.Count} zone{(route.Stops.Count == 1 ? string.Empty : "s")}. Eorzea time {EorzeaTime.Format(now)}");

        foreach (var stop in route.Stops)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted(stop.Zone);
            using var indent = ImRaii.PushIndent();
            foreach (var visit in stop.Spots)
                DrawVisit(index, stop, visit, now);
        }

        if (route.Unlocated.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Only in special areas such as the Diadem or ocean fishing");
            using var indent = ImRaii.PushIndent();
            foreach (var need in route.Unlocated)
            {
                ImGui.TextUnformatted($"{need.Amount}x {need.Item.Name}");
                LinkOnClick(need.Item);
            }
        }
    }

    private void DrawVisit(RecipeIndex index, LocationStop stop, SpotVisit visit, int now)
    {
        var spot = visit.Spot;
        using var id = ImRaii.PushId(spot.Key);

        if (ImGui.SmallButton("Map"))
            gameGui.OpenMapWithMapLink(new MapLinkPayload(spot.TerritoryId, spot.MapId, spot.X, spot.Y));
        ImGui.SameLine();
        ImGui.TextUnformatted($"{spot.Area} ({spot.X:0.0}, {spot.Y:0.0})");
        ImGui.SameLine();
        ImGui.TextDisabled($"{JobLabel(spot.Job)} Lv {spot.Level}");

        using var indent = ImRaii.PushIndent();
        if (spot.Aetheryte is not null)
            ImGui.TextDisabled($"Nearest aetheryte: {spot.Aetheryte}");
        if (spot.IsTimed)
        {
            var up = spot.Windows.Any(w => w.Contains(now));
            var text = $"Timed: {string.Join(", ", spot.Windows.Select(w => $"{EorzeaTime.Format(w.StartMinute)} for {Duration(w.DurationMinutes)}"))} ET";
            if (up)
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.5f, 1f), text + " (up now)");
            else
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), text);
        }

        foreach (var need in visit.Items)
        {
            ImGui.TextUnformatted($"{need.Amount}x {need.Item.Name}");
            var elsewhere = index.GetSpots(need.Item.ItemId)
                .Where(s => s.TerritoryId != stop.TerritoryId)
                .Select(s => s.Zone)
                .Distinct()
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            LinkOnClick(need.Item, elsewhere.Count > 0 ? $"Also in: {string.Join(", ", elsewhere)}" : null);
        }
    }

    private void DrawCrafts(MaterialPlan plan)
    {
        ImGui.TextDisabled("In order, ingredients first");
        foreach (var step in plan.Crafts)
        {
            ImGui.TextUnformatted($"{step.Crafts}x {step.Result.Name}");
            LinkOnClick(step.Result);
            ImGui.SameLine();
            var extra = step.Produced > step.Needed ? $", makes {step.Produced}, need {step.Needed}" : string.Empty;
            ImGui.TextDisabled($"({Job(step.Recipe)}{extra})");
        }
    }

    private void DrawBreakdown(RecipeIndex index, MaterialPlanner planner)
    {
        foreach (var marked in configuration.MarkedRecipes)
        {
            if (index.GetRecipe(marked.RecipeId) is null)
                continue;

            using var id = ImRaii.PushId((int)marked.RecipeId);
            DrawTreeNode(planner.BuildTree(marked.RecipeId, marked.Amount), 0);
        }
    }

    private void DrawTreeNode(TreeNode node, int depth)
    {
        using var id = ImRaii.PushId($"{depth}:{node.Item.ItemId}");
        var label = node.Recipe is { } recipe
            ? $"{node.Amount}x {node.Item.Name}  ({Job(recipe)}, {node.Crafts} craft{(node.Crafts == 1 ? string.Empty : "s")})"
            : $"{node.Amount}x {node.Item.Name}";

        if (node.Children.Count == 0)
        {
            ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Bullet);
            LinkOnClick(node.Item);
            return;
        }

        // Only the arrow folds the node, so clicking the name can link it.
        var open = ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.OpenOnArrow);
        if (!ImGui.IsItemToggledOpen())
            LinkOnClick(node.Item);
        if (!open)
            return;

        foreach (var child in node.Children)
            DrawTreeNode(child, depth + 1);
        ImGui.TreePop();
    }

    private void LinkOnClick(ItemNode item, string? hint = null)
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        ImGui.SetTooltip(hint is null ? "Click to link in chat" : $"{hint}\nClick to link in chat");

        if (ImGui.IsItemClicked())
            chatGui.Print(SeString.CreateItemLink(item.ItemId, false), "What Do I Need?");
    }

    private void Mark(uint recipeId)
    {
        var existing = configuration.MarkedRecipes.FirstOrDefault(m => m.RecipeId == recipeId);
        if (existing is not null)
            existing.Amount++;
        else
            configuration.MarkedRecipes.Add(new MarkedRecipe { RecipeId = recipeId });

        notice = null;
        Changed();
    }

    private void Changed()
    {
        plan = null;
        locations = null;
        save();
    }

    private static string GatherText(MaterialPlan plan)
    {
        var text = new StringBuilder();
        foreach (var group in plan.Materials.GroupBy(m => m.Item.Source))
        {
            text.AppendLine(SourceLabel(group.Key));
            foreach (var need in group)
                text.AppendLine($"  {need.Amount}x {need.Item.Name}");
        }

        return text.ToString();
    }

    private static string Job(RecipeNode recipe) =>
        recipe.Level > 0 ? $"{recipe.CraftTypeName} Lv {recipe.Level}" : recipe.CraftTypeName;

    private static string JobLabel(MaterialSource source) => source switch
    {
        MaterialSource.Miner => "MIN",
        MaterialSource.Botanist => "BTN",
        _ => "FSH",
    };

    private static string Duration(int minutes) =>
        minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h {minutes % 60}m";

    private static string SourceLabel(MaterialSource source) => source switch
    {
        MaterialSource.Miner => "Miner",
        MaterialSource.Botanist => "Botanist",
        MaterialSource.Fisher => "Fisher",
        MaterialSource.Crystal => "Crystals",
        MaterialSource.Vendor => "Vendor",
        _ => "Other (drops, market board, etc.)",
    };
}
