using System;

/// <summary>
/// Handles low-level attach/detach operations for items to different location types.
/// </summary>
public sealed class EntityAttachmentAPI : APIDomain
{
    public EntityAttachmentAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    // TODO: Migrate to generic
    internal bool CanAttachToItem(
        CharacterId characterId, 
        ItemId actingItemId,
        IItemLocation targetItemLocation, 
        bool allowSwap)
    {
        return targetItemLocation switch
        {
            InventoryLocation invLoc => CanAttachToInventory(characterId, invLoc, actingItemId, allowSwap),
            AttachedLocation eqLoc => CanAttachToEquipped(characterId, eqLoc, actingItemId),
            GunAmmoLocation gunLoc => CanAttachToGun(characterId, gunLoc, actingItemId),
            MapLocation worldLoc => CanAttachToWorld(characterId, worldLoc, actingItemId),
            RootLocation rootLocation => true,
            null => true,
            _ => throw new NotImplementedException($"Location type {targetItemLocation?.GetType().Name} not supported")
        };
    }

    // TODO: Migrate to generic
    internal bool CanAttachToItem(
        CharacterId characterId,
        ItemId actingItemId,
        ItemId targetItemId,
        bool allowSwap)
    {
        if (!GameAPI.Databases.TryResolve(targetItemId, out Item targetItem))
            return false;

        IItemLocation location = null;

        if (targetItem.GetIsGun())
            location = new GunAmmoLocation(targetItemId);

        if (targetItem.GetIsInventory())
            location = new InventoryLocation(targetItemId);

        if (location == null) 
            return false;

        return CanAttachToItem(characterId, actingItemId, location, allowSwap);
    }

    public bool TryAttachEntityToLocation(
        CharacterId actorId,
        IGameModelLocation targetLocation,
        IGameDbId entityId, 
        bool allowOverlap)
    {
        if (!GameAPI.Databases.TryResolveUntyped(entityId, out var model))
            return false;

        bool success = targetLocation switch
        {
            InventoryLocation invLoc => TryAttachToInventory(actorId, invLoc, entityId, allowOverlap),
            AttachedLocation eqLoc => TryAttachToEquipped(actorId, eqLoc, entityId),
            GunAmmoLocation gunLoc => TryAttachToGun(actorId, gunLoc, entityId),
            MapLocation worldLoc => TryAttachToWorld(actorId, worldLoc, entityId),
            RootLocation root => TryAttachToRoot(actorId, root, entityId),
            _ => throw new NotImplementedException($"Location type {targetLocation.GetType().Name} not supported")
        };

        if (success)
            model.SetAttachedLocation(targetLocation);

        RaiseEvent(new DatabaseAPI.Events.ModelAttached(entityId, targetLocation));

        return success;
    }

    public bool TryDetachEntityFromLocation(
        CharacterId actorId, 
        IGameModelLocation fromLocation,
        IGameDbId entityId)
    {
        if (!GameAPI.Databases.TryResolveUntyped(entityId, out var model))
            return false;

        bool success = fromLocation switch
        {
            InventoryLocation invLoc => TryDetachFromInventory(actorId, invLoc, entityId),
            AttachedLocation eqLoc => TryDetachFromEquipped(actorId, eqLoc, entityId),
            GunAmmoLocation gunLoc => TryDetachFromGun(actorId, gunLoc, entityId),
            MapLocation worldLoc => TryDetachFromWorld(actorId, worldLoc, entityId),
            RootLocation root => TryDetachFromRoot(actorId, root, entityId),
            null => true,
            _ => throw new NotImplementedException($"Location type {fromLocation.GetType().Name} not supported")
        };

        if (success)
            model.ClearAttachedLocation();

        RaiseEvent(new DatabaseAPI.Events.ModelDetached(entityId, fromLocation));

        return success;
    }

    private bool TryAttachToRoot(CharacterId actorId, RootLocation root, IGameDbId entityId)
    {
        return GameAPI.RootModel.TryAddEntity(entityId);
    }

    private bool TryDetachFromRoot(CharacterId actorId, RootLocation root, IGameDbId entityId)
    {
        return GameAPI.RootModel.TryRemoveEntity(entityId);
    }

