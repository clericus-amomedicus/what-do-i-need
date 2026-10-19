using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using WhatDoINeed.Data;
using WhatDoINeed.Detection;
using WhatDoINeed.UI;

namespace WhatDoINeed;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/whatdoineed";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;

    private readonly Configuration configuration;
    private readonly CraftingDataService craftingData;
    private readonly RecipeContextMenu contextMenu;
    private readonly WindowSystem windowSystem = new("WhatDoINeed");
    private readonly MainWindow mainWindow;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IDataManager dataManager,
        IContextMenu contextMenu,
        IGameGui gameGui,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;

        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        craftingData = new CraftingDataService(new LuminaCraftingDataReader(dataManager.Excel), log);

        mainWindow = new MainWindow(craftingData, gameGui, configuration, () => pluginInterface.SavePluginConfig(configuration));
        windowSystem.AddWindow(mainWindow);

        this.contextMenu = new RecipeContextMenu(contextMenu, craftingData, log);
        this.contextMenu.RecipeRequested += mainWindow.Request;

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the window. Add a name to search recipes, e.g. /whatdoineed bronze ingot",
        });

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        commandManager.RemoveHandler(CommandName);

        contextMenu.RecipeRequested -= mainWindow.Request;
        contextMenu.Dispose();
        windowSystem.RemoveAllWindows();
        craftingData.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var text = args.Trim();
        if (text.Length == 0)
            mainWindow.Toggle();
        else
            mainWindow.Search(text);
    }
}
