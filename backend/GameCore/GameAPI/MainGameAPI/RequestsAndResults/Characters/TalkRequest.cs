public sealed record TalkRequest(
    CharacterId SelfUid,
    CharacterId TargetUid,
    string DialogueNode
) : IGameCommand
{
    public string Name => "Talk";
}