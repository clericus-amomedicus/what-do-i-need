using WhatDoINeed.Core.Models;

namespace WhatDoINeed.Core.Crafting;

public sealed class LocationPlanner
{
    private readonly RecipeIndex index;

    public LocationPlanner(RecipeIndex index)
    {
        this.index = index;
    }

    public LocationPlan Plan(MaterialPlan plan)
    {
        var needs = plan.Materials
            .Where(m => m.Item.Source is MaterialSource.Miner or MaterialSource.Botanist or MaterialSource.Fisher)
            .ToList();
        if (needs.Count == 0)
            return LocationPlan.Empty;

        var byItem = needs.ToDictionary(n => n.Item.ItemId);
        var unlocated = needs.Where(n => index.GetSpots(n.Item.ItemId).Count == 0).ToList();

        var zones = needs
            .SelectMany(n => index.GetSpots(n.Item.ItemId).Select(s => (Spot: s, n.Item.ItemId)))
            .GroupBy(x => x.Spot.TerritoryId)
            .Select(g => (Candidate: g.First().Spot, Items: g.Select(x => x.ItemId).ToHashSet()))
            .OrderBy(z => z.Candidate.Zone, StringComparer.OrdinalIgnoreCase)
            .ThenBy(z => z.Candidate.TerritoryId)
            .ToList();

        var stops = new List<LocationStop>();
        foreach (var (zone, items) in Cover(byItem.Keys.ToHashSet(), zones))
        {
            var spots = items
                .SelectMany(id => index.GetSpots(id).Where(s => s.TerritoryId == zone.TerritoryId).Select(s => (Spot: s, ItemId: id)))
                .GroupBy(x => x.Spot.Key)
                .Select(g => (Candidate: g.First().Spot, Items: g.Select(x => x.ItemId).ToHashSet()))
                .OrderBy(s => s.Candidate.IsTimed)
                .ThenBy(s => s.Candidate.Level)
                .ThenBy(s => s.Candidate.Key, StringComparer.Ordinal)
                .ToList();

            var visits = Cover(items, spots)
                .Select(v => new SpotVisit(v.Candidate, v.Items.Select(id => byItem[id]).OrderBy(n => n.Item.Name, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();
            stops.Add(new LocationStop(zone.TerritoryId, zone.Zone, visits));
        }

        return new LocationPlan(stops, unlocated);
    }

    private static List<(T Candidate, HashSet<uint> Items)> Cover<T>(HashSet<uint> wanted, IReadOnlyList<(T Candidate, HashSet<uint> Items)> candidates)
    {
        var remaining = new HashSet<uint>(wanted);
        var chosen = new List<(T, HashSet<uint>)>();

        while (remaining.Count > 0)
        {
            var best = -1;
            var bestCount = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                var count = candidates[i].Items.Count(remaining.Contains);
                if (count > bestCount)
                {
                    best = i;
                    bestCount = count;
                }
            }

            if (best < 0)
                break;

            var covered = candidates[best].Items.Where(remaining.Contains).ToHashSet();
            remaining.ExceptWith(covered);
            chosen.Add((candidates[best].Candidate, covered));
        }

        return chosen;
    }
}
