//TODO: This is an inner structure used for World and Characters Push so doesnt belong here
public sealed record MoveResult(
    bool WasMoved,
    MapChunkId FromMapId,
    CellFootprint From,
    MapChunkId ToMapId,
    CellFootprint To
);