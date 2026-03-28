using System;
using System.Collections.Generic;

public record ClothingSaveData
{
    public StyleSlotId StyleSlotId;
    public float SurfaceArea;
    public List<string> Aesthetics = new();
    public List<string> Traits = new();
    public List<FactionId> FactionAffiliations = new();
}

public class ClothingComponent : ItemComponent
{
    public ClothingSaveData Save()
    {
        return new ClothingSaveData
        {
            StyleSlotId = ClothingSlotPath,
            SurfaceArea = SurfaceArea,
            Aesthetics = Aesthetics,
            Traits = Traits,
            FactionAffiliations = FactionAffiliations
        };
    }

    public StyleSlotId ClothingSlotPath { get; private set; }

    // How much this covers the area. This will define how likely it is to be hit by attacks
    // For example a ring will have very little coverage whereas a full helmet will have a lot
    public float SurfaceArea { get; private set; }
    public List<string> Aesthetics { get; private set; } = new(); //TODO: Create an Aesthetic Id
    public List<string> Traits { get; private set; } = new(); //TODO: Create a Traits Id
    public List<FactionId> FactionAffiliations { get; private set; }

    public ClothingComponent() { }
    public ClothingComponent(ClothingSaveData data) 
    {
        ClothingSlotPath = data.StyleSlotId;
        SurfaceArea = MathF.Max(0, data.SurfaceArea);
        Aesthetics = data.Aesthetics ?? new();
        Traits = data.Traits ?? new();
        FactionAffiliations = data.FactionAffiliations ?? new();
    }

    public HashSet<ISlotId> GetCompatibleSlotPaths()
    {
        if (ClothingSlotPath == new StyleSlotId("character.style.accessory")) //HACK
        {
            return new HashSet<ISlotId>
            {
                SlotIds.Style.Accessory1,
                SlotIds.Style.Accessory2,
                SlotIds.Style.Accessory3,
                SlotIds.Style.Accessory4,
                SlotIds.Style.Accessory5,
            };
        }

        return new HashSet<ISlotId> { ClothingSlotPath };
    }
}