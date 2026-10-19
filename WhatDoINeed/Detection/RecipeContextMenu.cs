using System;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using WhatDoINeed.Data;

namespace WhatDoINeed.Detection;

public sealed record RecipeRequest(uint RecipeId, uint ItemId);

public sealed unsafe class RecipeContextMenu : IDisposable
{
    private const uint ItemIdModulus = 500_000;

    private readonly IContextMenu contextMenu;
    private readonly CraftingDataService data;
    private readonly IPluginLog log;

    public RecipeContextMenu(IContextMenu contextMenu, CraftingDataService data, IPluginLog log)
    {
        this.contextMenu = contextMenu;
        this.data = data;
        this.log = log;

        contextMenu.OnMenuOpened += OnMenuOpened;
    }

    public event Action<RecipeRequest>? RecipeRequested;

    public void Dispose()
    {
        contextMenu.OnMenuOpened -= OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (data.Index is not { } index)
            return;

        var itemId = TargetItemId(args) % ItemIdModulus;
        if (itemId == 0 || index.GetRecipesFor(itemId).Count == 0)
            return;

        var recipeId = args.AddonName == "RecipeNote" ? SelectedCraftingLogRecipe(itemId) : 0;
        if (recipeId != 0 && index.GetRecipe(recipeId)?.ResultItemId != itemId)
            recipeId = 0;

        var request = new RecipeRequest(recipeId, itemId);
        args.AddMenuItem(new MenuItem
        {
            Name = "Mark recipe in What Do I Need?",
            PrefixChar = 'N',
            OnClicked = _ => RecipeRequested?.Invoke(request),
        });
        log.Verbose($"Context menu in {args.AddonName}: item {itemId}, recipe {recipeId}");
    }

    private static uint TargetItemId(IMenuOpenedArgs args)
    {
        if (args.MenuType == ContextMenuType.Inventory)
            return args.Target is MenuTargetInventory { TargetItem: { } item } ? item.BaseItemId : 0;

        return args.AddonName switch
        {
            "RecipeNote" => AgentRecipeNote.Instance()->ContextMenuResultItemId,
            "RecipeMaterialList" or "RecipeTree" => AgentRecipeItemContext.Instance()->ResultItemId,
            "ChatLog" => AgentChatLog.Instance()->ContextItemId,
            "ItemSearch" => (uint)AgentContext.Instance()->UpdateCheckerParam,
            _ => 0,
        };
    }

    private static uint SelectedCraftingLogRecipe(uint itemId)
    {
        var note = RecipeNote.Instance();
        if (note == null || note->RecipeList == null)
            return 0;

        var list = note->RecipeList;
        if (list->SelectedIndex >= list->RecipeCount)
            return 0;

        var entry = list->Recipes + list->SelectedIndex;
        return entry->ItemId == itemId ? entry->RecipeId : 0u;
    }
}