    // --- Inventory ---
    private bool CanAttachToInventory(
        CharacterId actorId, 
        InventoryLocation loc,
        IGameDbId entityId, 
        bool allowOverlap)
    {
        if (entityId is not ItemId itemId)
            return false;

        // Can't place self inside self
        if (loc.InventoryItemId == itemId)
            return false;

        if (!TryResolve(loc.InventoryItemId, out Item inv))
            return false;

        if (!inv.GetIsInventory())
            return false;

        if (!TryResolve(itemId, out Item item))
            return false;

        if (!inv.Inventory.CanPlaceItem(item, allowOverlap, loc.CellPosition))
            return false;

        return true;
    }

    private bool TryAttachToInventory(
        CharacterId actorId, 
        InventoryLocation loc, 
        IGameDbId entityId, 
        bool allowSwap)
    {
        if (entityId is not ItemId itemId)
            return false;

        if (!TryResolve(loc.InventoryItemId, out Item inv))
            return false;

        if (!TryResolve(itemId, out Item item))
            return false;

        var inventoryCellFootprint = new CellFootprint(loc.CellPosition, item.SizeInInventory);

        bool addSuccess = inv.Inventory.TryAddItem(
            item,
            allowSwap,
            loc.CellPosition,
            out var itemPlacement,
            out var displacedItemId);
                
        if (addSuccess == false)
            return false;

        // TODO: Try equip to held slot
        if (displacedItemId.HasValue)
        {
            Debug.LogError("HEY. I was displaced: " + displacedItemId.Value);
            RaiseEvent(new ItemsAPI.Events.ItemRemovedFromInventory(
                true, 
                displacedItemId.Value, 
                loc.InventoryItemId));
        }

        RaiseEvent(new ItemsAPI.Events.ItemAddToInventoryResolved(
            true,
            itemId,
            loc.InventoryItemId,
            inventoryCellFootprint,
            new ItemPlacementPresentation(GameAPI, itemPlacement)));

        return true;
    }

    private bool TryDetachFromInventory(
        CharacterId characterId, 
        InventoryLocation loc, 
        IGameDbId entityId)
    {
        if (entityId is not ItemId itemId)
            return false;

        if (!TryResolve(loc.InventoryItemId, out Item inv))
            return false;

        if (!inv.GetIsInventory())
            return false;

        if (!inv.Inventory.TryRemoveItem(itemId, out var itemPlacement))
            return false;


        // ═══════════════════════════════════════════════════════════
        // RAISE EVENT - Item removed from inventory
        // ═══════════════════════════════════════════════════════════
        RaiseEvent(new ItemsAPI.Events.ItemRemovedFromInventory(
            true,
            itemId,
            loc.InventoryItemId));

        return true;
    }

    // --- Equipped ---
    private bool CanAttachToEquipped(
        CharacterId actorId, 
        AttachedLocation location,
        IGameDbId entityId)
    {
        if (entityId is not ItemId itemId)
            return false;

        if (location.EntityId is not CharacterId characterId)
            return false;

        if (!TryResolve(characterId, out Character ch))
            return false;

        if (!TryResolve(itemId, out Item item))
            return false;

        if (!ch.CanEquip(item, location.SlotPath))
            return false;

        return true;
    }

    private bool TryAttachToEquipped(
        CharacterId actorId, 
        AttachedLocation location, 
        IGameDbId entityId)
    {
        if (entityId is not ItemId itemId)
            return false;

        if (location.EntityId is not CharacterId characterId)
            return false;

        if (!TryResolve(characterId, out Character ch))
            return false;

        if (!TryResolve(itemId, out Item item))
            return false;

        if (!ch.TryEquip(item, location.SlotPath))
            return false;

        // ═══════════════════════════════════════════════════════════
        // RAISE EVENT - Item equipped
        // ═══════════════════════════════════════════════════════════
        RaiseEvent(new ItemsAPI.Events.ItemEquipped(
            (CharacterId)location.EntityId,
            itemId,
            location.SlotPath,
            new ItemSlotPresentation(GameAPI, (CharacterId)location.EntityId, location.SlotPath, itemId)));

        return true;
    }

