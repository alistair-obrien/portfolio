public class ItemPreviewPresentation : IPreviewPresentation
{
    public readonly ItemPresentation Item;

    public readonly bool IsGun;
    public readonly PreviewGunPresentation Gun;

    public readonly bool IsAmmo;
    public readonly PreviewAmmoPresentation Ammo;

    public ItemPreviewPresentation(
        GameInstance gameAPI,
        CharacterId? characterViewer,
        ItemId itemId)
    {
        if (!gameAPI.Databases.TryGetModel(itemId, out Item item))
            return;

        Item = new ItemPresentation(gameAPI, itemId);
        
        IsGun = item.GetIsGun();
        if (IsGun)
        {
            Gun = new PreviewGunPresentation(gameAPI, characterViewer, itemId);
        }

        IsAmmo = item.GetIsAmmo();
        if (IsAmmo)
        {
            Ammo = new PreviewAmmoPresentation(gameAPI, characterViewer, itemId);
        }
    }
}
