using System;
using System.Collections.Generic;
using System.Linq;

public interface IAttachmentSlotResolver
{
    bool TryResolve(IBlueprint blueprint, string fieldName, out ISlotId slot);
}

public class CharacterAttachmentSlotResolver : IAttachmentSlotResolver
{
    public bool TryResolve(IBlueprint blueprint, string fieldName, out ISlotId slot)
    {
        slot = null;

        if (blueprint is not CharacterBlueprint character)
            return false;

        slot = fieldName switch
        {
            nameof(CharacterBlueprint.HeldItemId) => SlotIds.Loadout.HeldItem,
            nameof(CharacterBlueprint.InventoryItemId) => SlotIds.Loadout.Inventory,
            nameof(CharacterBlueprint.WeaponItemId) => SlotIds.Loadout.PrimaryWeapon,
            nameof(CharacterBlueprint.ArmorItemId) => SlotIds.Loadout.Armor,
            _ => null
        };

        return slot != null;
    }
}

public record CharacterBlueprint : IBlueprint
{
    IGameDbId IBlueprint.Id => Id;
    public string TypeId => "Character";

    string IBlueprint.Name { get => Name; set => Name = value; }

    public CharacterId Id;
    public string Name;
    [MultipleLineText(3)] public string Description;
    public int Level;
    public string VisualArchetype;

    // These are not components. They are just sub groups.
    [ForceNotNull] public CharacterStatsSaveData Stats = new();
    [ForceNotNull] public AnatomySaveData Anatomy = new();
    [ForceNotNull] public CyberneticsSaveData Cybernetics = new();
    [ForceNotNull] public StyleSaveData Style = new();
    [ForceNotNull] public OperatingSystemBlueprint OperatingSystem = new();

    [Attachment("Held Item")] public ItemId? HeldItemId;
    [Attachment("Inventory Item")] public ItemId? InventoryItemId;
    [Attachment("Weapon Item")] public ItemId? WeaponItemId;
    [Attachment("Armor Item")] public ItemId? ArmorItemId;

    public string DialogueNode;

    public CellSize SizeOnMap;

    public CharacterBlueprint()
    {
        Id = CharacterId.New();
        SizeOnMap = new CellSize(3, 3);
    }
}

