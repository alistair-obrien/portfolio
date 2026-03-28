using System.Collections.Generic;

public class InventoryPresentation
{
    public readonly ItemPresentation InventoryItem;
    public readonly int Rows;
    public readonly int Columns;
    public readonly IReadOnlyList<ItemPlacementPresentation> ItemPlacements;

    public InventoryPresentation(GameInstance gameAPI, ItemId? inventoryItemUid)
    {
        if (inventoryItemUid == null) { return; }

        InventoryItem = new ItemPresentation(gameAPI, inventoryItemUid.Value);

        if (!gameAPI.Databases.TryGetModel(inventoryItemUid.Value, out Item inventoryItem))
            return;

        Rows = inventoryItem.Inventory.Rows;
        Columns = inventoryItem.Inventory.Columns;

        List<ItemPlacementPresentation> itemPlacements = new();
        foreach (var itemPlacement in inventoryItem.Inventory.ItemPlacements)
        {
            itemPlacements.Add(new ItemPlacementPresentation(gameAPI, itemPlacement));
        }
        ItemPlacements = itemPlacements;
    }

    public InventoryPresentation(int rows, int columns)
    {
        Rows = rows; 
        Columns = columns;

        ItemPlacements = new List<ItemPlacementPresentation>();
    }

    internal static InventoryPresentation MakeDummyData()
    {
        return new InventoryPresentation(6, 8);
    }
}