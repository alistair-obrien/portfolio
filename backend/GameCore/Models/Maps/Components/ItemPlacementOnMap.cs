using System.Collections.Generic;

public class ItemPlacementOnMap : BaseEntity, 
    IMapPlacement,
    IHasGameDbResolvableReferences
{
    public MapPlacementSaveData Save() => new MapPlacementSaveData(this);

    public ItemId ItemId { get; private set; }
    public CellFootprint Footprint { get; }

    public IGameDbId Id => ItemId;

    public ItemPlacementOnMap(
        ItemId itemId, 
        CellFootprint footprint)
    {
        ItemId = itemId;
        Footprint = footprint;
    }

    public IMapPlacement WithFootprint(CellFootprint newFootprint)
    {
        return new ItemPlacementOnMap(ItemId, newFootprint);
    }

    // =======================
    // GRAPH
    // =======================
    public List<IGameDbId> GetChildIdReferences()
    {
        return new List<IGameDbId> { ItemId };
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        if (idMap.TryGetValue(Id, out ITypedStringId newId))
        {
            ItemId = (ItemId)newId;
        }
    }
}