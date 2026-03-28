public class MapTilePresentation
{
    public readonly MapChunkId MapId;
    public readonly string Id;
    public readonly CellFootprint CellFootprint;
    public readonly RenderKey RenderKey;

    public MapTilePresentation(TilePlacementOnMap tile)
    {
        MapId = tile.MapId;
        Id = $"{tile.MapId}->({tile.Footprint.X},{tile.Footprint.Y})";
        CellFootprint = new CellFootprint(new CellPosition(tile.Footprint.X, tile.Footprint.Y), new CellSize(1, 1));
        RenderKey = tile.MapTile.RenderKey;
    }
}