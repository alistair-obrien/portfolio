using System;
using System.Collections.Generic;

public record ItemBlueprint : IBlueprint
{
    IGameDbId IBlueprint.Id => Id;

    public string TypeId => "Item";

    // Identity
    [HideInEditor] public ItemId Id;
    string IBlueprint.Name { get => Name; set => Name = value; }
    public string Name;
    [MultipleLineText(3)] public string Description;
    public string FlavorText;
    public int BaseMonetaryValue;
    [ForceNotNull] public RenderKey RenderKey;
    public ManufacturerId? ManufacturerId;

    // Used for Inventory
    public CellSize SizeInInventory;
    public CellSize SizeOnMap;

    // State
    public Fraction Condition;

    // Stack
    public Fraction Stack;

    [SubComponent] public ModFrameSaveData ModFrame;
    [SubComponent] public ModSaveData Mod;
    [SubComponent] public ConsumableSaveData Consumable;
    [SubComponent] public ClothingSaveData Clothing;
    [SubComponent] public InventoryBlueprint Inventory;
    [SubComponent] public AmmoSaveData Ammo;
    [SubComponent] public GunSaveData Gun;
    [SubComponent] public ArmorBlueprint Armor;

    public ItemBlueprint() 
    {
        Id = ItemId.New();
        SizeInInventory = new CellSize(1, 1);
        SizeOnMap = new CellSize(1, 1);
        RenderKey = new RenderKey("");
    }

    public ItemBlueprint(Item item)
    {
        // Identity
        Id = item.Id;
        Name = item.Name;
        ManufacturerId = item.ManufacturerId;
        Description = item.Description;
        FlavorText = item.FlavorText;
        BaseMonetaryValue = item.BaseMonetaryValue;
        RenderKey = item.RenderKey;

        // Used for Inventory
        SizeInInventory = item.SizeInInventory;
        SizeOnMap = item.SizeOnMap;

        // State
        Condition = new Fraction(item.CurrentCondition, item.MaxCondition);

        // Stack
        Stack = new Fraction(item.CurrentStackCount, item.MaxStackCount);

        if (item.GetIsModFrame()) ModFrame = item.ModFrame.Save();
        if (item.GetIsMod()) Mod = item.Mod.Save();
        if (item.GetIsConsumable()) Consumable = item.Consumable.Save();
        if (item.GetIsClothing()) Clothing = item.Clothing.Save();
        if (item.GetIsInventory()) Inventory = item.Inventory.Save();
        if (item.GetIsAmmo()) Ammo = item.Ammo.Save();
        if (item.GetIsGun()) Gun = item.Gun.Save();
        if (item.GetIsArmor()) Armor = item.Armor.Save();
    }
}

