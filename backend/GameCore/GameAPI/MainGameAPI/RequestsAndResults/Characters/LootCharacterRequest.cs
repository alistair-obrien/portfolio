public sealed record TreatInjuryRequest(
    CharacterId SelfUid,
    CharacterId TargetUid, 
    ISlotId BodySlotPath, 
    int InjuryIndex, 
    string treatment) : IGameCommand
{
    public string Name => "Treat Injury";
}

public sealed record LootCharacterRequest(CharacterId SelfUid, CharacterId TargetUid) : IGameCommand
{
    public string Name => "Loot";
}

public sealed record CreateCharacterRequest(
    CharacterId CharacterId
) : IGameCommand
{
    public string Name => "Create Character";
}
