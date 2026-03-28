public class PreviewAmmoPresentation
{
    public readonly RenderKey LoadedAmmoRenderKey;
    public readonly string AmmoItemName;
    public readonly DamageType DamageType;
    public int BaseDamage;

    public PreviewAmmoPresentation(GameInstance gameAPI, CharacterId? characterViewer, ItemId ammoItemUid) 
    {
        Character character = null;
        if (characterViewer.HasValue)
            gameAPI.Databases.TryGetModel(characterViewer.Value, out character);

        if (!gameAPI.Databases.TryGetModel(ammoItemUid, out Item ammoItem))
            return;

        LoadedAmmoRenderKey = ammoItem.RenderKey;
        AmmoItemName = ammoItem.Name;
        DamageType = ammoItem.Ammo.DamageType;
        BaseDamage = ammoItem.Ammo.BaseEnergy;
    }
}