    private bool TryDetachFromEquipped(
        CharacterId actorId, 
        AttachedLocation location, 
        IGameDbId entityId)
    {
        if (entityId is not ItemId itemId)
            return false;

        if (location.EntityId is not CharacterId characterId)
            return false;

        if (!TryResolve(characterId, out Character ch))
            return false;

        if (!TryResolve(itemId, out Item item))
            return false;

        if (!ch.TryUnequip(item, location.SlotPath))
            return false;

        // ═══════════════════════════════════════════════════════════
        // RAISE EVENT - Item equipped
        // ═══════════════════════════════════════════════════════════
        RaiseEvent(new ItemsAPI.Events.ItemUnequipped(
            (CharacterId)location.EntityId,
            item.Id,
            location.SlotPath,
            new ItemSlotPresentation(GameAPI, (CharacterId)location.EntityId, location.SlotPath, itemId)));

        return true;
    }

    // --- Gun Ammo ---
    private bool CanAttachToGun(
        CharacterId actorId, 
        GunAmmoLocation location, 
        IGameDbId entityId)
    {
        if (entityId is not ItemId itemId)
            return false;

        if (!TryResolve(location.GunItemId, out Item gun))
            return false;

        if (!TryResolve(itemId, out Item item))
            return false;

        if (!gun.GetIsGun())
            return false;

        if (!gun.Gun.CanLoadWithAmmo(GameAPI, item))
            return false;

        return true;
    }

    private bool TryAttachToGun(
        CharacterId actorId, 
        GunAmmoLocation location, 
        IGameDbId entityId)
    {
        if (entityId is not ItemId itemId) 
            return false;

        if (!TryResolve(location.GunItemId, out Item gunItem))
            return false;

        if (!gunItem.GetIsGun())
            return false;

        if (!TryResolve(itemId, out Item item))
            return false;

        if (!item.GetIsAmmo())
            return false;

        var gun = gunItem.Gun;

        if (!gun.CanLoadWithAmmo(GameAPI, item))
            return false;

        // Check if gun already has ammo - if so, that ammo will be displaced
        if (gun.TryGetLoadedAmmo(out var existingAmmo))
        {
            var ammoLocation = new GunAmmoLocation(location.GunItemId);
        }

        if (!gun.TryLoadAmmo(itemId))
            return false;

        RaiseEvent(new ItemsAPI.Events.ItemUpdated(
            location.GunItemId,
            new ItemPresentation(GameAPI, location.GunItemId)));

        // ═══════════════════════════════════════════════════════════
        // RAISE EVENT - Gun loaded with ammo
        // ═══════════════════════════════════════════════════════════
        RaiseEvent(new ItemsAPI.Events.GunLoaded(
            location.GunItemId,
            itemId,
            new ItemPresentation(GameAPI, location.GunItemId)));

        return true;
    }

    private bool TryDetachFromGun(
        CharacterId actorId, 
        GunAmmoLocation location, 
        IGameDbId entityId)
    {
        if (entityId is not ItemId itemId)
            return false;

        if (!TryResolve(location.GunItemId, out Item gunItem))
            return false;

        if (!gunItem.GetIsGun())
            return false;

        if (!TryResolve(location.GunItemId, out Item ammoItem))
            return false;

        if (!ammoItem.GetIsAmmo())
            return false;

        if (!gunItem.Gun.TryUnloadAmmo(ammoItem))
            return false;

        // ═══════════════════════════════════════════════════════════
        // RAISE EVENT - Gun unloaded
        // ═══════════════════════════════════════════════════════════
        RaiseEvent(new ItemsAPI.Events.GunUnloaded(
            location.GunItemId,
            itemId,
            new ItemPresentation(GameAPI, location.GunItemId)));

        return true;
    }

