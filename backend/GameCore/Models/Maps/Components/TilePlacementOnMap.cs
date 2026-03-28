using Newtonsoft.Json;
using System;

public class TilePlacementOnMap : BaseEntity, 
    IHasStringId, 
    IGridPlacement
{
    string IHasStringId.Uid { get => GenerateKey(MapId, Footprint.X, Footprint.Y); set { } }

    readonly public MapChunkId MapId; // Shouldnt be here
    readonly public MapTile MapTile;
    public CellFootprint Footprint { get; }

    public TilePlacementOnMap(MapChunkId mapUid, int xPos, int yPos, MapTile mapTile)
    {
        MapId = mapUid;
        MapTile = mapTile;
        Footprint = new CellFootprint(xPos, yPos, 1, 1);
    }

    public static string GenerateKey(MapChunkId mapUid, int xPos, int yPos)
    {
        return $"{mapUid} x:{xPos} y:{yPos}";
    }
}