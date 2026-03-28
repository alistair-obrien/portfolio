using System.Collections.Generic;

public class PropPlacementOnMap : BaseEntity, 
    IMapPlacement,
    IHasGameDbResolvableReferences
{
    public PropId PropId { get; private set; }
    public CellFootprint Footprint { get; }

    public IGameDbId Id => PropId;

    public MapPlacementSaveData Save() => new MapPlacementSaveData(this);

    public PropPlacementOnMap(PropId propId, CellFootprint footprint)
    {
        PropId = propId;
        Footprint = footprint;
    }

    public IMapPlacement WithFootprint(CellFootprint newFootprint)
    {
        return new PropPlacementOnMap(PropId, newFootprint);
    }

    public List<IGameDbId> GetChildIdReferences()
    {
        return new List<IGameDbId> { PropId };
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        if (idMap.TryGetValue(Id, out ITypedStringId newId))
        {
            PropId = (PropId)newId;
        }
    }
}