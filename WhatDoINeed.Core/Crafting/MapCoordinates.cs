namespace WhatDoINeed.Core.Crafting;

public static class MapCoordinates
{
    public static float FromWorld(float world, int offset, int sizeFactor) =>
        FromPixel(((world + offset) * Scale(sizeFactor)) + 1024f, sizeFactor);

    public static float FromPixel(float pixel, int sizeFactor) =>
        (41f / Scale(sizeFactor) * pixel / 2048f) + 1f;

    private static float Scale(int sizeFactor) => (sizeFactor <= 0 ? 100 : sizeFactor) / 100f;
}
