public class ItemPresentation
{
    public readonly IdentityPresentation Identity;
    public readonly CellSize CellSize;
    public readonly RenderKey RenderKey;

    // Stacks
    public readonly bool IsStackable;
    public readonly int CurrentStack;
    public readonly int MaxStack;

    // Condition
    public readonly bool HasCondition;
    public readonly int CurrentCondition;
    public readonly int MaxCondition;

    // Gun
    public readonly bool IsGun;
    public readonly bool HasLoadedAmmo;
    public readonly LoadedAmmoPresentation LoadedAmmo;
    public readonly int GunClipSize;
    public readonly GunStatsPresentation ComputedGunStats;

    // Ammo
    public readonly bool IsAmmo;
    public readonly RenderKey AmmoRenderKey;

    // Clothing
    public readonly bool IsClothing;
    
    // Mod Frame
    public readonly bool IsModFrame;
    
    // Mod
    public readonly bool IsMod;
    
    // Consumable
    public readonly bool IsConsumable;
    
    // Inventory
    public readonly bool IsInventory;
    public InventoryPresentation InventoryPresentation;

    // Armor    
    public readonly bool IsArmor;

    // Later we will introduce adapters for cleaner and more powerful view data creation
    public ItemPresentation(GameInstance gameAPI, ItemId itemId)
    {
        if (!gameAPI.Databases.TryGetModel(itemId, out Item item))
            return;

        Character character = null;
        
        gameAPI.Databases.EntityLocationAPI.TryFindEntityLocation(itemId, out var location);
        
        if (location is AttachedLocation equippedLocation)
        {
            gameAPI.Databases.TryGetModel(equippedLocation.EntityId, out character);
        }

        // Hmm
        if (location is GunAmmoLocation gunAmmoLocation)
        {

        }

        Identity = new IdentityPresentation(itemId, item.Name, item.Description, item.FlavorText);
        CellSize = item.SizeInInventory;
        RenderKey = item.RenderKey;

        // Stacks
        IsStackable = item.MaxStackCount > 1;
        CurrentStack = item.CurrentStackCount;
        MaxStack = item.MaxStackCount;

        // Condition
        HasCondition = item.MaxCondition > 1;
        CurrentCondition = item.CurrentCondition;
        MaxCondition = item.MaxCondition;

        // Gun
        IsGun = item.GetIsGun();
        if (IsGun)
        {
            GunClipSize = item.Gun.ClipSize;

            if (item.Gun.TryGetLoadedAmmo(out var ammoItemUid))
            {
                HasLoadedAmmo = true;
                LoadedAmmo = new LoadedAmmoPresentation(gameAPI, character?.Id, ammoItemUid);
            }

            gameAPI.Rulebook.WeaponsSection.TryGetGunStats(character, item, out ComputedGunStats);
        }

        // Ammo
        IsAmmo = item.GetIsAmmo();
        if (IsAmmo)
        {
            RenderKey = new RenderKey("ammo_piercing_case"); // Case Icon
            AmmoRenderKey = RenderKey; // Ammo Icon
        }

        // Inventory
        IsInventory = item.GetIsInventory();
        if (IsInventory)
        {
            //InventoryPresentation = new InventoryPresentation(gameAPI, model);
        }
    }
}