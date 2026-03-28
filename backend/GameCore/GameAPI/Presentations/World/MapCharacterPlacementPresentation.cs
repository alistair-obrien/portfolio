public class MapCharacterPlacementPresentation
{
    public readonly CharacterPresentation CharacterPresentation;

    public readonly CharacterId CharacterId;
    public readonly CellFootprint CellFootprint;

    public readonly bool HasWeapon;
    public readonly RenderKey WeaponRenderKey;
    public readonly CellSize WeaponCellSize;

    public MapCharacterPlacementPresentation(
        GameInstance gameApi,
        CharacterPlacementOnMap characterPlacementOnMap)
    {
        if (!gameApi.Databases.TryResolve(characterPlacementOnMap.CharacterId, out Character character))
            return;

        CharacterId = characterPlacementOnMap.CharacterId;
        CellFootprint = characterPlacementOnMap.Footprint;

        if (character.WeaponAItemId.HasValue &&
            gameApi.Databases.TryGetModel(character.WeaponAItemId.Value, out Item weaponItem) &&
            weaponItem.IsCompatibleWithSlot(SlotIds.Loadout.PrimaryWeapon))
        {
            WeaponRenderKey = weaponItem.RenderKey;
            HasWeapon = true;
            WeaponCellSize = weaponItem.SizeInInventory;
        }

        CharacterPresentation = new CharacterPresentation(character);
    }
}
