public class PlacementOnMapSaveData
{
    public IGameDbId Id;
    public int X;
    public int Y;
    public int Width;
    public int Height;
}

public interface IMapPlacement : IGridPlacement
{
    IGameDbId Id { get; }

    MapPlacementSaveData Save();
    IMapPlacement WithFootprint(CellFootprint newFootprint);
}

public interface IGridPlacement
{
    public CellFootprint Footprint { get; }
}

public interface IMapPlaceable
{
    IGameDbId Id { get; }
    CellSize SizeOnMap { get; }
}