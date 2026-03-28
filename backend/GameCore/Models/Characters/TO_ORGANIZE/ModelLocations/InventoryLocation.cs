using Newtonsoft.Json;

public struct InventoryLocation : IItemLocation
{
    public ItemId InventoryItemId { get;  private set; }
    public CellPosition CellPosition { get; private set; }

    public IGameDbId OwnerEntityId => InventoryItemId;

    // -1 -1 will place in first available space
    public InventoryLocation(ItemId inventoryUid) : this(inventoryUid, new CellPosition(-1, -1)) { }
    public InventoryLocation(ItemId inventoryUid, CellPosition cellPosition)
    {
        InventoryItemId = inventoryUid;
        CellPosition = cellPosition;
    }

    public override string ToString()
    {
        return $"InventoryLocation(inventoryItem:{InventoryItemId} Pos:{CellPosition})";
    }
}