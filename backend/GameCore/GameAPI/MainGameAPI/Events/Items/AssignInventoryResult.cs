public sealed record AssignInventoryResult(
    Character SelfSnapshot,
    Item InventoryItemSnapshot) : IGameEvent;
