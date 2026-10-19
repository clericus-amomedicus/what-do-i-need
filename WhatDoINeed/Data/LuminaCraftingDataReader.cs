using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using WhatDoINeed.Core.Crafting;
using WhatDoINeed.Core.Models;

namespace WhatDoINeed.Data;

public sealed class LuminaCraftingDataReader
{
    private const uint CrystalUiCategory = 59;
    private const uint FirstCrafterClassJob = 8;
    private const ushort NoTime = ushort.MaxValue;
    private const byte AetheryteMarker = 3;

    private static readonly HashSet<uint> RoutableTerritoryUses = [0, 1];

    private static readonly Dictionary<uint, MaterialSource> GatheringTypeSources = new()
    {
        [0] = MaterialSource.Miner,
        [1] = MaterialSource.Miner,
        [2] = MaterialSource.Botanist,
        [3] = MaterialSource.Botanist,
    };

    private readonly ExcelModule excel;

    public LuminaCraftingDataReader(ExcelModule excel)
    {
        this.excel = excel;
    }

    public CraftingDataSnapshot Read()
    {
        var recipes = new List<RecipeNode>();
        var referenced = new HashSet<uint>();
        var jobs = excel.GetSheet<ClassJob>();

        foreach (var recipe in excel.GetSheet<Recipe>())
        {
            var result = recipe.ItemResult.RowId;
            if (!IsItemId(result) || recipe.AmountResult == 0)
                continue;

            var ingredients = new List<Ingredient>();
            for (var i = 0; i < recipe.Ingredient.Count; i++)
            {
                var itemId = recipe.Ingredient[i].RowId;
                var amount = recipe.AmountIngredient[i];
                if (!IsItemId(itemId) || amount == 0)
                    continue;

                ingredients.Add(new Ingredient(itemId, amount));
                referenced.Add(itemId);
            }

            if (ingredients.Count == 0)
                continue;

            var craftType = recipe.CraftType.RowId;
            var jobName = jobs.TryGetRow(craftType + FirstCrafterClassJob, out var job)
                ? Text(job.Abbreviation)
                : Text(recipe.CraftType.ValueNullable?.Name ?? default);

            recipes.Add(new RecipeNode(
                recipe.RowId,
                result,
                recipe.AmountResult,
                craftType,
                jobName,
                recipe.RecipeLevelTable.ValueNullable?.ClassJobLevel ?? 0,
                ingredients));
            referenced.Add(result);
        }

        var sources = GatherSources();
        var vendorItems = VendorItems();
        var itemSheet = excel.GetSheet<Item>();
        var items = new List<ItemNode>();
        foreach (var itemId in referenced)
        {
            if (!itemSheet.TryGetRow(itemId, out var item))
                continue;

            var name = Text(item.Name);
            if (name.Length == 0)
                continue;

            var source = item.ItemUICategory.RowId == CrystalUiCategory ? MaterialSource.Crystal
                : sources.TryGetValue(itemId, out var gathered) ? gathered
                : vendorItems.Contains(itemId) ? MaterialSource.Vendor
                : MaterialSource.Other;
            items.Add(new ItemNode(itemId, name, source));
        }

        return new CraftingDataSnapshot(items, recipes, Spots(referenced));
    }

    private Dictionary<uint, MaterialSource> GatherSources()
    {
        var sources = new Dictionary<uint, MaterialSource>();
        var gatheringItems = excel.GetSheet<GatheringItem>();

        var seenBases = new HashSet<uint>();

        foreach (var point in excel.GetSheet<GatheringPoint>())
        {
            if (Text(point.TerritoryType.ValueNullable?.PlaceName.ValueNullable?.Name ?? default).Length == 0 ||
                !seenBases.Add(point.GatheringPointBase.RowId) ||
                point.GatheringPointBase.ValueNullable is not { } pointBase ||
                !GatheringTypeSources.TryGetValue(pointBase.GatheringType.RowId, out var source))
            {
                continue;
            }

            foreach (var entry in pointBase.Item)
            {
                if (entry.RowId != 0 && gatheringItems.TryGetRow(entry.RowId, out var gatheringItem) && gatheringItem.Item.Is<Item>())
                    sources.TryAdd(gatheringItem.Item.RowId, source);
            }
        }

        foreach (var fish in excel.GetSheet<FishParameter>())
        {
            if (fish.Item.Is<Item>() && fish.Item.RowId != 0)
                sources.TryAdd(fish.Item.RowId, MaterialSource.Fisher);
        }

        foreach (var fish in excel.GetSheet<SpearfishingItem>())
        {
            if (fish.Item.RowId != 0)
                sources.TryAdd(fish.Item.RowId, MaterialSource.Fisher);
        }

        return sources;
    }

