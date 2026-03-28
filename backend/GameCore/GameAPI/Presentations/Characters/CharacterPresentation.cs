public class CharacterPresentation
{
    // Identity
    public readonly CharacterId CharacterId;
    public readonly string Name;
    public readonly string Description;
    public readonly string PortraitTextureKey;

    // Health
    public AllHealthDomainsPresentation HealthSummary;

    // Loadout
    public readonly ItemSlotPresentation WeaponSlotPresentation;
    public readonly ItemSlotPresentation ArmorSlotPresentation;

    public readonly CharacterAnatomyPresentation AnatomyViewModel;

    public CharacterPresentation(Character character)
    {
        CharacterId = character.Id;
        Name = character.Name;
        Description = character.Description;
        //PortraitTextureKey = character.PortraitTextureKey.ToString(); //TODO: Render Key

        HealthSummary = new AllHealthDomainsPresentation(character);
    }
}