    // In WeaponsSection or a specialized handler
    public bool TryLoadGunWithAmmo(
        CharacterId actorId, 
        ItemId gunUid, 
        ItemId ammoUid)
    {
        if (!TryResolve(gunUid, out Item gunItem)) 
            return false;

        if (!TryResolve(ammoUid, out Item ammoItem))
            return false;

        if (!gunItem.GetIsGun() || !ammoItem.GetIsAmmo())
            return false;

        var gunLocation = new GunAmmoLocation(gunUid);
        // If ammo stack is larger than clip size, split it first
        if (ammoItem.CurrentStackCount > gunItem.Gun.ClipSize)
        {
            // Split off just what we need
            if (!GameAPI.Items.ItemStackAPI.SplitStack(
                new ItemsAPI.Commands.SplitStack(
                    actorId,
                    ammoUid,
                    gunLocation,
                    gunItem.Gun.ClipSize)).Ok)
                return false;

            return true; //Not sure
        }
        else
        {
            // Just load the whole stack

            return GameAPI.Databases.EntityMoverAPI.TryMoveEntity(
                new ItemsAPI.Commands.MoveEntity(
                    actorId,
                    ammoUid,
                    gunLocation,
                    AllowSwap: false)).Ok;
        }
    }

    // --- World ---
    private bool CanAttachToWorld(
        CharacterId actorId, 
        MapLocation location, 
        IGameDbId entityId)
    {
        if (!TryResolve(location.MapId, out MapChunk map))
            return false;

        if (!TryResolveUntyped(entityId, out var entity))
            return false;

        if (entity is not IMapPlaceable mapPlaceable)
            return false;

        var intendedFootprint = new CellFootprint(
            location.CellFootprint.Position,
            mapPlaceable.SizeOnMap);

        if (!map.CanAdd(entityId, intendedFootprint))
            return false;

        return true;
    }

    private bool TryAttachToWorld(
        CharacterId actorId, 
        MapLocation location,
        IGameDbId entityId)
    {
        if (!TryResolve(location.MapId, out MapChunk map))
            return false;

        if (!TryResolveUntyped(entityId, out var entity))
            return false;

        bool addResult = map.TryAttach(
            entity, 
            location.CellFootprint.Position,
            out var addedPlacement);

        if (addedPlacement is ItemPlacementOnMap itemPlacement)
        {
            RaiseEvent(
            new MapsAPI.Events.ItemAddToMapResolved(
                addResult,
                map.Id,
                GameAPI.Maps.CreateItemPlacementPresentation(itemPlacement)));
        }
        else if (addedPlacement is PropPlacementOnMap propPlacement)
        {
            RaiseEvent(
            new MapsAPI.Events.PropAddToMapResolved(
                addResult,
                map.Id,
                GameAPI.Maps.CreatePropPlacementPresentation(map.Id, propPlacement.PropId)));

            // Refresh neighbors so they update their wall connections
            GameAPI.Maps.RaisePropUpdatedForNeighborhood(map, propPlacement.Footprint.Position);
        }
        else if (addedPlacement is CharacterPlacementOnMap characterPlacement)
        {
            RaiseEvent(
            new MapsAPI.Events.CharacterAddToMapResolved(
                addResult,
                map.Id,
                GameAPI.Maps.CreateCharacterPlacementPresentation(map.Id, characterPlacement.CharacterId)));
        }

        return addResult;
    }

    private bool TryDetachFromWorld(
        CharacterId actorId, 
        MapLocation location, 
        IGameDbId entityId)
    {
        if (!TryResolve(location.MapId, out MapChunk map))
            return false;

        if (!map.TryRemovePlacement(
                entityId,
                out var removedPlacement))
            return false;

        if (removedPlacement is ItemPlacementOnMap itemPlacement)
        {
            RaiseEvent(
                new MapsAPI.Events.ItemRemovedFromMap(
                    map.Id,
                    itemPlacement.ItemId));
        }
        else if (removedPlacement is CharacterPlacementOnMap characterPlacement)
        {
            RaiseEvent(
                new MapsAPI.Events.CharacterRemovedFromMap(
                    map.Id,
                    characterPlacement.CharacterId));
        }
        else if (removedPlacement is PropPlacementOnMap propPlacement)
        {
            RaiseEvent(
                new MapsAPI.Events.PropRemovedFromMap(
                    map.Id,
                    propPlacement.PropId));

            // Refresh neighbors so they update their wall connections
            GameAPI.Maps.RaisePropUpdatedForNeighborhood(map, propPlacement.Footprint.Position);
        }

        return true;
    }
}