    private HashSet<uint> VendorItems()
    {
        var items = new HashSet<uint>();
        foreach (var shop in excel.GetSubrowSheet<GilShopItem>())
        {
            foreach (var entry in shop)
            {
                if (entry.Item.RowId != 0)
                    items.Add(entry.Item.RowId);
            }
        }

        return items;
    }

    private List<GatheringSpot> Spots(IReadOnlySet<uint> wanted)
    {
        var zones = Zones();
        var spots = new List<GatheringSpot>();
        var gatheringItems = excel.GetSheet<GatheringItem>();
        var exported = excel.GetSheet<ExportedGatheringPoint>();
        var transients = excel.GetSheet<GatheringPointTransient>();

        var nodes = excel.GetSheet<GatheringPoint>()
            .Where(p => p.GatheringPointBase.RowId != 0 && zones.ContainsKey(p.TerritoryType.RowId))
            .GroupBy(p => (Base: p.GatheringPointBase.RowId, Territory: p.TerritoryType.RowId));

        foreach (var node in nodes)
        {
            var point = node.First();
            if (point.GatheringPointBase.ValueNullable is not { } pointBase ||
                !GatheringTypeSources.TryGetValue(pointBase.GatheringType.RowId, out var job) ||
                !exported.TryGetRow(pointBase.RowId, out var position))
            {
                continue;
            }

            var itemIds = pointBase.Item
                .Where(i => i.RowId != 0 && gatheringItems.TryGetRow(i.RowId, out var g) && g.Item.Is<Item>())
                .Select(i => gatheringItems.GetRow(i.RowId).Item.RowId)
                .Where(wanted.Contains)
                .ToList();

            var zone = zones[node.Key.Territory];
            var windows = node
                .Select(p => transients.TryGetRow(p.RowId, out var t) ? Windows(t) : [])
                .FirstOrDefault(w => w.Count > 0) ?? [];
            var x = MapCoordinates.FromWorld(position.X, zone.Map.OffsetX, zone.Map.SizeFactor);
            var y = MapCoordinates.FromWorld(position.Y, zone.Map.OffsetY, zone.Map.SizeFactor);

            Add(spots, $"node:{pointBase.RowId}:{node.Key.Territory}", job, zone, Text(point.PlaceName.ValueNullable?.Name ?? default), x, y, pointBase.GatheringLevel, windows, itemIds);
        }

        foreach (var hole in excel.GetSheet<FishingSpot>())
        {
            if (!zones.TryGetValue(hole.TerritoryType.RowId, out var zone))
                continue;

            var itemIds = hole.Item.Select(i => i.RowId).Where(wanted.Contains).ToList();
            var x = MapCoordinates.FromPixel(hole.X, zone.Map.SizeFactor);
            var y = MapCoordinates.FromPixel(hole.Z, zone.Map.SizeFactor);
            Add(spots, $"fish:{hole.RowId}", MaterialSource.Fisher, zone, Text(hole.PlaceName.ValueNullable?.Name ?? default), x, y, hole.GatheringLevel, [], itemIds);
        }

        var spearItems = excel.GetSheet<SpearfishingItem>();
        foreach (var notebook in excel.GetSheet<SpearfishingNotebook>())
        {
            if (!zones.TryGetValue(notebook.TerritoryType.RowId, out var zone) || notebook.GatheringPointBase.ValueNullable is not { } pointBase)
                continue;

            var itemIds = pointBase.Item
                .Where(i => i.RowId != 0 && spearItems.HasRow(i.RowId))
                .Select(i => spearItems.GetRow(i.RowId).Item.RowId)
                .Where(wanted.Contains)
                .ToList();
            var x = MapCoordinates.FromPixel(notebook.X, zone.Map.SizeFactor);
            var y = MapCoordinates.FromPixel(notebook.Y, zone.Map.SizeFactor);
            Add(spots, $"spear:{notebook.RowId}", MaterialSource.Fisher, zone, Text(notebook.PlaceName.ValueNullable?.Name ?? default), x, y, notebook.GatheringLevel, [], itemIds);
        }

        return spots;
    }

