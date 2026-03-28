using System.Collections.Generic;

public sealed partial class ItemsAPI : APIDomain
{
    public readonly ItemStackAPI ItemStackAPI;

    private EntityMoverAPI EntityMoverAPI => GameAPI.Databases.EntityMoverAPI;
    public EntityLocationAPI EntityLocationAPI => GameAPI.Databases.EntityLocationAPI;


    private readonly HashSet<ItemId> _openInventories = new();

    public ItemsAPI(GameInstance gameAPI) : base(gameAPI)
    {
        ItemStackAPI = new ItemStackAPI(gameAPI);
    }

    internal override void RegisterHandlers(CommandRouter router)
    {
        //// SPAWNING
        //router.Register<Commands.SpawnItemFromTemplate>(TrySpawnItemFromTemplate);

        // MOVEMENT
        router.Register<Commands.MoveEntity>(TryMoveItem);
        router.Register<Commands.TakeItem>(TryTakeItem);
        router.Register<Commands.DropItem>(TryDropItem);

        // EQUIPMENT
        router.Register<Commands.EquipItem>(TryEquipItem);
        router.Register<Commands.UnequipItem>(TryUnequipItem);

        // WEAPONS
        router.Register<Commands.LoadGun>(TryLoadGun);
        router.Register<Commands.UnloadGun>(TryUnloadGun);

        // STACKS
        router.Register<Commands.SplitStack>(TrySplitStack);
        router.Register<Commands.MergeStack>(TryMergeStack);

        // INTERACTION
        router.Register<Commands.UseItemOnLocation>(TryUseItem);

        // CONTAINER
        router.Register<Commands.OpenInventory>(TryOpenInventory);
        router.Register<Commands.CloseInventory>(TryCloseInventory);
    }

    private CommandResult TryMoveItem(Commands.MoveEntity cmd)
    {
        return GameAPI.Databases.EntityMoverAPI.TryMoveEntity(cmd);
    }

    private CommandResult TryTakeItem(Commands.TakeItem cmd)
    {
        if (!TryResolve(cmd.ActorId, out Character character))
            return Fail($"Could not resolve actor {cmd.ActorId} to take item {cmd.ItemId}.");

        if (!TryGetValidInventoryLocation(character, out var inventoryLocation))
            return Fail($"Actor {cmd.ActorId} does not have a valid inventory to receive item {cmd.ItemId}.");

        return EntityMoverAPI.TryMoveEntity(
            new Commands.MoveEntity(cmd.ActorId, cmd.ItemId, inventoryLocation, false));
    }

    private CommandResult TryDropItem(Commands.DropItem cmd)
    {
        if (!EntityLocationAPI.TryFindNearestAvailableMapTileForItem(cmd.ActorId, cmd.ItemId, out var dropLocation))
            return Fail($"Could not find a valid drop location for item {cmd.ItemId} near actor {cmd.ActorId}.");

        return EntityMoverAPI.TryMoveEntity(
            new Commands.MoveEntity(cmd.ActorId, cmd.ItemId, dropLocation, false));
    }

    private CommandResult TryEquipItem(Commands.EquipItem cmd)
    {
        var equipLocation = new AttachedLocation(cmd.ActorId, cmd.EquipmentSlotId);

        // ItemEquipped event raised inside ItemAttachmentAPI
        return EntityMoverAPI.TryMoveEntity(
            new Commands.MoveEntity(cmd.ActorId, cmd.ItemId, equipLocation, false));
    }

    private CommandResult TryUnequipItem(Commands.UnequipItem cmd)
    {
        if (!TryResolve(cmd.ActorId, out Character character))
            return Fail($"Could not resolve actor {cmd.ActorId} to unequip item {cmd.ItemId}.");

        if (!TryGetValidInventoryLocation(character, out var inventoryLocation))
            return Fail($"Actor {cmd.ActorId} does not have a valid inventory to unequip item {cmd.ItemId} into.");

        return EntityMoverAPI.TryMoveEntity(
            new Commands.MoveEntity(cmd.ActorId, cmd.ItemId, inventoryLocation, false));
    }

