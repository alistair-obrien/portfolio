using System;
using System.Collections.Generic;

/// <summary>
/// Handles finding items and determining their locations in the game world.
/// </summary>
public sealed class EntityLocationAPI : APIDomain
{
    public EntityLocationAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    internal bool TryFindEntityLocation(IGameDbId entityId, out IGameModelLocation entityLocation)
    {
        return GameAPI.Databases.TryGetLocationOfModel(entityId, out entityLocation);
    }

    internal bool TryGetEntityFromLocation(IGameModelLocation location, out IGameDbId entityId)
    {
        entityId = null;

        if (location is InventoryLocation inventoryLocation)
        {
            if (!TryResolve(inventoryLocation.InventoryItemId, out Item inventoryItem))
                return false;

            if (!inventoryItem.Inventory.TryGetItemPlacementAt(inventoryLocation.CellPosition, out var placement))
                return false;

            entityId = placement.ItemId;
            return true;
        }
        else if (location is GunAmmoLocation gunAmmoLocation)
        {
            if (!TryResolve(gunAmmoLocation.GunItemId, out Item gunItem))
                return false;

            if (!gunItem.Gun.TryGetLoadedAmmo(out var ammoId))
                return false;

            entityId = ammoId;
            return true;
        }
        else if (location is AttachedLocation equippedLocation)
        {
            if (equippedLocation.EntityId is not CharacterId characterId)
                return false;

            if (!TryResolve(characterId, out Character character))
                return false;

            if (!character.TryGetItemInSlot(equippedLocation.SlotPath, out var itemId))
                return false;

            entityId = itemId;
            return true;
        }
        else if (location is MapLocation mapLocation)
        {
            if (!TryResolve(mapLocation.MapId, out MapChunk map))
                return false;

            if (!map.TryGetPlacementAt(mapLocation.CellFootprint.Position, out var placement))
                return false;

            entityId = placement.Id;
            return true;
        }
        else if (location is RootLocation rootLocation)
        {
            entityId = rootLocation.EntityId;
            return true;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    internal bool TryFindCharacterLocation(CharacterId characterId, out ICharacterLocation characterLocation)
    {
        return GameAPI.Databases.TryGetLocationOfModel(characterId, out characterLocation);
    }

    internal bool HasSpaceInCharacterInventory(CharacterId characterId, ItemId itemId)
    {
        if (!TryResolve(characterId, out Character character))
            return false;

        if (!character.HasInventory())
            return false;

        if (!HasSpaceInInventory(character.InventoryItemId.Value, itemId))
            return false;

        return true;
    }

    internal bool HasSpaceInInventory(ItemId inventoryId, ItemId itemId)
    {
        return GameAPI.Databases.TryGetModel(inventoryId, out Item inv)
            && inv.GetIsInventory()
            && GameAPI.Databases.TryGetModel(itemId, out Item item)
            && inv.Inventory.TryGetSpaceForItem(item, false, out _);
    }

    internal bool TryFindNearestAvailableMapTileForItem(CharacterId characterId, ItemId itemId, out MapLocation availableTile)
    {
        availableTile = default;

        if (!TryFindCharacterLocation(characterId, out var charLoc))
            return false;

        if (charLoc is not MapLocation worldLoc)
            return false;

        if (!TryFindFirstAvailableMapTileForItem(itemId, worldLoc, out availableTile))
            return false;

        return true;
    }

    internal bool TryFindFirstAvailableMapTileForItem(ItemId itemId, MapLocation fromTile, out MapLocation availableTile)
    {
        availableTile = default;

        if (!TryResolve(itemId, out Item item))
            return false;

        if (!TryResolve(fromTile.MapId, out MapChunk map))
            return false;

        if (!map.FindFirstAvailableMapTileForItem(itemId, fromTile, out availableTile))
            return false;

        return true;
    }

    private bool TryFindCharacterFromEquippedItem(ItemId itemId, out CharacterId? characterId)
    {
        characterId = null;

        if (!TryFindEntityLocation(itemId, out var location))
            return false;

        if (location is not AttachedLocation equippedLocation)
            return false;

        if (equippedLocation.EntityId is not CharacterId character)
            return false;

        characterId = character;
        return true;
    }
}