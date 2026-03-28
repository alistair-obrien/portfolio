using System;

public sealed partial class ItemsAPI
{
    public class Events
    {
        // NEW
        public sealed record HoldItemResolved(
            CharacterId ActorId,
            ItemId ItemId,
            bool Success,
            string FailureReason
        ) : IGameEvent;

        public sealed record TakeItemResolved(
            CharacterId ActorId,
            ItemId ItemId,
            bool Success,
            string FailureReason
        ) : IGameEvent;

        // END NEW

        // LIFECYCLE
        public sealed record ItemCreated(
            ItemId ItemId,
            ItemPresentation Presentation
        ) : IGameEvent;

        public sealed record ItemDestroyed(
            ItemId ItemId,
            ItemPresentation Presentation
        ) : IGameEvent;

        // MOVEMENT & LOCATION
        public sealed record ItemUpdated(
            ItemId ItemId,
            ItemPresentation Presentation
        ) : IGameEvent;

        public sealed record ItemAddToInventoryResolved(
            bool Success,
            ItemId ItemId,
            ItemId InventoryItemId,
            CellFootprint CellFootprint,
            ItemPlacementPresentation Presentation
        ) : IGameEvent;

        public sealed record ItemRemovedFromInventory(
            bool Success,
            ItemId ItemId,
            ItemId InventoryItemId
        ) : IGameEvent;

        // EQUIPMENT
        public sealed record ItemEquipped(
            CharacterId CharacterId,
            ItemId ItemId,
            ISlotId EquipmentSlotId,
            ItemSlotPresentation Presentation
        ) : IGameEvent;

        public sealed record ItemUnequipped(
            CharacterId CharacterId,
            ItemId ItemId,
            ISlotId EquipmentSlotId,
            ItemSlotPresentation Presentation
        ) : IGameEvent;

        // WEAPONS
        public sealed record GunLoaded(
            ItemId GunItemId,
            ItemId AmmoItemId,
            ItemPresentation GunPresentation
        ) : IGameEvent;

        public sealed record GunUnloaded(
            ItemId GunItemId,
            ItemId AmmoItemId,
            ItemPresentation GunPresentation
        ) : IGameEvent;

        // STACKS
        public sealed record StackSplit(
            ItemId SourceItemId,
            ItemId NewItemId,
            int AmountSplit,
            ItemPresentation SourcePresentation,
            ItemPresentation NewPresentation
        ) : IGameEvent;

        public sealed record StackMerged(
            ItemId SourceItemId,
            ItemId TargetItemId,
            int AmountMerged,
            ItemPresentation TargetPresentation,
            bool SourceConsumed
        ) : IGameEvent;

        // INTERACTION
        public sealed record ItemUsed(
            CharacterId CharacterId,
            ItemId ItemId,
            IGameModelLocation TargetLocation,
            ItemPresentation Presentation
        ) : IGameEvent;

        // CONTAINER
        public sealed record InventoryOpened(
            CharacterId CharacterId,
            ItemId InventoryItemId
        ) : IGameEvent;

        public sealed record InventoryClosed(
            CharacterId CharacterId,
            ItemId InventoryItemId
        ) : IGameEvent;
    }
}
