public class AllHealthDomainsPresentation
{
    public HealthPresentation OrganicHealth;
    public HealthPresentation CyberneticHealth;
    public HealthPresentation StructuralHealth;

    public AllHealthDomainsPresentation(Character character)
    {
        OrganicHealth = new HealthPresentation(character.GetCurrentBiologicalHP(), character.GetMaxBiologicalHP(), DamageDomain.Organic);
        CyberneticHealth = new HealthPresentation(character.GetCurrentCyberneticHP(), character.GetMaxCyberneticHP(), DamageDomain.Cybernetic);
        StructuralHealth = new HealthPresentation(0, 0, DamageDomain.Structural);
    }

    public AllHealthDomainsPresentation(Prop worldObject)
    {
        OrganicHealth = new HealthPresentation(worldObject.GetCurrentOrganicHP(), worldObject.GetMaxOrganicHP(), DamageDomain.Organic);
        CyberneticHealth = new HealthPresentation(worldObject.GetCurrentCyberneticHP(), worldObject.GetMaxCyberneticHP(), DamageDomain.Cybernetic);
        StructuralHealth = new HealthPresentation(worldObject.GetCurrentStructuralHP(), worldObject.GetMaxStructuralHP(), DamageDomain.Structural);
    }
}
