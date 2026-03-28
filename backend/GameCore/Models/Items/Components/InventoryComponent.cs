using System;
using System.Collections.Generic;
using System.Linq;

public class InventoryBlueprint
{
    public int Columns;
    public int Rows;
    public List<ItemPlacementSaveData> ItemPlacementSaveDatas;

    public InventoryBlueprint()
    {
        Columns = 8;
        Rows = 8;
        ItemPlacementSaveDatas = new();
    }
}

public sealed class InventoryComponent : ItemComponent, IHasGameDbResolvableReferences
{
    public InventoryBlueprint Save()
    {
        List<ItemPlacementSaveData> placementSaveData = new List<ItemPlacementSaveData>();

        foreach (var item in ItemPlacements)
        {
            placementSaveData.Add(item.Save());
        }

        return new InventoryBlueprint
        {
            Columns = Columns,
            Rows = Rows,
            ItemPlacementSaveDatas = placementSaveData
        };
    }

    public readonly int Columns;
    public readonly int Rows;
    public List<ItemPlacementComponent> ItemPlacements { get; private set; }

    public InventoryComponent() { }
    public InventoryComponent(
        int columns,
        int rows,
        List<ItemPlacementComponent> itemPlacements) : base()
    {
        this.Columns = columns;
        this.Rows = rows;
        this.ItemPlacements = itemPlacements;
    }

    public InventoryComponent(InventoryBlueprint inventory)
    {
        Columns = Math.Max(1, inventory.Columns);
        Rows = Math.Max(1, inventory.Rows);
        ItemPlacements = new(); // Dont populate yet. We will attach properly later
    }

    private bool IsSpaceOpen(
        CellFootprint cellFootprint,
        ItemId? selfItemId,
        bool allowSingleOverlap)
    {
        // bounds check
        if (cellFootprint.X < 0 || cellFootprint.Y < 0 ||
            cellFootprint.X + cellFootprint.Width > Columns ||
            cellFootprint.Y + cellFootprint.Height > Rows)
        {
            return false;
        }

        // target rectangle
        int x1 = cellFootprint.X;
        int y1 = cellFootprint.Y;
        int x2 = x1 + cellFootprint.Width;
        int y2 = y1 + cellFootprint.Height;

        int overlapsCount = 0;
        ItemPlacementComponent overlappingPlacement = null;
        foreach (var placement in ItemPlacements)
        {
            var placedItem = placement.ItemId;

            // ignore the item we're moving (UID check = safest)
            if (selfItemId != null && placedItem == selfItemId)
                continue;

            // placed rectangle
            int px1 = placement.CellFootprint.X;
            int py1 = placement.CellFootprint.Y;
            int px2 = px1 + placement.CellFootprint.Width;
            int py2 = py1 + placement.CellFootprint.Height;

            // overlap?
            bool overlaps =
                x1 < px2 &&
                x2 > px1 &&
                y1 < py2 &&
                y2 > py1;

            if (!overlaps) { continue; }

            if (!allowSingleOverlap)
                return false;

            overlapsCount++;
            overlappingPlacement = placement;

            if (overlapsCount > 1)
                return false;
        }

        return true;
    }

    internal bool TryGetSpaceForItem(Item item, bool allowSingleOverlap, out CellFootprint cellFootprint)
    {
        // Early out: item is bigger than inventory
        if (item.SizeInInventory.Width > Columns || item.SizeInInventory.Height > Rows)
        {
            cellFootprint = new CellFootprint(new CellPosition(-1, -1), item.SizeInInventory);
            return false;
        }

        for (int y = 0; y <= Rows - item.SizeInInventory.Height; y++)
        {
            for (int x = 0; x <= Columns - item.SizeInInventory.Width; x++)
            {
                if (IsSpaceOpen(new CellFootprint(x, y, item.SizeInInventory.Width, item.SizeInInventory.Height), item.Id, allowSingleOverlap))
                {
                    cellFootprint = new CellFootprint(new CellPosition(x, y), item.SizeInInventory);
                    return true;
                }
            }
        }

        cellFootprint = new CellFootprint(new CellPosition(-1, -1), item.SizeInInventory);
        return false;
    }

