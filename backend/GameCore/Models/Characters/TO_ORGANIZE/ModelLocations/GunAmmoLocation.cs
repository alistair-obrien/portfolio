using Newtonsoft.Json;

public struct GunAmmoLocation : IItemLocation 
{
    public ItemId GunItemId { get; private set; }

    public IGameDbId OwnerEntityId => GunItemId;

    public GunAmmoLocation(ItemId gunItemUid)
    {
        GunItemId = gunItemUid;
    }

    public override string ToString()
    {
        return $"GunAmmoLocation(gunItem:{GunItemId})";
    }
}