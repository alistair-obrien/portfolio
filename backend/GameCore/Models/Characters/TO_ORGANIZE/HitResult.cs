public record HitResult(
    MapChunkId MapId,
    float HitdirectionX,
    float HitdirectionY,
    int HitCellX,
    int HitCellY,
    float HitPointX,
    float HitPointY,
    int ResistedEnergy,
    int TotalEnergyAppliedToCharacter, 
    int LeftoverEnergy,
    bool DidHit) : TurnMutation;
