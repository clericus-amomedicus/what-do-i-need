using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace WhatDoINeed;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public List<MarkedRecipe> MarkedRecipes { get; set; } = [];
}

[Serializable]
public class MarkedRecipe
{
    public uint RecipeId { get; set; }
    public int Amount { get; set; } = 1;
}
