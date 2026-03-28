public sealed record CharacterLinecastHit(
    CharacterPlacementOnMap PlacementOnGrid,
    CellPosition HitCell,
    Vec2 HitPoint,
    int DistanceTiles,
    float Distance) : LinecastHit(HitCell, HitPoint, DistanceTiles, Distance);
