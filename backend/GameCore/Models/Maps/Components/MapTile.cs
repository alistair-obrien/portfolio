using Newtonsoft.Json;
using System;

public class MapTile : BaseEntity
{
    public static MapTile Default => new MapTile(true, new RenderKey("hey"));

    public bool Walkable { get; private set; }
    public RenderKey RenderKey { get; private set; }

    public MapTile(bool walkable, RenderKey renderKey)
    {
        Walkable = walkable;
    }

    internal bool IsDefault()
    {
        return Walkable;
    }
}