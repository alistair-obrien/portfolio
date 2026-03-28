using System.Collections.Generic;


public sealed record CharacterHitResult(

    MapChunkId MapId,
    float HitdirectionX,
    float HitdirectionY,
    int HitCellX,
    int HitCellY,
        
    float HitPointX,
    float HitPointY,

    CharacterId TargetId,
    Character TargetSnapshot,

    bool DidHit,

    // Damage Instances
    IReadOnlyDictionary<ItemId, Item> ItemSnapshots,
    IReadOnlyDictionary<ItemId, DamageInstance> ClothingDamageInstances,
    IReadOnlyDictionary<ItemId, DamageInstance> ModFrameDamageInstances,
    IReadOnlyDictionary<(CharacterId CharacterUid, AnatomySlotId BodyPartSlotPath), DamageInstance> BodyPartInjuryInstances,

    // Energy
    int ResistedEnergy,
    int TotalEnergyAppliedToCharacter,
    int LeftoverEnergy,

    // Pushed
    MoveResult PushResult) : HitResult(
        MapId,
        HitdirectionX,
        HitdirectionY,
        HitCellX, 
        HitCellY,
        HitPointX,
        HitPointY,
        ResistedEnergy, 
        TotalEnergyAppliedToCharacter, 
        LeftoverEnergy, 
        DidHit);
