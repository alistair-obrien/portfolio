using System.Collections.Generic;
using System.Linq;

public record PropPreviewPresentation(
    PropId PropId,
    string Name,
    string Description,
    string FlavorText,
    int BaseDamageMitigation,
    AllHealthDomainsPresentation HealthSummary,
    IReadOnlyList<ResistanceData> Resistances
) : IPreviewPresentation
{
    public PropPreviewPresentation(
        GameInstance gameAPI,
        Prop prop)
    : this(
        PropId: prop.Id,
        Name: prop.Name,
        Description: prop.Description,
        FlavorText: prop.FlavorText,
        BaseDamageMitigation: gameAPI.Rulebook.InjuriesAndDamageSection.GetEnergyMitigationOfProp(prop),
        HealthSummary: new AllHealthDomainsPresentation(prop),
        Resistances: prop.GetResistances().ToList()
    )
    {
    }
}
