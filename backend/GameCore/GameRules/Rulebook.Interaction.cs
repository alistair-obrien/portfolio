using System.Collections.Generic;
using System.Linq;

public partial class Rulebook
{
    internal sealed class InteractionRules : RulebookSection
    {
        private ItemStackAPI ItemStackAPI => Rulebook.GameAPI.Items.ItemStackAPI;
        private EntityLocationAPI EntityLocationAPI => Rulebook.GameAPI.Databases.EntityLocationAPI;
        private EntityAttachmentAPI EntityAttachmentAPI => Rulebook.GameAPI.Databases.EntityAttachmentAPI;

        public InteractionRules(Rulebook rulebook) : base(rulebook) { }

        // =============================================================
        // PRIMARY SELECTION
        // =============================================================

        internal InteractionRequest SelectPrimaryAction(
            IEnumerable<InteractionRequest> actions)
        {
            return actions.FirstOrDefault();
        }

        // =============================================================
        // ITEM → ITEM
        // =============================================================

        internal IEnumerable<InteractionRequest> GetAvailableActionsOnItem(
            Character self,
            Item actingItem,
            Item targetItem)
        {
            // Ammo → Gun
            if (actingItem != null && targetItem != null && actingItem.GetIsAmmo() && targetItem.GetIsGun())
            {
                yield return new InteractionRequest(
                    "Load Ammo",
                    new ItemsAPI.Commands.MoveEntity(
                        self.Id,
                        actingItem.Id,
                        new GunAmmoLocation(targetItem.Id),
                        AllowSwap: true));
            }

            // Stack merge
            if (actingItem != null && targetItem != null && 
                ItemStackAPI.CanMergeStacks(
                    self,
                    actingItem,
                    targetItem))
            {
                yield return new InteractionRequest(
                    "Merge Stack",
                    new ItemsAPI.Commands.MergeStack(
                        self.Id,
                        actingItem.Id,
                        targetItem.Id,
                        0));
            }

            // Place into container item
            if (targetItem != null && targetItem.GetIsInventory())
            {
                yield return new InteractionRequest(
                    "Place Inside",
                    new ItemsAPI.Commands.MoveEntity(
                        self.Id,
                        actingItem.Id,
                        new InventoryLocation(targetItem.Id),
                        AllowSwap: false));
            }

            if (targetItem != null && actingItem != null)
            {
                EntityLocationAPI.TryFindEntityLocation(targetItem.Id, out var itemLocation);

                yield return new InteractionRequest(
                    "Swap",
                    new ItemsAPI.Commands.MoveEntity(
                        self.Id,
                        actingItem.Id,
                        itemLocation,
                        AllowSwap: true));
            }
        }

        // =============================================================
        // ITEM → LOCATION
        // =============================================================

        internal IEnumerable<InteractionRequest> GetAvailableActionsOnLocation(
            Character self,
            Item actingItem,
            IGameModelLocation location)
        {
            if (location is IItemLocation itemLocation)
            {
                // Generic placement
                if (actingItem != null && EntityAttachmentAPI.CanAttachToItem(
                    self.Id,
                    actingItem.Id,
                    itemLocation,
                    allowSwap: true))
                {
                    yield return new InteractionRequest(
                        "Place",
                        new ItemsAPI.Commands.MoveEntity(
                            self.Id,
                            actingItem.Id,
                            itemLocation,
                            AllowSwap: true));
                }
            }
        }

        // =============================================================
        // EMPTY HAND → ITEM
        // =============================================================

        internal IEnumerable<InteractionRequest> GetAvailableActionsOnItem(
            Character actor,
            Item targetItem)
        {
            yield return new InteractionRequest(
                "Hold",
                new ItemsAPI.Commands.EquipItem(actor.Id, targetItem.Id, SlotIds.Loadout.HeldItem));

            //// Unequip
            //if (actor.TryGetEquippedSlot(targetItem.Id, out var slotPath))
            //{
            //    yield return new InteractionRequest(
            //        "Unequip",
            //        new ItemsAPI.Commands.UnequipItem(actor.Id, targetItem.Id, slotPath));
            //}
            // Take
            //else 
            if (actor.InventoryItemId.HasValue)
            {
                if (Rulebook.GameAPI.Databases.TryGetModel(actor.InventoryItemId.Value, out Item inventoryItem) &&
                    inventoryItem.GetIsInventory() &&
                    inventoryItem.Inventory != null)
                {
                    if (!inventoryItem.Inventory.ContainsItem(targetItem.Id))
                        yield return new InteractionRequest(
                            "Take",
                            new ItemsAPI.Commands.TakeItem(
                                actor.Id,
                                targetItem.Id));
                }
            }

            //yield return new InteractionRequest(
            //    "Inspect", 
            //    new ItemsAPI.Commands.InspectItem(
            //        self.Uid, 
            //        targetItem.Uid));

            // Open container
            if (targetItem.Inventory != null)
            {
                yield return new InteractionRequest(
                    "Open",
                    new ItemsAPI.Commands.OpenInventory(actor.Id, targetItem.Id));
            }

            // Gun unload
            if (targetItem.Gun != null &&
                targetItem.Gun.TryGetLoadedAmmo(out var ammoItem))
            {
                yield return new InteractionRequest(
                    "Unload",
                    new ItemsAPI.Commands.UnloadGun(
                        actor.Id,
                        targetItem.Id));
            }

            // Split stack
            if (targetItem.MaxStackCount > 0 &&
                targetItem.CurrentStackCount > 1)
            {
                if (actor.InventoryItemId.HasValue &&
                    Rulebook.GameAPI.Databases.TryGetModel(actor.InventoryItemId.Value, out Item inventoryItem) &&
                    inventoryItem.GetIsInventory() &&
                    inventoryItem.Inventory != null)
                {
                    yield return new InteractionRequest(
                        "Split Stack",
                        new ItemsAPI.Commands.SplitStack(
                            actor.Id,
                            targetItem.Id,
                            new InventoryLocation(actor.InventoryItemId.Value),
                            0));
                }
            }
        }

