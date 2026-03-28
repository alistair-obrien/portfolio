public class ItemPlacementPresentation
{
    public readonly ItemId ItemId;
    public readonly ItemPresentation ItemPresentation;

    public readonly CellFootprint CellFootprint;

    public ItemPlacementPresentation(
        GameInstance gameAPI,
        ItemId itemId, 
        CellPosition cellPos)
    {
        ItemId = itemId;

        if (gameAPI.Databases.TryGetModel(itemId, out Item item))
            ItemPresentation = new ItemPresentation(gameAPI, itemId);

        CellFootprint = new CellFootprint(cellPos, item.SizeInInventory);
    }

    public ItemPlacementPresentation(GameInstance gameAPI, ItemPlacementComponent itemPlacementSnapshot)
        : this(
            gameAPI,
            itemPlacementSnapshot.ItemId,
            itemPlacementSnapshot.CellFootprint.Position
        )
    {
    }

    public ItemPlacementPresentation(GameInstance gameAPI, InventoryLocation inventoryLocation, ItemId itemId)
    : this(
        gameAPI,
        itemId,
        inventoryLocation.CellPosition
    )
    {
    }
}