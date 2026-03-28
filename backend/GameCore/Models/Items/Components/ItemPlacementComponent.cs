using System.Collections.Generic;

public class ItemPlacementSaveData
{
    public ItemId ItemId;
    public CellFootprint CellFootprint;
    
    public ItemPlacementSaveData()
    {

    }
}

public sealed class ItemPlacementComponent : BaseEntity, IHasGameDbResolvableReferences
{
    public ItemPlacementSaveData Save()
    {
        return new ItemPlacementSaveData
        {
            ItemId = ItemId,
            CellFootprint = CellFootprint
        };
    }

    public ItemId ItemId { get; private set; }
    public readonly CellFootprint CellFootprint;

    public ItemPlacementComponent(Item item, CellPosition cellPosition)
    {
        ItemId = item.Id;
        CellFootprint = new CellFootprint(cellPosition, item.SizeInInventory);
    }

    public ItemPlacementComponent(ItemId itemId, CellFootprint cellFootprint)
    {
        ItemId = itemId;
        CellFootprint = cellFootprint;
    }

    public List<IGameDbId> GetChildIdReferences()
    {
        return new List<IGameDbId>
        {
            ItemId,
        };
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        ItemId = (ItemId)idMap[ItemId];
    }
}