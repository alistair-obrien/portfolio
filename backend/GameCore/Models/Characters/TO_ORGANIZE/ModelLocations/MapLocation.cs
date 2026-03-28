public struct RootLocation : IItemLocation, ICharacterLocation, IPropLocation
{
    public IGameDbId OwnerEntityId => null;
    public IGameDbId EntityId;

    public RootLocation(IGameDbId entityId)
    {
        EntityId = entityId;
    }
}

public struct MapLocation: IItemLocation, ICharacterLocation, IPropLocation
{
    public MapChunkId MapId;
    public CellFootprint CellFootprint;

    public MapLocation(MapChunkId mapId, CellFootprint cellFootprint)
    {
        MapId = mapId;
        CellFootprint = cellFootprint;
    }

    public IGameDbId OwnerEntityId => MapId;

    public override string ToString()
    {
        return $"WorldLocation(map:{MapId} x:{CellFootprint.X} y:{CellFootprint.Y} W: {CellFootprint.Width} H: {CellFootprint.Height})";
    }
}