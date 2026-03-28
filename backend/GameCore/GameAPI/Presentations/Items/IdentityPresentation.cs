public sealed record LoadedAmmoPresentation
{
    public readonly int LoadedAmmoCount;

    public readonly PreviewAmmoPresentation Ammo;

    public LoadedAmmoPresentation(GameInstance gameAPI, CharacterId? characterViewer, ItemId ammoItemId)
    {
        if (!gameAPI.Databases.TryGetModel(ammoItemId, out Item ammoItem))
            return;

        LoadedAmmoCount = ammoItem.CurrentStackCount;
        Ammo = new PreviewAmmoPresentation(gameAPI, characterViewer, ammoItemId);
    }
}

public sealed record IdentityPresentation(
    IGameDbId EntityId,
    string Name,
    string Description,
    string FlavorText
);
