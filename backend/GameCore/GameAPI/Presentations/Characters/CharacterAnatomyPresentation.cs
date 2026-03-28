using System.Collections.Generic;

public class CharacterAnatomyPresentation
{
    public readonly List<OrganicBodyPartPresentation> AllBodyParts;

    public CharacterAnatomyPresentation(AnatomyComponent anatomyComponent)
    {
        foreach (var bodyPart in anatomyComponent.GetAllBodyParts())
        {
            AllBodyParts.Add(new OrganicBodyPartPresentation(bodyPart.Key, bodyPart.Value));
        }
    }
}
