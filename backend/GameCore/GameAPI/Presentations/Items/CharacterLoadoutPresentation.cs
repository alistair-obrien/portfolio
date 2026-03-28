public class CharacterLoadoutPresentation
{
    public readonly ItemId? InventoryId;

    public readonly ItemSlotPresentation PrimaryWeapon;

    public readonly ItemSlotPresentation Armor;

    public CharacterLoadoutPresentation(GameInstance gameAPI, Character character)
    {
        if (character == null)
            return;

        InventoryId = character.InventoryItemId;

        if (character.WeaponAItemId.HasValue &&
            gameAPI.Databases.TryGetModel(character.WeaponAItemId.Value, out Item weaponItem) &&
            weaponItem.IsCompatibleWithSlot(SlotIds.Loadout.PrimaryWeapon))
        {
            PrimaryWeapon = new ItemSlotPresentation(
                gameAPI, 
                character.Id, 
                SlotIds.Loadout.PrimaryWeapon, 
                character.WeaponAItemId.Value);
        }
        if (character.ArmorItemId.HasValue &&
            gameAPI.Databases.TryGetModel(character.ArmorItemId.Value, out Item armorItem) &&
            armorItem.IsCompatibleWithSlot(SlotIds.Loadout.Armor))
        {
            Armor = new ItemSlotPresentation(gameAPI, character.Id, SlotIds.Loadout.Armor, character.ArmorItemId.Value);
        }
    }
}
