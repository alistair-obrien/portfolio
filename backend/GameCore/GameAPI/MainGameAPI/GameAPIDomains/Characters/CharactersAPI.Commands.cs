using System;

public sealed partial class CharactersAPI
{
    public sealed class Commands
    {
        public sealed record AssignPlayerCharacter(
            CharacterId? CharacterUid
            ) : IGameCommand;
    }
}