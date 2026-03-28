public class ItemSlotPresentation
{
    public readonly ItemPresentation Item;

    public readonly CharacterId OwnerCharacterId;
    public readonly ISlotId SlotPath;
    
    public readonly int Width;
    public readonly int Height;

    public ItemSlotPresentation(
        GameInstance gameAPI,
        CharacterId characterUid,
        ISlotId slotPath, 
        ItemId? itemUid)
    {
        OwnerCharacterId = characterUid;
        SlotPath = slotPath;

        if (itemUid.HasValue)
        {
            Item = new ItemPresentation(gameAPI, itemUid.Value);
        }
    }
}
