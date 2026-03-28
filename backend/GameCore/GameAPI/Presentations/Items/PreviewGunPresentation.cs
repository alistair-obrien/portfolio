public class PreviewGunPresentation
{
    public readonly GunStatsPresentation ComputedGunStats;

    public readonly bool HasAmmo;
    public readonly LoadedAmmoPresentation LoadedAmmo;

    public PreviewGunPresentation(GameInstance gameAPI, CharacterId? characterViewer, ItemId gunItemId) 
    {
        Character character = null;
        if (characterViewer.HasValue)
            gameAPI.Databases.TryGetModel(characterViewer.Value, out character);

        if (!gameAPI.Databases.TryGetModel(gunItemId, out Item gunItem))
            return;

        if (!gunItem.GetIsGun() || gunItem.Gun == null)
            return;

        gameAPI.Rulebook.WeaponsSection.TryGetGunStats(character, gunItem, out ComputedGunStats);
        if (gunItem.Gun.TryGetLoadedAmmo(out var loadedAmmoId))
        {
            HasAmmo = true;
            LoadedAmmo = new LoadedAmmoPresentation(gameAPI, characterViewer, loadedAmmoId);
        }
    }
}