    public CommandResult TryLoadGun(Commands.LoadGun cmd)
    {
        var target = new GunAmmoLocation(cmd.GunItemId);

        // GunLoaded event raised inside ItemAttachmentAPI
        return EntityMoverAPI.TryMoveEntity(
            new Commands.MoveEntity(cmd.ActorId, cmd.AmmoItemId, target, false));
    }

    private CommandResult TryUnloadGun(Commands.UnloadGun cmd)
    {
        if (!GameAPI.Databases.TryGetModel(cmd.ActorId, out Character character))
            return Fail($"Could not resolve actor {cmd.ActorId} to unload gun {cmd.GunItemId}.");

        if (!GameAPI.Databases.TryGetModel(cmd.GunItemId, out Item gun))
            return Fail($"Could not resolve gun {cmd.GunItemId}.");

        if (!gun.GetIsGun() || !gun.Gun.TryGetLoadedAmmo(out var ammoItemId))
            return Fail($"Item {cmd.GunItemId} is not a loaded gun.");

        if (!TryGetValidInventoryLocation(character, out var inventoryLocation))
            return Fail($"Actor {cmd.ActorId} does not have a valid inventory to receive unloaded ammo from {cmd.GunItemId}.");

        return EntityMoverAPI.TryMoveEntity(
            new Commands.MoveEntity(cmd.ActorId, ammoItemId, inventoryLocation, false));
    }

    private CommandResult TrySplitStack(Commands.SplitStack cmd) => ItemStackAPI.SplitStack(cmd);

    private CommandResult TryMergeStack(Commands.MergeStack cmd) => ItemStackAPI.MergeStack(cmd);

    private CommandResult TryUseItem(Commands.UseItemOnLocation cmd) => GameAPI.Interactions.UseItem(cmd);

    private CommandResult TryOpenInventory(Commands.OpenInventory cmd)
    {
        if (!TryResolve(cmd.ActorId, out Character character))
            return Fail($"Could not resolve actor {cmd.ActorId} to open inventory {cmd.InventoryItemId}.");

        if (!TryResolve(cmd.InventoryItemId, out Item inventory))
            return Fail($"Could not resolve inventory item {cmd.InventoryItemId}.");

        if (!inventory.GetIsInventory())
            return Fail($"Item {cmd.InventoryItemId} is not an inventory.");

        _openInventories.Add(cmd.InventoryItemId);

        RaiseEvent(new Events.InventoryOpened(cmd.ActorId, cmd.InventoryItemId));

        return Ok();
    }

    private CommandResult TryCloseInventory(Commands.CloseInventory cmd)
    {
        if (!_openInventories.Contains(cmd.InventoryItemId))
            return Fail($"Inventory {cmd.InventoryItemId} is not currently open.");

        _openInventories.Remove(cmd.InventoryItemId);

        RaiseEvent(new Events.InventoryClosed(cmd.ActorId, cmd.InventoryItemId));

        return Ok();
    }

    public bool TryGetItemPreview(ItemId itemId, CharacterId? characterViewer, out IPreviewPresentation presentation)
    {
        presentation = default;

        if (!TryResolve(itemId, out Item item))
        {
            Debug.LogError("Failed to resolve item to preview");
            return false;
        }

        presentation = new ItemPreviewPresentation(GameAPI, characterViewer, itemId);
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers (pure domain logic)
    // ─────────────────────────────────────────────────────────────


    internal bool ItemsAreSameType(params Item[] items)
    {
        return ItemsAreSameType((IEnumerable<Item>)items);
    }

    internal bool ItemsAreSameType(IEnumerable<Item> items)
    {
        string name = null;

        foreach (var item in items)
        {
            if (name == null)
            {
                name = item.Name;
                continue;
            }

            if (item.Name != name)
                return false;
        }

        return true;
    }

    private bool TryGetValidInventoryLocation(Character character, out InventoryLocation inventoryLocation)
    {
        inventoryLocation = default;

        if (character == null || !character.InventoryItemId.HasValue)
            return false;

        if (!GameAPI.Databases.TryGetModel(character.InventoryItemId.Value, out Item inventoryItem))
            return false;

        if (!inventoryItem.GetIsInventory() || inventoryItem.Inventory == null)
            return false;

        inventoryLocation = new InventoryLocation(character.InventoryItemId.Value);
        return true;
    }
}