    private static void Add(List<GatheringSpot> spots, string key, MaterialSource job, Zone zone, string area, float x, float y, int level, IReadOnlyList<TimeWindow> windows, List<uint> itemIds)
    {
        if (itemIds.Count == 0)
            return;

        var aetheryte = zone.Aetherytes
            .OrderBy(a => ((a.X - x) * (a.X - x)) + ((a.Y - y) * (a.Y - y)))
            .Select(a => a.Name)
            .FirstOrDefault();

        spots.Add(new GatheringSpot(key, job, zone.TerritoryId, zone.Map.RowId, zone.Name, area.Length > 0 ? area : zone.Name, x, y, level, aetheryte, windows, itemIds));
    }

    private Dictionary<uint, Zone> Zones()
    {
        var zones = new Dictionary<uint, Zone>();
        var markers = excel.GetSubrowSheet<MapMarker>();

        foreach (var territory in excel.GetSheet<TerritoryType>())
        {
            if (!RoutableTerritoryUses.Contains(territory.TerritoryIntendedUse.RowId) || territory.Map.ValueNullable is not { } map)
                continue;

            var name = Text(territory.PlaceName.ValueNullable?.Name ?? default);
            if (name.Length == 0)
                continue;

            var aetherytes = new List<Landmark>();
            if (markers.TryGetRow(map.MapMarkerRange, out var mapMarkers))
            {
                foreach (var marker in mapMarkers)
                {
                    if (marker.DataType == AetheryteMarker && marker.DataKey.TryGetValue<Aetheryte>(out var aetheryte) && aetheryte.IsAetheryte)
                    {
                        var label = Text(aetheryte.PlaceName.ValueNullable?.Name ?? default);
                        if (label.Length > 0)
                            aetherytes.Add(new Landmark(label, MapCoordinates.FromPixel(marker.X, map.SizeFactor), MapCoordinates.FromPixel(marker.Y, map.SizeFactor)));
                    }
                }
            }

            zones[territory.RowId] = new Zone(territory.RowId, name, map, aetherytes);
        }

        return zones;
    }

    private static IReadOnlyList<TimeWindow> Windows(GatheringPointTransient transient)
    {
        var windows = new List<TimeWindow>();
        if (transient.GatheringRarePopTimeTable.ValueNullable is { } table)
        {
            for (var i = 0; i < table.StartTime.Count; i++)
            {
                if (table.StartTime[i] != NoTime && table.Duration[i] > 0)
                    windows.Add(new TimeWindow(EorzeaTime.FromGameClock(table.StartTime[i]), EorzeaTime.FromGameClock(table.Duration[i])));
            }
        }

        if (transient.EphemeralStartTime != NoTime && transient.EphemeralStartTime != transient.EphemeralEndTime)
        {
            var start = EorzeaTime.FromGameClock(transient.EphemeralStartTime);
            var end = EorzeaTime.FromGameClock(transient.EphemeralEndTime);
            windows.Add(new TimeWindow(start, (end - start + 1440) % 1440));
        }

        return windows;
    }

    private static bool IsItemId(uint id) => id is not 0 and not uint.MaxValue;

    private static string Text(ReadOnlySeString value) => GameText.Clean(value.ExtractText());

    private sealed record Landmark(string Name, float X, float Y);

    private sealed record Zone(uint TerritoryId, string Name, Map Map, IReadOnlyList<Landmark> Aetherytes);
}
