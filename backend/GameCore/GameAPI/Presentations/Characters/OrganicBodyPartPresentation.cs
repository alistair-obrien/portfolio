using System.Collections.Generic;

public class OrganicBodyPartPresentation
{
    public readonly AnatomySlotId SlotId;
    public readonly string Name;
    public readonly string Description;
    //public readonly string PartName;
    //public readonly string SpritePath; //TODO: Render Key

    public readonly int MaxInjuryCapacity;
    public readonly int RemainingInjuryCapacity;

    public string ConditionDescription;
    public List<DamageInstancePresentation> Injuries = new();

    public OrganicBodyPartPresentation(AnatomySlotId slot, OrganicBodyPartComponent organicBodyPartComponent)
    {
        SlotId = slot;
        Name = organicBodyPartComponent.Name;
        Description = organicBodyPartComponent.Description;
        //SpritePath = SlotIds.Organic.GetBodySlotImagePath(organicBodyPartComponent.OrganicBodySlotPath);

        MaxInjuryCapacity = organicBodyPartComponent.GetMaxInjuryCapacity();
        RemainingInjuryCapacity = organicBodyPartComponent.GetRemainingInjuryCapacity();

        ConditionDescription = $"{organicBodyPartComponent.GetConditionDescription()}";

        foreach (var injury in organicBodyPartComponent.GetInjuries())
        {
            Injuries.Add(new DamageInstancePresentation(injury));
        }
    }
}
