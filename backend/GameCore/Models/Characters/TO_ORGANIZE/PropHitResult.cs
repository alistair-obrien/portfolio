using System.Collections.Generic;

public sealed record PropHitResult(
    MapChunkId MapId,
    PropId PropId,
    Prop PropSnapshot,
    float HitdirectionX,
    float HitdirectionY,
    int HitCellX,
    int HitCellY,
    float HitPointX,
    float HitPointY,

    IReadOnlyList<PropLayerHitResult> LayerHitResults,

    int EnergyAppliedToObject,
    int EnergyConsumedByDamage,
    int LeftoverEnergy,

    bool DidHit,

    MoveResult PushResult
    ) : HitResult(
        MapId,
        HitdirectionX,
        HitdirectionY,
        HitCellX, HitCellY,
        HitPointX, HitPointY,
        0,                     // ResistedEnergy (world objects don't resist)
        EnergyAppliedToObject, // TotalEnergyApplied
        LeftoverEnergy,
        DidHit); //Inference hack