public class Character : Entity<CharacterBlueprint>,
    IGameDbResolvable,
    IMapPlaceable,
    IHasGameDbResolvableReferences,
    IHasAttachments
{
    protected override string TypeId => "Character";
    protected override int Version => 1;
    public override CharacterBlueprint SaveToBlueprint()
    {
        return new CharacterBlueprint()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Level = Level,
            VisualArchetype = VisualArchetype,

            SizeOnMap = SizeOnMap,

            Stats = Stats.Save(),
            Anatomy = Anatomy.Save(),
            Cybernetics = Cybernetics.Save(),
            Style = Style.Save(),
            OperatingSystem = OperatingSystem.Save(),

            HeldItemId = HeldItemId,
            InventoryItemId = InventoryItemId,
            WeaponItemId = WeaponAItemId,
            ArmorItemId = ArmorItemId,

            DialogueNode = DialogueNode
        };
    }

    IDbId IDbResolvable.Id => Id;
    string IGameDbResolvable.Name => Name;
    public ICharacterLocation AttachedLocation { get; private set; }
    IGameModelLocation IGameDbResolvable.AttachedLocation => AttachedLocation;

    IGameDbId IGameDbResolvable.Id => Id;
    // Identity
    public CharacterId Id { get; private set; }
    IGameDbId IMapPlaceable.Id => Id;

    public string Name { get; private set; }
    public string Description { get; private set; }
    public int Level { get; private set; }
    public string VisualArchetype { get; private set; }

    public CellSize SizeOnMap { get; private set; }

    // Composition
    public OperatingSystem OperatingSystem { get; private set; }
    public CharacterStatsComponent Stats { get; private set; }
    public AnatomyComponent Anatomy { get; private set; }
    public CyberneticsComponent Cybernetics { get; private set; }
    public StyleComponent Style { get; private set; }

    // Loadout
    public ItemId? HeldItemId { get; private set; }
    public ItemId? InventoryItemId { get; private set; }
    public ItemId? WeaponAItemId { get; private set; }
    public ItemId? ArmorItemId { get; private set; }

    // Dialogue
    public string DialogueNode { get; private set; }

    private CyberneticsComponent CreateDefaultCybernetics()
    {
        return new CyberneticsComponent(new Dictionary<CyberneticSlotId, ItemId?>
        {
            { SlotIds.Cybernetic.Head, null },
            { SlotIds.Cybernetic.Torso, null },
            { SlotIds.Cybernetic.LeftArm, null },
            { SlotIds.Cybernetic.RightArm, null },
            { SlotIds.Cybernetic.Legs, null },
            { SlotIds.Cybernetic.Internal1, null },
            { SlotIds.Cybernetic.Internal2, null },
            { SlotIds.Cybernetic.Internal3, null },
            { SlotIds.Cybernetic.Internal4, null },
            { SlotIds.Cybernetic.Internal5, null },
        });
    }

    private AnatomyComponent CreateDefaultAnatomy()
    {
        return new AnatomyComponent(new Dictionary<AnatomySlotId, OrganicBodyPartComponent>
        {
            { SlotIds.Organic.Head, new OrganicBodyPartComponent("Head", "The Head", 3, new()) },
            { SlotIds.Organic.Torso, new OrganicBodyPartComponent("Torso", "The Torso", 6, new()) },
            { SlotIds.Organic.LeftArm,  new OrganicBodyPartComponent("Left Arm", "The Left Arm", 4, new()) },
            { SlotIds.Organic.RightArm, new OrganicBodyPartComponent("Right Arm", "The Right Arm", 4, new()) },
            { SlotIds.Organic.LeftLeg, new OrganicBodyPartComponent("Left Leg", "The Left Leg", 4, new()) },
            { SlotIds.Organic.RightLeg, new OrganicBodyPartComponent("Right Leg", "The Right Leg", 4, new()) },
        });
    }

    private StyleComponent CreateDefaultStyle()
    {
        return new StyleComponent(new Dictionary<StyleSlotId, ItemId?>
        {
            { SlotIds.Style.Head, null },
            { SlotIds.Style.Face, null },
            { SlotIds.Style.TorsoInner, null },
            { SlotIds.Style.TorsoOuter, null },
            { SlotIds.Style.Hands, null },
            { SlotIds.Style.Legs, null },
            { SlotIds.Style.Feet, null },
            { SlotIds.Style.Accessory1, null },
            { SlotIds.Style.Accessory2, null },
            { SlotIds.Style.Accessory3, null },
            { SlotIds.Style.Accessory4, null },
            { SlotIds.Style.Accessory5, null },
        });
    }

    // ---------------------------
    // Health
    // ---------------------------

    public int GetMaxBiologicalHP() => Anatomy.GetMaxHealth();
    public int GetCurrentBiologicalHP() => Anatomy.GetCurrentHealth();
    public int GetRemainingInjuryCapacity() => Anatomy.GetRemainingHealth();
    public float GetBiologicalHPPercentage() => Anatomy.GetHealthPercentage();

    public int GetMaxCyberneticHP() => Cybernetics.GetMaxDamageCapacity();
    public int GetCurrentCyberneticHP() => Cybernetics.GetCurrentHealth();
    public float GetCyberneticHPPercentage() => Cybernetics.GetHealthPercentage();

    internal void OnNewTickStarted() => Stats.Tick();

    internal void SetDialogueNode(string dialogueNode) => DialogueNode = dialogueNode;

    // ---------------------------
    // Slot Resolution
    // ---------------------------
    internal bool TryGetItemInSlot(ISlotId slotId, out ItemId itemId)
    {
        switch (slotId)
        {
            case LoadoutSlotId loadoutSlotId:
                if (loadoutSlotId == SlotIds.Loadout.HeldItem)
                {
                    if (HeldItemId.HasValue)
                    {
                        itemId = HeldItemId.Value;
                        return true;
                    }
                }
                if (loadoutSlotId == SlotIds.Loadout.Inventory) 
                { 
                    if (InventoryItemId.HasValue)
                    {
                        itemId = InventoryItemId.Value; 
                        return true; 
                    }
                }
                if (loadoutSlotId == SlotIds.Loadout.PrimaryWeapon) 
                { 
                    if (WeaponAItemId.HasValue)
                    {
                        itemId = WeaponAItemId.Value; 
                        return true; 
                    }
                }
                if (loadoutSlotId == SlotIds.Loadout.Armor) 
                { 
                    if (ArmorItemId.HasValue)
                    {
                        itemId = ArmorItemId.Value; 
                        return true; 
                    }
                }
                break;
            case CyberneticSlotId cyberneticSlotId:
                if (Cybernetics.TryResolveSlot(cyberneticSlotId, out var cyberslotItemId))
                {
                    if (cyberslotItemId.HasValue)
                    {
                        itemId = cyberslotItemId.Value;
                        return true;
                    }
                }
                break;
            case StyleSlotId styleSlotId:
                if (Style.TryResolveSlot(styleSlotId, out var styleSlotItem))
                {
                    if (styleSlotItem.HasValue)
                    {
                        itemId = styleSlotItem.Value;
                        return true;
                    }
                }
                break;
            default:
                break;
        }

        itemId = default;
        return false;
    }

    internal bool SetItemInSlot(ISlotId slotId, Item newItem)
    {
        ApplySlotValue(slotId, newItem?.Id);
        return true;
    }

    private void ApplySlotValue(ISlotId slotId, ItemId? itemId)
    {
        if (slotId is LoadoutSlotId loadoutSlotId)
        {
            if (loadoutSlotId == SlotIds.Loadout.HeldItem) 
            { 
                HeldItemId = itemId;
            }
            if (loadoutSlotId == SlotIds.Loadout.Inventory) 
            { 
                InventoryItemId = itemId;
            }
            if (loadoutSlotId == SlotIds.Loadout.PrimaryWeapon) 
            { 
                WeaponAItemId = itemId;
            }
            if (loadoutSlotId == SlotIds.Loadout.Armor) 
            { 
                ArmorItemId = itemId;
            }
        }

        if (slotId is CyberneticSlotId cyberSlotId)
        {
            if (itemId.HasValue)
            {
                Cybernetics.TryAssignToSlot(cyberSlotId, itemId);
            }
            else
            {
                Cybernetics.TryUnassignFromSlot(cyberSlotId, out _);
            }
        }

        if (slotId is StyleSlotId styleSlotId)
        {
            if (itemId.HasValue)
            {
                Style.TryAssignToSlot(styleSlotId, itemId);
            }
            else
            {
                Style.TryUnassignFromSlot(styleSlotId, out _);
            }
        }
    }

    // ---------------------------
    // Equip / Unequip
    // ---------------------------

    internal bool CanEquip(Item item, ISlotId slotId)
    {
        if (!item.IsCompatibleWithSlot(slotId))
            return false;

        // Already has something equipped
        if (TryGetItemInSlot(slotId, out var currentlyEquipped))
            return false;

        return true;
    }

    internal bool TryEquip(Item item, ISlotId slotId)
    {
        if (!CanEquip(item, slotId))
            return false;

        return SetItemInSlot(slotId, item);
    }

    internal bool TryUnequip(Item item, ISlotId slotId)
    {
        if (!TryGetItemInSlot(slotId, out var currentItemId))
            return false;

        if (currentItemId != item.Id)
            return false;

        ApplySlotValue(slotId, null);
        return true;
    }

    // ---------------------------
    // Inventory
    // ---------------------------

    internal bool HasInventory() => InventoryItemId.HasValue;

    public bool HasOperatingSystemCapability(string capabilityId)
    {
        return OperatingSystem?.HasCapability(capabilityId) ?? false;
    }

    public bool HasOperatingSystemGrant(string grantId, OperatingSystemAccessLevel minimumAccess)
    {
        return OperatingSystem?.HasGrant(grantId, minimumAccess) ?? false;
    }

    public bool HasOperatingSystemId(string operatingSystemId)
    {
        return OperatingSystem?.IsId(operatingSystemId) ?? false;
    }

    internal bool TrySetOperatingSystemModuleEnabled(string moduleId, bool enabled)
    {
        if (OperatingSystem == null || !OperatingSystem.TryGetModule(moduleId, out var module))
            return false;

        if (module.Enabled == enabled)
            return false;

        OperatingSystem.SetModuleEnabled(moduleId, enabled);
        return true;
    }

    // ---------------------------
    // Loadout Queries
    // ---------------------------

    public bool HasWeapon() => WeaponAItemId.HasValue;
    public ItemId? GetWeapon() => WeaponAItemId;

    public bool HasArmor() => ArmorItemId.HasValue;
    public ItemId? GetArmor() => ArmorItemId;

    internal bool IsEquippedToLoadout(ItemId itemId)
    {
        return WeaponAItemId == itemId || ArmorItemId == itemId;
    }

    // ---------------------------
    // Item Location
    // ---------------------------

    internal bool TryGetEquippedSlot(ItemId itemId, out ISlotId slotPath)
    {
        slotPath = default;
        return false;
        //throw new NotImplementedException();
    }

    //TODO
    public List<IGameDbId> GetChildIdReferences()
    {
        var childIds = new HashSet<IGameDbId>();

        if (HeldItemId.HasValue) childIds.Add(HeldItemId.Value);
        if (InventoryItemId.HasValue) childIds.Add(InventoryItemId.Value);
        if (WeaponAItemId.HasValue) childIds.Add(WeaponAItemId.Value);
        if (ArmorItemId.HasValue) childIds.Add(ArmorItemId.Value);

        foreach (var item in Style.GetAllItems().Values)
        {
            if (item.HasValue)
                childIds.Add(item.Value);
        }

        foreach (var item in Cybernetics.GetAllItems().Values)
        {
            if (item.HasValue)
                childIds.Add(item.Value);
        }

        return childIds.ToList();
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        Id = (CharacterId)idMap[Id];
        if (HeldItemId.HasValue) HeldItemId = (ItemId)idMap[HeldItemId];
        if (InventoryItemId.HasValue) InventoryItemId = (ItemId)idMap[InventoryItemId];
        if (WeaponAItemId.HasValue) WeaponAItemId = (ItemId)idMap[WeaponAItemId];
        if (ArmorItemId.HasValue) ArmorItemId = (ItemId)idMap[ArmorItemId];

        foreach (var slot in Style.GetAllItems().Keys.ToList())
        {
            var itemId = Style.GetAllItems()[slot];
            if (itemId.HasValue)
                Style.GetAllItems()[slot] = (ItemId)idMap[itemId.Value];
        }

        foreach (var slot in Cybernetics.GetAllItems().Keys.ToList())
        {
            var itemId = Cybernetics.GetAllItems()[slot];
            if (itemId.HasValue)
                Cybernetics.GetAllItems()[slot] = (ItemId)idMap[itemId.Value];
        }
    }

    void IGameDbResolvable.ClearAttachedLocation()
    {
        AttachedLocation = null;
    }

    void IGameDbResolvable.SetAttachedLocation(IGameModelLocation targetLocation)
    {
        if (targetLocation is not ICharacterLocation characterLocation)
            return;

        AttachedLocation = characterLocation;
    }

    // Attaches other referenced Ids after all have been built
    internal void AttachReferences(
        IBlueprint blueprint,
        Dictionary<IGameDbId, IGameDbResolvable> entityDictionary)
    {
        if (blueprint is not CharacterBlueprint characterBlueprint)
            return;

        if (characterBlueprint.InventoryItemId != null &&
            entityDictionary.TryGetValue(characterBlueprint.InventoryItemId, out var inventory))
        {
            TryEquip((Item)inventory, SlotIds.Loadout.Inventory);
        }

        if (characterBlueprint.WeaponItemId != null &&
            entityDictionary.TryGetValue(characterBlueprint.WeaponItemId, out var weapon))
        {
            TryEquip((Item)weapon, SlotIds.Loadout.PrimaryWeapon);
        }

        if (characterBlueprint.ArmorItemId != null &&
            entityDictionary.TryGetValue(characterBlueprint.ArmorItemId, out var armor))
        {
            TryEquip((Item)armor, SlotIds.Loadout.Armor);
        }

        if (characterBlueprint.HeldItemId != null &&
            entityDictionary.TryGetValue(characterBlueprint.HeldItemId, out var held))
        {
            TryEquip((Item)held, SlotIds.Loadout.HeldItem);
        }
    }

    public void AttachEntities(IBlueprint blueprint,
        Dictionary<IGameDbId, IGameDbResolvable> entityDictionary)
    {
        Debug.Log("Attaching Entities to character");
        if (blueprint is not CharacterBlueprint characterBlueprint)
            return;

        if (characterBlueprint.InventoryItemId.HasValue) 
        {
            if (entityDictionary.TryGetValue(characterBlueprint.InventoryItemId, out IGameDbResolvable modelLookup))
            {
                if (modelLookup is Item item)
                    TryEquip(item, SlotIds.Loadout.Inventory);
            }
        }

        if (characterBlueprint.WeaponItemId.HasValue)
        {
            if (entityDictionary.TryGetValue(characterBlueprint.WeaponItemId, out IGameDbResolvable modelLookup))
            {
                if (modelLookup is Item item)
                    TryEquip(item, SlotIds.Loadout.PrimaryWeapon);
            }
        }

        if (characterBlueprint.ArmorItemId.HasValue)
        {
            if (entityDictionary.TryGetValue(characterBlueprint.ArmorItemId, out IGameDbResolvable modelLookup))
            {
                if (modelLookup is Item item)
                    TryEquip(item, SlotIds.Loadout.Armor);
            }
        }

        if (characterBlueprint.HeldItemId.HasValue)
        {
            if (entityDictionary.TryGetValue(characterBlueprint.HeldItemId, out IGameDbResolvable modelLookup))
            {
                if (modelLookup is Item item)
                    TryEquip(item, SlotIds.Loadout.HeldItem);
            }
        }

        foreach (var styleEntry in characterBlueprint.Style.StyleItems)
        {
            if (!styleEntry.ItemId.HasValue)
                continue;

            if (entityDictionary.TryGetValue(styleEntry.ItemId, out IGameDbResolvable modelLookup))
            {
                if (modelLookup is Item item)
                    TryEquip(item, styleEntry.StyleSlotId);
            }
        }

        foreach (var cyberEntry in characterBlueprint.Cybernetics.Cybernetics)
        {
            if (!cyberEntry.ItemId.HasValue)
                continue;

            if (entityDictionary.TryGetValue(cyberEntry.ItemId, out IGameDbResolvable modelLookup))
            {
                if (modelLookup is Item item)
                    TryEquip(item, cyberEntry.CyberneticSlotId);
            }
        }
    }

    public void ReattachReferences(IGameDbResolvable originalModel)
    {
        if (originalModel is not Character character)
            return;

        HeldItemId = character.HeldItemId;
        InventoryItemId = character.InventoryItemId;
        WeaponAItemId = character.WeaponAItemId;
        ArmorItemId = character.ArmorItemId;
    }

    public IEnumerable<IGameDbId> GetAttachedEntityIds()
    {
        if (HeldItemId != null) yield return HeldItemId;
        if (InventoryItemId != null) yield return InventoryItemId;
        if (WeaponAItemId != null) yield return WeaponAItemId;
        if (ArmorItemId != null) yield return ArmorItemId;

        var allStyleItems = Style.GetAllItems();
        foreach (var item in allStyleItems)
        {
            if (item.Value != null)
                yield return item.Value;
        }

        var allCybernetics = Cybernetics.GetAllItems();
        foreach (var item in allCybernetics)
        {
            if (item.Value != null)
                yield return item.Value;
        }
    }

    public IEnumerable<IGameDbId> GetReferencedEntityIds()
    {
        yield break;
    }

    // Applies primitives and surface level
    public void ApplyBlueprint(IBlueprint blueprint)
    {
        if (blueprint is not CharacterBlueprint data)
            return;

        //
        Id = data.Id;

        Name = data.Name ?? "";
        Description = data.Description ?? "";
        Level = Math.Max(1, data.Level);
        VisualArchetype = data.VisualArchetype ?? "";

        SizeOnMap = new CellSize(
            Math.Max(1, data.SizeOnMap.Width),
            Math.Max(1, data.SizeOnMap.Height)
        );

        // ---------------------------
        // Stats
        // ---------------------------
        Stats = data.Stats != null
            ? new CharacterStatsComponent(data.Stats)
            : new CharacterStatsComponent();

        // ---------------------------
        // Anatomy
        // ---------------------------
        if (data.Anatomy != null)
            Anatomy = new AnatomyComponent(data.Anatomy);
        else
            Anatomy = CreateDefaultAnatomy();

        // ---------------------------
        // Cybernetics
        // ---------------------------
        if (data.Cybernetics != null)
            Cybernetics = new CyberneticsComponent(data.Cybernetics);
        else
            Cybernetics = CreateDefaultCybernetics();

        // ---------------------------
        // Style
        // ---------------------------
        if (data.Style != null)
            Style = new StyleComponent(data.Style);
        else
            Style = CreateDefaultStyle();

        OperatingSystem = data.OperatingSystem != null
            ? new OperatingSystem(data.OperatingSystem)
            : new OperatingSystem();

        DialogueNode = data.DialogueNode;


        //// 3. attachments (IMPORTANT)
        //HeldItemId = SyncItemSlot(data.HeldItemId, HeldItemId, SlotIds.Loadout.HeldItem, databaseDict);
        //InventoryItemId = SyncItemSlot(data.InventoryItemId, InventoryItemId, SlotIds.Loadout.Inventory, databaseDict);
        //WeaponAItemId = SyncItemSlot(data.WeaponItemId, WeaponAItemId, SlotIds.Loadout.PrimaryWeapon, databaseDict);
        //ArmorItemId = SyncItemSlot(data.ArmorItemId, ArmorItemId, SlotIds.Loadout.Armor, databaseDict);
    }

    public IEnumerable<AttachmentChange> GetAttachmentChanges(
        IBlueprint blueprint,
        Dictionary<IGameDbId, IGameDbResolvable> db)
    {
        if (blueprint is not CharacterBlueprint data)
            yield break;

        foreach (var change in ReconcileSlot(
            SlotIds.Loadout.PrimaryWeapon,
            WeaponAItemId,
            data.WeaponItemId,
            db))
            yield return change;

        foreach (var change in ReconcileSlot(
            SlotIds.Loadout.HeldItem,
            HeldItemId,
            data.HeldItemId,
            db))
            yield return change;

        foreach (var change in ReconcileSlot(
            SlotIds.Loadout.Inventory,
            InventoryItemId,
            data.InventoryItemId,
            db))
            yield return change;

        foreach (var change in ReconcileSlot(
            SlotIds.Loadout.Armor,
            ArmorItemId,
            data.ArmorItemId,
            db))
            yield return change;
    }

    private IEnumerable<AttachmentChange> ReconcileSlot(
        ISlotId slot,
        ItemId? currentId,
        ItemId? desiredId,
        Dictionary<IGameDbId, IGameDbResolvable> db)
    {
        ItemId? validatedDesired = null;

        if (desiredId.HasValue &&
            db.TryGetValue(desiredId.Value, out var resolvable) &&
            resolvable is Item item &&
            item.IsCompatibleWithSlot(slot))
        {
            validatedDesired = desiredId;
        }

        if (currentId == validatedDesired)
            yield break;

        // DETACH CURRENT
        if (currentId.HasValue &&
            db.TryGetValue(currentId.Value, out var currentResolvable))
        {
            yield return new AttachmentChange(
                currentId.Value,
                currentResolvable.AttachedLocation, // REAL location
                new RootLocation(currentId.Value));
        }

        // ATTACH NEW
        if (validatedDesired.HasValue)
        {
            yield return new AttachmentChange(
                validatedDesired.Value,
                new RootLocation(currentId.Value),
                new AttachedLocation(Id, slot));
        }
    }
}

public readonly struct AttachmentChange
{
    public readonly IGameDbId EntityId;
    public readonly IGameModelLocation OldLocation;
    public readonly IGameModelLocation NewLocation;

    public bool IsNone =>
        Equals(OldLocation, NewLocation);

    public AttachmentChange(
        IGameDbId entityId,
        IGameModelLocation oldLocation,
        IGameModelLocation newLocation)
    {
        EntityId = entityId;
        OldLocation = oldLocation;
        NewLocation = newLocation;
    }
}
