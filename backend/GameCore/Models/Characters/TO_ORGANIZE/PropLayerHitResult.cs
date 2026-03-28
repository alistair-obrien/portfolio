using System.Collections.Generic;

public sealed record PropLayerHitResult(
    bool DidHit,
    IReadOnlyDictionary<string, DamageInstance> StructuralDamageInstances,
    IReadOnlyDictionary<string, DamageInstance> CyberneticDamageInstances,
    int EnergyAppliedToObject,
    int EnergyConsumedByDamage,
    int LeftoverEnergy,
    PropLayerId LayerId,
    PropLayer PropLayerSnapshot,
    CombatAPI.Events.PropLayerDamagedEvent PropLayerDamagedEvent
    );