    internal bool TryAddItem(
        Item item,
        bool allowSingleOverlap,
        CellPosition cellPosition,
        out ItemPlacementComponent itemPlacement,
        out ItemId? displacedItemId)
    {
        allowSingleOverlap = false; //TEMP

        itemPlacement = null;
        displacedItemId = null;

        CellFootprint cellFootprint = new CellFootprint(cellPosition, item.SizeInInventory);
        // Allow auto-find position
        if (!CanPlaceItem(item, allowSingleOverlap, ref cellFootprint))
            return false;

        // Remove any existing placement of THIS item first
        if (TryGetItemPlacement(item.Id, out var _))
        {
            TryRemoveItem(item.Id, out _);
        }

        // Collect ALL overlapping items for the new rectangle
        var overlaps = new HashSet<ItemPlacementComponent>();

        for (int yy = cellFootprint.Y; yy < cellFootprint.Y + item.SizeInInventory.Height; yy++)
        {
            for (int xx = cellFootprint.X; xx < cellFootprint.X + item.SizeInInventory.Width; xx++)
            {
                if (TryGetItemPlacementAt(new CellPosition(xx, yy), out var p))
                    overlaps.Add(p);
            }
        }

        // If more than one and overlap not allowed, fail (this matches CanPlace logic)
        if (!allowSingleOverlap && overlaps.Count > 0)
            return false;

        if (allowSingleOverlap && overlaps.Count > 1)
            return false;

        if (overlaps.Count == 1)
        {
            displacedItemId = overlaps.First().ItemId;
        }

        // Displace them // IMPORTANT: Make sure these are put somewhere sensible
        foreach (var p in overlaps)
        {
            TryRemoveItem(p.ItemId, out var displacedPlacement);
        }

        if (!CanPlaceItem(item, allowSingleOverlap, ref cellFootprint))
        {
            return false;
        }

        itemPlacement = new ItemPlacementComponent(item, cellPosition);
        ItemPlacements.Add(itemPlacement);
        return true;
    }

    internal bool TryRemoveItem(ItemId itemUid, out ItemPlacementComponent itemPlacement)
    {
        if (!TryGetItemPlacement(itemUid, out itemPlacement))
            return false;

        ItemPlacements.Remove(itemPlacement);
        return true;
    }


    internal bool TryAddItemToFirstFreeSpot(Item itemData, out ItemPlacementComponent itemPlacement, out ItemId? displacedItemId)
        => TryAddItem(itemData, allowSingleOverlap: false, new CellPosition(-1, -1), out itemPlacement, out displacedItemId);

    public bool CanPlaceItem(Item itemData, bool allowSingleOverlap, CellPosition cellPosition)
    {
        CellFootprint cellFootprint = new CellFootprint(cellPosition, itemData.SizeInInventory);
        return CanPlaceItem(itemData, allowSingleOverlap, ref cellFootprint);
    }

    public bool CanPlaceItem(Item itemData, bool allowSingleOverlap, ref CellFootprint cellFootprint)
    {
        // If coords are -1 just find first available place
        if (cellFootprint.X < 0 || cellFootprint.Y < 0)
        {
            return TryGetSpaceForItem(itemData, allowSingleOverlap, out cellFootprint);
        }

        return IsSpaceOpen(new CellFootprint(cellFootprint.Position, itemData.SizeInInventory), itemData.Id, allowSingleOverlap);
    }

    public bool TryGetItemAt(CellPosition cellPosition, out ItemId item)
    {
        if (TryGetItemPlacementAt(cellPosition, out var placement))
        {
            item = placement.ItemId;
            return true;
        }

        item = default;
        return false;
    }

    public bool TryGetItemPlacementAt(CellPosition cellPosition, out ItemPlacementComponent data)
    {
        data = default;

        foreach (var item in ItemPlacements)
        {
            if (cellPosition.X >= item.CellFootprint.X &&
                cellPosition.Y >= item.CellFootprint.Y &&
                cellPosition.X < item.CellFootprint.X + item.CellFootprint.Width &&
                cellPosition.Y < item.CellFootprint.Y + item.CellFootprint.Height)
            {
                data = item;
                return true;
            }
        }

        return false;
    }

    public bool TryGetItemPlacement(ItemId itemId, out ItemPlacementComponent itemPlacement)
    {
        itemPlacement = ItemPlacements.FirstOrDefault(p => p.ItemId == itemId);
        return itemPlacement != default;
    }

    internal bool ContainsItem(ItemId itemId)
    {
        foreach (var placement in ItemPlacements)
        {
            if (placement.ItemId == itemId)
            {
                return true;
            }

            // This belongs one layer above
            //if (placement.ItemId.Value.Inventory != null)
            //{
            //    if (placement.ItemId.Value.Inventory.ContainsItem(uid))
            //    {
            //        return true;
            //    }
            //}
        }

        return false;
    }

    // TODO:
    public bool CharacterHasTakePermissions(string characterUid)
    {
        //Debug.LogError($"[{characterUid}] did not have take permssions.");
        return true;
    }

    // TODO:
    public bool CharacterHasPlacePermissions(string characterUid)
    {
        //Debug.LogError($"[{characterUid}] did not have take permssions.");
        return true;
    }

    internal bool TryGetItemPosition(ItemId itemId, out CellPosition cellPosition)
    {
        if (TryGetItemPlacement(itemId, out var placement))
        {
            cellPosition = placement.CellFootprint.Position;
            return true;
        }

        cellPosition = new CellPosition(-1, -1);
        return false;
    }

    internal HashSet<ISlotId> GetCompatibleSlotPaths()
    {
        return new HashSet<ISlotId>
        {
            SlotIds.Loadout.Inventory,
        };
    }

    public List<IGameDbId> GetChildIdReferences()
    {
        List<IGameDbId> ids = new();

        foreach (var placement in ItemPlacements)
        {
            ids.AddRange(placement.GetChildIdReferences());
        }

        return ids;
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        foreach (var placement in ItemPlacements)
        {
            placement.RemapIds(idMap);
        }
    }
}