using Newtonsoft.Json;

public class ClothingSlotComponent : BaseEntity
{
    [JsonProperty] public ISlotId ClothingSlotPath { get; private set; }
    [JsonProperty] public ClothingComponent ClothingComponent { get; private set; }

    [JsonConstructor] private ClothingSlotComponent() { }
    public ClothingSlotComponent(ISlotId clothingSlotPath, ClothingComponent clothingComponent)
    {
        ClothingSlotPath = clothingSlotPath;
        ClothingComponent = clothingComponent;
    }
}
