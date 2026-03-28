using Newtonsoft.Json;

public struct AttachedLocation : IItemLocation
{
    [JsonProperty] public IGameDbId EntityId { get; private set; }
    [JsonProperty] public ISlotId SlotPath { get; private set; }

    public IGameDbId OwnerEntityId => EntityId;

    public AttachedLocation(
        IGameDbId characterUid,
        ISlotId slotPath)
    {
        EntityId = characterUid;
        SlotPath = slotPath;
    }

    public override string ToString()
    {
        return $"EquippedLocation(character:{EntityId} slotPath:{SlotPath})";
    }
}