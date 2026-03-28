using Newtonsoft.Json;

public class MapGrid : BaseEntity
{
    [JsonProperty] public int Width;
    [JsonProperty] public int Height;
    [JsonProperty] public bool[,] Walkable;

    [JsonConstructor] public MapGrid() { }
    public MapGrid(int width, int height, bool[,] walkable)
    {
        Width = width;
        Height = height;
        Walkable = walkable;

        // Example: everything is walkable; define real logic yourself
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Walkable[x, y] = true;
    }

    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        return Walkable[x, y];
    }
}