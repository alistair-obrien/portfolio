public sealed record SetCharacterDialogueRequest(
    string CharacterUid,
    string DialogueNode
    ) : IGameCommand
{
    public string Name => "Set Character Dialogue";
}