        // =============================================================
        // CHARACTER → CHARACTER
        // =============================================================

        internal IEnumerable<InteractionRequest> GetAvailableActionsOnCharacter(
            Character self,
            Character target,
            Item actingItem)
        {
            yield return new InteractionRequest(
                "Inspect",
                new InspectCharacterRequest(self.Id, target.Id));

            yield return new InteractionRequest(
                "Health Report",
                new InspectCharacterHealthReportRequest(
                    self.Id,
                    target.Id));

            if (Rulebook.CombatSection.IsCharacterDead(target))
            {
                yield return new InteractionRequest(
                    "Loot",
                    new LootCharacterRequest(self.Id, target.Id));
            }
            else
            {
                if (CanTalkToCharacter(self, target))
                {
                    yield return new InteractionRequest(
                        "Talk",
                        new TalkRequest(
                            self.Id,
                            target.Id,
                            target.DialogueNode));
                }

                //if (Rulebook.CombatSection
                //    .CanAttackCharacterWithPrimaryWeapon(self, target))
                //{
                //    yield return new InteractionRequest(
                //        "Attack",
                //        new CombatAPI.Commands.AttackTarget(
                //            self.Id,
                //            target.Id,
                //            SlotIds.Loadout.PrimaryWeapon));
                //}
            }
        }

        internal IEnumerable<InteractionRequest> GetAvailableActionsOnWorldObject(
            Character self,
            Prop target,
            Item actingItem)
        {
            // Nothing implemented yet
            yield break;
        }

        internal IEnumerable<InteractionRequest> GetAvailableActionsOnEmptyWorldTile(Character character, MapLocation mapLocation)
        {
            yield return new InteractionRequest(
                "Move",
                new MapsAPI.Commands.MoveCharacterAlongPathToCell(
                    character.Id,
                    mapLocation.MapId,
                    character.Id,
                    mapLocation.CellFootprint.X, 
                    mapLocation.CellFootprint.Y));
        }

        internal InteractionRequest CreateBuildOnMapInteraction(
            Character character,
            IGameDbId prototypeEntityId,
            BulkLocationTarget target,
            IReadOnlyList<CellPosition> acceptedCells)
        {
            var commands = acceptedCells
                .Select(cell => (IGameCommand)new InteractionsAPI.Commands.BuildPrototypeOnMap(
                    character.Id,
                    prototypeEntityId,
                    target.MapId,
                    cell))
                .ToList();

            return new InteractionRequest("Build", commands);
        }

        internal IEnumerable<InteractionRequest> GetDirectionalActions(
            Character character,
            ItemId actingItemId,
            Vec2 direction)
        {
            direction = direction.normalized;

            if (!TryResolve(actingItemId, out Item item))
                yield break;

            if (item.GetIsGun())
            {
                yield return CreateAttackInDirectionInteraction(
                    character,
                    actingItemId,
                    direction);
            }
        }

        private InteractionRequest CreateAttackInDirectionInteraction(
            Character character,
            ItemId weaponItemId, 
            Vec2 direction)
        {
            return new InteractionRequest("Attack In Direction", new CombatAPI.Commands.AttackInDirection(
                character.Id,
                weaponItemId, 
                direction.x, 
                direction.y));
        }

        // =============================================================
        // HELPERS
        // =============================================================

        internal bool CanTalkToCharacter(
            Character self,
            Character target)
        {
            if (self == target) return false;
            if (Rulebook.CombatSection.IsCharacterDead(self)) return false;
            if (Rulebook.CombatSection.IsCharacterDead(target)) return false;
            if (string.IsNullOrEmpty(target.DialogueNode)) return false;

            return true;
        }
    }
}
