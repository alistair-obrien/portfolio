using System.Collections.Generic;

public sealed record HitRayPresentation(
    int AttackerTileX,
    int AttackerTileY,
    int EndTileX,
    int EndTileY,

    float AttackStartPositionX,
    float AttackStartPositionY,
    float RayStoppedPositionX,
    float RayStoppedPositionY,
    float AttackEndPositionX,
    float AttackEndPositionY,
    float ShotRadiusInTiles,

    List<RangeEffectivenessPresentation> rangeEffectivenessData,

    DamageType DamageType,
    IReadOnlyList<HitResult> HitResults);