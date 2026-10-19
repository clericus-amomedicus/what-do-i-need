namespace WhatDoINeed.Core.Crafting;

public static class GameText
{
    public static string Clean(string value) =>
        new string(value.Where(c => c is < '' or > '').ToArray()).Trim();
}
