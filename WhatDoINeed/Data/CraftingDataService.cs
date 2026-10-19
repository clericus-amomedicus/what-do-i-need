using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using WhatDoINeed.Core.Crafting;

namespace WhatDoINeed.Data;

public sealed class CraftingDataService : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly IPluginLog log;
    private volatile Loaded? loaded;
    private volatile string? error;

    public CraftingDataService(LuminaCraftingDataReader reader, IPluginLog log)
    {
        this.log = log;
        Task.Run(() => Load(reader, cancellation.Token));
    }

    public string? Error => error;

    public RecipeIndex? Index => loaded?.Index;

    public MaterialPlanner? Planner => loaded?.Planner;

    public LocationPlanner? Locations => loaded?.Locations;

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void Load(LuminaCraftingDataReader reader, CancellationToken token)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var data = reader.Read();
            token.ThrowIfCancellationRequested();

            var index = new RecipeIndex(data);
            loaded = new Loaded(index, new MaterialPlanner(index), new LocationPlanner(index));
            log.Debug($"Indexed {index.RecipeCount} recipes, {index.ItemCount} items and {data.Spots.Count} gathering spots in {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            error = "Could not read recipe data.";
            log.Error(ex, "Failed to build the recipe index");
        }
    }

    private sealed record Loaded(RecipeIndex Index, MaterialPlanner Planner, LocationPlanner Locations);
}
