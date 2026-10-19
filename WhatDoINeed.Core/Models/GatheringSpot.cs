namespace WhatDoINeed.Core.Models;

public sealed record TimeWindow(int StartMinute, int DurationMinutes)
{
    public bool Contains(int minuteOfDay) => (minuteOfDay - StartMinute + 1440) % 1440 < DurationMinutes;
}

public sealed record GatheringSpot(
    string Key,
    MaterialSource Job,
    uint TerritoryId,
    uint MapId,
    string Zone,
    string Area,
    float X,
    float Y,
    int Level,
    string? Aetheryte,
    IReadOnlyList<TimeWindow> Windows,
    IReadOnlyList<uint> ItemIds)
{
    public bool IsTimed => Windows.Count > 0;
}
