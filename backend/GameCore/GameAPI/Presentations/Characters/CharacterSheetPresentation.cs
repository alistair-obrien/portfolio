public class CharacterSheetPresentation
{
    public readonly string Name;
    public readonly int Level;
    public readonly string PortraitTextureKey;
    public readonly string Description;
    
    public readonly int Physique;
    public readonly int Reflexes;
    public readonly int Technical;
    public readonly int Grit;
    public readonly int Charisma;
    public readonly float Cyberpsychosis;
    
    public readonly ItemSlotPresentation WeaponSlotPresentation;
    public readonly ItemSlotPresentation ArmorSlotPresentation;
    public readonly InventoryPresentation InventoryPresentation;

    public CharacterSheetPresentation(GameInstance gameAPI, Character character)
    {
        Name = character.Name;
        Level = character.Level;
        //PortraitTextureKey = character.PortraitTextureKey.ToString();//TODO: Render Key
        Description = character.Description;

        Physique = character.Stats.Physique;
        Reflexes = character.Stats.Reflexes;
        Technical = character.Stats.Technical;
        Grit = character.Stats.Grit;
        Charisma = character.Stats.Charisma;

        Cyberpsychosis = character.Stats.GetCyberpsychosis();

        if (character.WeaponAItemId.HasValue &&
            gameAPI.Databases.TryGetModel(character.WeaponAItemId.Value, out Item weaponItem) &&
            weaponItem.IsCompatibleWithSlot(SlotIds.Loadout.PrimaryWeapon))
        {
            WeaponSlotPresentation = new ItemSlotPresentation(gameAPI, character.Id, SlotIds.Loadout.PrimaryWeapon, character.WeaponAItemId.Value);
        }

        if (character.ArmorItemId.HasValue &&
            gameAPI.Databases.TryGetModel(character.ArmorItemId.Value, out Item armorItem) &&
            armorItem.IsCompatibleWithSlot(SlotIds.Loadout.Armor))
        {
            ArmorSlotPresentation = new ItemSlotPresentation(gameAPI, character.Id, SlotIds.Loadout.Armor, character.ArmorItemId.Value);
        }

        InventoryPresentation = new InventoryPresentation(gameAPI, character.InventoryItemId);
    }
}
