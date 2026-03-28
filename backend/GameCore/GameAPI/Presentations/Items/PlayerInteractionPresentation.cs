public class PlayerInteractionPresentation
{
    public readonly CharacterId characterUid;
    public readonly string OperatingSystemId;
    public readonly AllHealthDomainsPresentation HealthSummary;

    public PlayerInteractionPresentation(Character character)
    {
        characterUid = character.Id;
        OperatingSystemId = character.OperatingSystem?.Id ?? string.Empty;
        HealthSummary = new AllHealthDomainsPresentation(character);
    }
}
