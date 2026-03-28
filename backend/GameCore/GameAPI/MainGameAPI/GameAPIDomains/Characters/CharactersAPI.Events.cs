
public sealed partial class CharactersAPI
{
    public sealed class Events
    {
        public sealed record CharacterLoadoutUpdated(
            CharacterId CharacterId,
            CharacterLoadoutPresentation CharacterLoadoutPresentation
        ) : IGameEvent;

        public sealed record PlayerCharacterAssigned(
            CharacterId? OldCharacterId,
            CharacterId? NewCharacterId,
            PlayerInteractionPresentation PlayerInteractionPresentation
        ) : IGameEvent;
    }
}