public class Item : Entity<ItemBlueprint>,
    IMapPlaceable,
    IGameDbResolvable,
    IHasGameDbResolvableReferences
{
    IGameModelLocation IGameDbResolvable.AttachedLocation => AttachedLocation;
    public IItemLocation AttachedLocation { get; internal set; }

    protected override string TypeId => "Item";
    protected override int Version => 1;

    IGameDbId IMapPlaceable.Id => Id;
    IGameDbId IGameDbResolvable.Id => Id;
    IDbId IDbResolvable.Id => Id;

    public void SetAttachedLocation(IGameModelLocation targetLocation)
    {
        if (targetLocation is not IItemLocation itemLocation)
            return;

        AttachedLocation = itemLocation;
    }

    void IGameDbResolvable.ClearAttachedLocation()
    {
        AttachedLocation = null;
    }

    public List<IGameDbId> GetChildIdReferences()
    {
        List<IGameDbId> childIds = new();

        if (GetIsGun())
            childIds.AddRange(Gun.GetChildIdReferences());

        if (GetIsInventory())
            childIds.AddRange(Inventory.GetChildIdReferences());

        // Frames will return mod references

        return childIds;
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        Id = (ItemId)idMap[Id];

        if (GetIsGun())
            Gun.RemapIds(idMap);

        if (GetIsInventory())
            Inventory.RemapIds(idMap);
    }

    public override ItemBlueprint SaveToBlueprint()
    {
        return new ItemBlueprint(this);
    }

    // Identity
    public ItemId Id { get; private set; }
    public string Name { get; private set; }
    public ManufacturerId? ManufacturerId { get; private set; }
    public string Description { get; private set; }
    public string FlavorText { get; private set; }
    public int BaseMonetaryValue { get; private set; }
    public RenderKey RenderKey { get; private set; }

    // Used for Inventory
    public CellSize SizeInInventory { get; private set; }
    public CellSize SizeOnMap { get; private set; }

    // State
    public int CurrentCondition { get; private set; }
    public int MaxCondition { get; private set; }

    // Stack
    public int CurrentStackCount { get; private set; }
    public int MaxStackCount { get; private set; }

    // Components
    public ModFrameComponent ModFrame { get; private set; }
    public ModComponent Mod { get; private set; }
    public ConsumableComponent Consumable { get; private set; }
    public ClothingComponent Clothing { get; private set; }
    public InventoryComponent Inventory { get; private set; } // Items can have an inventory inside them too! Like bags and shit
    public AmmoComponent Ammo { get; private set; }
    public GunComponent Gun { get; private set; }
    public ArmorComponent Armor { get; private set; }

    // Used in editors with Activator. May want to make this obsolete
    public Item()
    {
        Id = ItemId.New();
        RenderKey = new RenderKey("");
    }

    // Attaches other referenced Ids after all have been built
    internal void AttachReferences(CharacterBlueprint characterSaveData, Dictionary<IGameDbId, IGameDbResolvable> entityDictionary)
    {
        // TODO
    }

    public bool GetIsModFrame() => ModFrame != null;

    public bool GetIsMod() => Mod != null;

    public bool GetIsConsumable() => Consumable != null;

    public bool GetIsClothing() => Clothing != null;

    public bool GetIsInventory() => Inventory != null;

    public bool GetIsAmmo() => Ammo != null;

    public bool GetIsGun() => Gun != null;

    public bool GetIsArmor() => Armor != null;

    internal void ApplyDamage(DamageInstance itemDamageInstance)
    {
        CurrentCondition -= itemDamageInstance.DamageSeverity;
        if (CurrentCondition < 0)
        {
            CurrentCondition = 0;
        }
    }

    internal IItemLocation FindItemLocation(ItemId itemId)
    {
        if (this.GetIsInventory() && Inventory.ContainsItem(itemId))
        {
            if (this.Inventory.TryGetItemPosition(itemId, out var position))
            {
                return new InventoryLocation(Id, position);
            }
        }

        if (this.GetIsGun() && Gun.HasItemLoadedAsAmmo(itemId)) { return new GunAmmoLocation(Id); }

        // TODO: Mod References for mods installed on frames

        return null;
    }

    public bool IsCompatibleWithSlot(ISlotId slotPath)
    {
        if (slotPath is LoadoutSlotId loadoutSlotId && loadoutSlotId == SlotIds.Loadout.HeldItem) { return true; } // Any item can be held

        if (GetIsInventory() && Inventory.GetCompatibleSlotPaths().Contains(slotPath)) { return true; }
        if (GetIsGun() && Gun.GetCompatibleSlotPaths().Contains(slotPath)) { return true; }
        if (GetIsArmor() && Armor.GetCompatibleSlotPaths().Contains(slotPath)) { return true; }
        if (GetIsModFrame() && ModFrame.GetCompatibleSlotPaths().Contains(slotPath)) { return true; }
        if (GetIsClothing() && Clothing.GetCompatibleSlotPaths().Contains(slotPath)) { return true; }

        return false;
    }

    internal void IncreaseStackCount(int amountIncreased)
    {
        SetStackAmount(CurrentStackCount + amountIncreased);
    }

    internal void DecreaseStackCount(int amountReduced)
    {
        SetStackAmount(CurrentStackCount - amountReduced);
    }

    internal void SetStackAmount(int newAmount)
    {
        CurrentStackCount = Mathf.Clamp(newAmount, 0, MaxStackCount);
    }

    public void ReattachReferences(IGameDbResolvable originalModel)
    {
        //TODO
    }

    public IEnumerable<IGameDbId> GetAttachedEntityIds()
    {
        if (GetIsGun())
        {
            if (Gun.TryGetLoadedAmmo(out var itemId))
            {
                yield return itemId;
            }
        }
    }

    public IEnumerable<IGameDbId> GetReferencedEntityIds()
    {
        if (ManufacturerId != null)
            yield return ManufacturerId;
    }

    public void AttachEntities(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        //TODO
    }

    public void ApplyBlueprint(IBlueprint blueprint)
    {
        if (blueprint is not ItemBlueprint data)
            return;

        Id = data.Id;
        Name = data.Name;
        ManufacturerId = data.ManufacturerId; // This is not an embedded reference so we can just assign it
        Description = data.Description;
        FlavorText = data.FlavorText;
        BaseMonetaryValue = Math.Max(0, data.BaseMonetaryValue);
        RenderKey = data.RenderKey;
        SizeInInventory = new CellSize(
            Math.Max(1, data.SizeInInventory.Width),
            Math.Max(1, data.SizeInInventory.Height)
        );
        SizeOnMap = new CellSize(
            Math.Max(1, data.SizeOnMap.Width),
            Math.Max(1, data.SizeOnMap.Height)
        );
        CurrentCondition = Math.Max(0, data.Condition.Current);
        MaxCondition = Math.Max(0, data.Condition.Max);
        CurrentStackCount = Math.Max(0, data.Stack.Current);
        MaxStackCount = Math.Max(0, data.Stack.Max);

        ModFrame = null;
        Mod = null;
        Consumable = null;
        Clothing = null;
        Inventory = null;
        Ammo = null;
        Gun = null;
        Armor = null;

        if (data.ModFrame != null) ModFrame = new ModFrameComponent(data.ModFrame);
        if (data.Mod != null) Mod = new ModComponent(data.Mod);
        if (data.Consumable != null) Consumable = new ConsumableComponent(data.Consumable);
        if (data.Clothing != null) Clothing = new ClothingComponent(data.Clothing);
        if (data.Inventory != null) Inventory = new InventoryComponent(data.Inventory);
        if (data.Ammo != null) Ammo = new AmmoComponent(data.Ammo);
        if (data.Gun != null) Gun = new GunComponent(data.Gun);
        if (data.Armor != null) Armor = new ArmorComponent(data.Armor);
    }

    public IEnumerable<AttachmentChange> GetAttachmentChanges(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        yield break;
    }
}
