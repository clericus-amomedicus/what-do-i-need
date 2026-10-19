namespace WhatDoINeed.Core.Models;

public sealed record SpotVisit(GatheringSpot Spot, IReadOnlyList<MaterialNeed> Items);

public sealed record LocationStop(uint TerritoryId, string Zone, IReadOnlyList<SpotVisit> Spots);

public sealed record LocationPlan(IReadOnlyList<LocationStop> Stops, IReadOnlyList<MaterialNeed> Unlocated)
{
    public static readonly LocationPlan Empty = new([], []);

    public bool IsEmpty => Stops.Count == 0 && Unlocated.Count == 0;
}
