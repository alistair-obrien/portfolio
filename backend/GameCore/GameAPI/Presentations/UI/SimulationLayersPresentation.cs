using System.Collections.Generic;

public enum SimulationEntityChangeKind
{
    Unchanged,
    Added,
    Removed,
    Changed
}

public sealed record SimulationEntityPresentation(
    IGameDbId EntityId,
    string Id,
    string Name,
    string Type,
    int Depth,
    SimulationEntityChangeKind ChangeKind,
    string Details
);

public sealed record SimulationLayersPresentation(
    IReadOnlyList<SimulationEntityPresentation> Entities,
    int MaxDepth
);
