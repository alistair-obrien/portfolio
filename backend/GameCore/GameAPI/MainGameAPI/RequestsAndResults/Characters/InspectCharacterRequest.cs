public sealed record InspectCharacterRequest(CharacterId SelfId, CharacterId TargetId) : IGameCommand
{
    public string Name => "Inspect";
}

public sealed record InspectCharacterHealthReportRequest(CharacterId SelfId, CharacterId TargetId) : IGameCommand
{
    public string Name => "Inspect Health";
}

public sealed record InspectWorldObjectRequest(CharacterId SelfId, MapChunkId MapId, PropId PropId) : IGameCommand
{
    public string Name => "Inspect World Object";
}
