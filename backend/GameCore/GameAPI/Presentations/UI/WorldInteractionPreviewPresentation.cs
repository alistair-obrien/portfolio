public record WorldInteractionPreviewPresentation(
    Vec2Int Cell,
    string PrimaryActionLabel,
    bool HasCharacter,
    bool HasItem,
    bool HasWorldObject
);