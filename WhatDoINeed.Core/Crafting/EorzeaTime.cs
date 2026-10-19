namespace WhatDoINeed.Core.Crafting;

public static class EorzeaTime
{
    public static int MinuteOfDay(DateTimeOffset now) =>
        (int)((now.ToUnixTimeMilliseconds() * 144 / 7 / 60_000) % 1440);

    public static int FromGameClock(int hhmm) => (hhmm / 100 * 60) + (hhmm % 100);

    public static string Format(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
}
