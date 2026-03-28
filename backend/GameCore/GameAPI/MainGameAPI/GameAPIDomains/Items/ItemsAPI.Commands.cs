public sealed partial class ItemsAPI
{
    public class Commands
    {
        // MOVEMENT
        public sealed record MoveEntity(
            CharacterId ActorId,
            IGameDbId EntityId,
            IGameModelLocation TargetLocation,
            bool AllowSwap
        ) : IGameCommand;

        public sealed record TakeItem(
            CharacterId ActorId,
            ItemId ItemId
        ) : IGameCommand;

        // EQUIPMENT
        public sealed record EquipItem(
            CharacterId ActorId,
            ItemId ItemId,
            ISlotId EquipmentSlotId
        ) : IGameCommand;

        public sealed record UnequipItem(
            CharacterId ActorId,
            ItemId ItemId,
            ISlotId EquipmentSlotId
        ) : IGameCommand;

        // WEAPONS
        public sealed record LoadGun(
            CharacterId ActorId,
            ItemId GunItemId,
            ItemId AmmoItemId
        ) : IGameCommand;

        public sealed record UnloadGun(
            CharacterId ActorId,
            ItemId GunItemId
        ) : IGameCommand;

        // STACKS
        public sealed record SplitStack(
            CharacterId ActorId,
            ItemId ItemId,
            IItemLocation targetLocation,
            int? AmountToSplit = null
        ) : IGameCommand;

        public sealed record MergeStack(
            CharacterId ActorId,
            ItemId SourceItemId,
            ItemId TargetItemId,
            int? MaxTransferAmount = null
        ) : IGameCommand;

        // INTERACTION
        public sealed record UseItemOnLocation(
            CharacterId ActorId,
            ItemId ItemId,
            IGameModelLocation TargetLocation
        ) : IGameCommand;

        public sealed record UseItemOnItem(
            CharacterId ActorId,
            ItemId ActingItemId,
            ItemId TargetItemId
        ) : IGameCommand;

        public sealed record DropItem(
            CharacterId ActorId,
            ItemId ItemId
        ) : IGameCommand;

        // CONTAINER MANAGEMENT
        public sealed record OpenInventory(
            CharacterId ActorId,
            ItemId InventoryItemId
        ) : IGameCommand;

        public sealed record CloseInventory(
            CharacterId ActorId,
            ItemId InventoryItemId
        ) : IGameCommand;
    }
}