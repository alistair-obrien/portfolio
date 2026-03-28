public class CharacterPreviewPresentation : IPreviewPresentation
{
    public readonly CharacterId CharacterId;
    public readonly string Name;

    public bool HasWeapon;
    public ItemSlotPresentation EquippedGun;
    public PreviewGunPresentation GunStats;

    public bool HasArmor;
    public ItemSlotPresentation EquippedArmor;
    public PreviewArmorPresentation ArmorStats;

    public AllHealthDomainsPresentation HealthSummary;

    public CharacterPreviewPresentation(GameInstance gameAPI, CharacterId characterId)
    {
        if (!gameAPI.Databases.TryGetModel(characterId, out Character character))
            return;

        CharacterId = character.Id;
        Name = character.Name;

        if (character.WeaponAItemId.HasValue &&
            gameAPI.Databases.TryGetModel(character.WeaponAItemId.Value, out Item weaponItem) &&
            weaponItem.IsCompatibleWithSlot(SlotIds.Loadout.PrimaryWeapon))
        {
            HasWeapon = true;
            EquippedGun = new ItemSlotPresentation(gameAPI, characterId, SlotIds.Loadout.PrimaryWeapon, character.WeaponAItemId.Value);
            if (weaponItem.GetIsGun())
                GunStats = new PreviewGunPresentation(gameAPI, characterId, character.WeaponAItemId.Value);
        }

        if (character.ArmorItemId.HasValue &&
            gameAPI.Databases.TryGetModel(character.ArmorItemId.Value, out Item armorItem) &&
            armorItem.IsCompatibleWithSlot(SlotIds.Loadout.Armor))
        {
            HasArmor = true;
            EquippedArmor = new ItemSlotPresentation(gameAPI, characterId, SlotIds.Loadout.Armor, character.ArmorItemId.Value);
            ArmorStats = new PreviewArmorPresentation(gameAPI, character.ArmorItemId.Value);
        }

        HealthSummary = new AllHealthDomainsPresentation(character);
    }
}
