namespace Content.Server.Imperial.Medieval.Procedural;

/// <summary>
/// Used for setting params for dungeon to generate.
/// </summary>
public record struct DungeonGenerationParams
{
    /// <summary>
    /// Count of rooms, which sets width of the dungeon.
    /// </summary>
    public int Width;

    /// <summary>
    /// Count of rooms, which sets height of the dungeon.
    /// </summary>
    public int Height;

    /// <summary>
    /// Roomsize, which can be different for different dungeons. In one dungeon roomsize is similar for
    /// </summary>
    public Vector2i RoomSize;

    public DungeonGenerationParams(int width, int height, Vector2i roomSize)
    {
        Width = width;
        Height = height;
        RoomSize = roomSize;
    }
}
