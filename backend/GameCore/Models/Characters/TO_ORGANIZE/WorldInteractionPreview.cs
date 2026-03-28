using System.Collections.Generic;

public sealed record WorldInteractionPreview(
    string MapUid, 
    int TileX, 
    int TileY, 
    bool HasCharacter, 
    bool HasItem, 
    bool HasWorldObject, 
    IEnumerable<InteractionRequest> PossibleActions,
    InteractionRequest PrimaryAction);