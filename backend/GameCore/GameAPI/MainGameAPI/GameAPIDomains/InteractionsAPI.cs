using System;
using System.Collections.Generic;
using System.Linq;

public class InteractionsAPI : APIDomain
{
    private DatabaseAPI DatabaseAPI => GameAPI.Databases;

    public static class Commands
    {
        public sealed record DeleteEntity(
            CharacterId ActorId,
            IGameDbId EntityId) : IGameCommand;

        public sealed record DetachEntity(
            CharacterId ActorId,
            IGameDbId EntityId) : IGameCommand;

        public sealed record BuildPrototypeOnMap(
            CharacterId ActorId,
            IGameDbId PrototypeEntityId,
            MapChunkId MapId,
            CellPosition CellPosition) : IGameCommand;
    }

    public InteractionsAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    internal override void RegisterHandlers(CommandRouter router)
    {
        router.Register<Commands.DeleteEntity>(HandleDeleteEntity);
        router.Register<Commands.DetachEntity>(HandleDetachEntity);
        router.Register<Commands.BuildPrototypeOnMap>(HandleBuildPrototypeOnMap);
    }

    // Do we need this?
    private bool PresentOptionsToPlayer(PresentOptionsToPlayerRequest mir)
    {
        RaiseEvent(new OptionsPresentedToPlayerCharacter(mir.Title, mir.Options));
        return true;
    }

    // =========================================================
    // CONTEXT-DRIVEN API
    // =========================================================

    private IEnumerable<InteractionRequest> GetDirectionalActions(
        InteractionContext context,
        DirectionTarget target)
    {
        if (!HasRequiredGrants(
                context,
                new RequiredOperatingSystemGrant(
                    OperatingSystemGrantIds.Interactions.Attack,
                    OperatingSystemAccessLevel.Use)))
        {
            yield break;
        }

        if (!TryResolve(context.ActorId, out Character character))
            yield break;

        if (context.ActingEntityId.IsValid)
        {
            if (context.ActingEntityId is ItemId itemId)
            {
                foreach (var action in Rulebook.InteractionSection.GetDirectionalActions(
                    character, 
                    itemId, 
                    target.Direction))
                {
                    yield return action;
                }
            }
        }
    }


    public IEnumerable<InteractionRequest> GetAvailableActions(
        InteractionContext context)
    {
        return context.Target switch
        {
            EntityTarget entityTarget =>
                GetEntityBasedActions(context, entityTarget.Id),

            LocationTarget locationTarget =>
                GetLocationBasedActions(context, locationTarget.Location),

            DirectionTarget directionTarget =>
                GetDirectionalActions(context, directionTarget),

            BulkLocationTarget bulkLocationTarget =>
                GetBulkLocationActions(context, bulkLocationTarget),

            _ => Enumerable.Empty<InteractionRequest>()
        };
    }

    private IEnumerable<InteractionRequest> GetBulkLocationActions(
        InteractionContext context,
        BulkLocationTarget target)
    {
        if (!DatabaseAPI.TryGetModelReadOnly<Character, CharacterId>(context.ActorId, out var actor))
            yield break;

        if (context.ActingEntityId == null ||
            !DatabaseAPI.TryGetModelUntypedReadOnly(context.ActingEntityId, out var actingEntity) ||
            actingEntity is not IMapPlaceable placeable)
        {
            yield break;
        }

        if (!HasRequiredGrants(
                context,
                new RequiredOperatingSystemGrant(
                    OperatingSystemGrantIds.Interactions.Build,
                    OperatingSystemAccessLevel.Write)))
        {
            yield break;
        }

        var acceptedCells = new List<CellPosition>();
        if (target.Cells != null)
        {
            foreach (var cell in target.Cells)
            {
                var footprint = new CellFootprint(cell, target.PlacementSize);
                if (!GameAPI.Maps.CanAdd(target.MapId, (IDbId)placeable.Id, footprint))
                    continue;

                acceptedCells.Add(cell);
            }
        }

        if (acceptedCells.Count == 0)
            yield break;

        yield return Rulebook.InteractionSection.CreateBuildOnMapInteraction(
            actor,
            placeable.Id,
            target,
            acceptedCells);
    }

    private IEnumerable<InteractionRequest> GetEntityBasedActions(
        InteractionContext context,
        IGameDbId targetEntityId)
    {
        var actorId = context.ActorId;
        var actingItemId = context.ActingEntityId;

        if (!DatabaseAPI.TryGetModelReadOnly<Character, CharacterId>(actorId, out var actor))
            yield break;

        if (!targetEntityId.IsValid)
            yield break;

        if (!DatabaseAPI.TryGetModelUntypedReadOnly(targetEntityId, out var targetEntity))
            yield break;

        // Editor/system-only actions that should not depend on location resolution.
        bool canDetach = targetEntity.AttachedLocation != null &&
                         targetEntity.AttachedLocation is not RootLocation;

        if (HasRequiredGrants(
                context,
                new RequiredOperatingSystemGrant(
                    OperatingSystemGrantIds.Interactions.DetachEntity,
                    OperatingSystemAccessLevel.Write)))
        {
            if (canDetach)
            {
                yield return new InteractionRequest(
                    "Detach",
                    new Commands.DetachEntity(actorId, targetEntityId));
            }
        }

        if (HasRequiredGrants(
                context,
                new RequiredOperatingSystemGrant(
                    OperatingSystemGrantIds.Interactions.DeleteEntity,
                    OperatingSystemAccessLevel.Write)))
        {
            yield return new InteractionRequest(
                "Delete",
                new Commands.DeleteEntity(actorId, targetEntityId));
        }

        if (HasRequiredGrants(
                context,
                new RequiredOperatingSystemGrant(
                    OperatingSystemGrantIds.Interactions.DuplicateEntity,
                    OperatingSystemAccessLevel.Write)))
        {
            yield return new InteractionRequest(
                "Duplicate",
                new DatabaseAPI.Commands.DuplicateEntity(targetEntityId));
        }

        IGameDbResolvable actingEntity = null;
        if (actingItemId != null)
            DatabaseAPI.TryGetModelUntypedReadOnly(actingItemId, out actingEntity);

        if (actingEntity == null)
        {
            if (targetEntity is Character targetCharacter)
            {
                foreach (var i in GetCharacterActions(context, actor, targetCharacter))
                {
                    if (!canDetach && string.Equals(i.Name, "Detach", StringComparison.OrdinalIgnoreCase))
                        continue;

                    yield return i;
                }
            }

            if (targetEntity is Item targetItem)
            {
                foreach (var i in Rulebook.InteractionSection
                    .GetAvailableActionsOnItem(actor, targetItem))
                    yield return i;
            }

            if (targetEntity is Prop targetProp)
            {
                foreach (var i in Rulebook.InteractionSection
                    .GetAvailableActionsOnWorldObject(actor, targetProp, null))
                    yield return i;
            }
        }
        else if (actingEntity is Item actingItem && targetEntity is Item targetItem)
        {
            foreach (var i in Rulebook.InteractionSection
                .GetAvailableActionsOnItem(actor, actingItem, targetItem))
                yield return i;

            // Also allow item-on-location actions for the target item's current location.
            if (targetEntity.AttachedLocation != null)
            {
                foreach (var i in Rulebook.InteractionSection
                    .GetAvailableActionsOnLocation(actor, actingItem, targetEntity.AttachedLocation))
                {
                    yield return i;
                }
            }
        }
        else if (actingEntity is Item characterActingItem && targetEntity is Character targetCharacter)
        {
            foreach (var action in GetItemOnCharacterActions(actorId, characterActingItem, targetCharacter))
                yield return action;
        }
        else if (actingEntity is Item locationActingItem)
        {
            // Item dragged over non-item entities should still surface location-driven options
            // (for example, place into equipped/inventory/map locations represented by the target entity).
            if (targetEntity.AttachedLocation != null)
            {
                foreach (var i in Rulebook.InteractionSection
                    .GetAvailableActionsOnLocation(actor, locationActingItem, targetEntity.AttachedLocation))
                {
                    yield return i;
                }
            }
        }
    }

    private IEnumerable<InteractionRequest> GetCharacterActions(
        InteractionContext context,
        Character actor,
        Character targetCharacter)
    {
        if (HasRequiredGrants(
                context,
                new RequiredOperatingSystemGrant(
                    OperatingSystemGrantIds.Interactions.PossessCharacter,
                    OperatingSystemAccessLevel.Admin)))
        {
            yield return new InteractionRequest(
                "Possess",
                new CharactersAPI.Commands.AssignPlayerCharacter(targetCharacter.Id));
        }

        foreach (var action in Rulebook.InteractionSection
                     .GetAvailableActionsOnCharacter(actor, targetCharacter, null))
        {
            if (string.Equals(action.Name, "Talk", StringComparison.OrdinalIgnoreCase) &&
                !HasRequiredGrants(
                    context,
                    new RequiredOperatingSystemGrant(
                        OperatingSystemGrantIds.Interactions.Talk,
                        OperatingSystemAccessLevel.Use)))
            {
                continue;
            }

            yield return action;
        }
    }

    private IEnumerable<InteractionRequest> GetItemOnCharacterActions(
        CharacterId actorId,
        Item actingItem,
        Character targetCharacter)
    {
        // Intentionally explicit labels for editor drag/drop.
        if (targetCharacter.CanEquip(actingItem, SlotIds.Loadout.PrimaryWeapon))
        {
            yield return new InteractionRequest(
                "Equip Weapon",
                new ItemsAPI.Commands.MoveEntity(
                    actorId,
                    actingItem.Id,
                    new AttachedLocation(targetCharacter.Id, SlotIds.Loadout.PrimaryWeapon),
                    false));
        }

        if (targetCharacter.CanEquip(actingItem, SlotIds.Loadout.Armor))
        {
            yield return new InteractionRequest(
                "Equip Armor",
                new ItemsAPI.Commands.MoveEntity(
                    actorId,
                    actingItem.Id,
                    new AttachedLocation(targetCharacter.Id, SlotIds.Loadout.Armor),
                    false));
        }

        if (targetCharacter.CanEquip(actingItem, SlotIds.Loadout.Inventory))
        {
            yield return new InteractionRequest(
                "Equip Inventory",
                new ItemsAPI.Commands.MoveEntity(
                    actorId,
                    actingItem.Id,
                    new AttachedLocation(targetCharacter.Id, SlotIds.Loadout.Inventory),
                    false));
        }

        if (targetCharacter.CanEquip(actingItem, SlotIds.Loadout.HeldItem))
        {
            yield return new InteractionRequest(
                "Equip Held Item",
                new ItemsAPI.Commands.MoveEntity(
                    actorId,
                    actingItem.Id,
                    new AttachedLocation(targetCharacter.Id, SlotIds.Loadout.HeldItem),
                    false));
        }
    }

    private IEnumerable<InteractionRequest> GetLocationBasedActions(
        InteractionContext context,
        IGameModelLocation location)
    {
        var actorId = context.ActorId;
        var actingItemId = context.ActingEntityId;

        if (!DatabaseAPI.TryGetModelReadOnly<Character, CharacterId>(actorId, out var actor)) 
            yield break;

        IGameDbResolvable actingEntity = null;
        if (actingItemId != null)
            DatabaseAPI.TryGetModelUntypedReadOnly(actingItemId, out actingEntity);

        IGameDbResolvable targetEntity = null;
        if (DatabaseAPI.EntityLocationAPI.TryGetEntityFromLocation(location, out var targetEntityId))
            DatabaseAPI.TryGetModelUntypedReadOnly(targetEntityId, out targetEntity);

        if (actorId == Root.SYSTEM_ID)
        {
            if (targetEntity == null)
            {
                if (location is AttachedLocation attachedLocation)
                {
                    if (attachedLocation.SlotPath is LoadoutSlotId loadoutSlotId)
                    {
                        if (loadoutSlotId == SlotIds.Loadout.PrimaryWeapon)
                        {
                            var newGunItem = new ItemBlueprint
                            {
                                Id = ItemId.New(),
                                Name = "New Gun",
                                Gun = new GunSaveData()
                            };

                            yield return new InteractionRequest(
                                "Create New Weapon",
                                new List<IGameCommand>
                                {
                                    new DatabaseAPI.Commands.CreateOrUpdateModel(newGunItem),
                                    new ItemsAPI.Commands.MoveEntity(actorId, newGunItem.Id, attachedLocation, false)
                                });

                            yield return new InteractionRequest(
                                "Assign Existing Weapon",
                                new List<IGameCommand>());
                        }
                        else if (loadoutSlotId == SlotIds.Loadout.Inventory)
                        {
                            var newInventoryItem = new ItemBlueprint
                            {
                                Id = ItemId.New(),
                                Name = "New Inventory",
                                Inventory = new InventoryBlueprint
                                {
                                    Columns = 8,
                                    Rows = 8
                                }
                            };

                            yield return new InteractionRequest(
                                "Create New Inventory",
                                new List<IGameCommand>
                                {
                                    new DatabaseAPI.Commands.CreateOrUpdateModel(newInventoryItem),
                                    new ItemsAPI.Commands.MoveEntity(actorId, newInventoryItem.Id, attachedLocation, false)
                                });

                            yield return new InteractionRequest(
                                "Assign Existing Inventory",
                                new List<IGameCommand>());
                        }
                        else if (loadoutSlotId == SlotIds.Loadout.Armor)
                        {
                            var newArmorItem = new ItemBlueprint
                            {
                                Id = ItemId.New(),
                                Name = "New Armor",
                                Armor = new ArmorBlueprint()
                            };

                            yield return new InteractionRequest(
                                "Create New Armor",
                                new List<IGameCommand>
                                {
                                    new DatabaseAPI.Commands.CreateOrUpdateModel(newArmorItem),
                                    new ItemsAPI.Commands.MoveEntity(actorId, newArmorItem.Id, attachedLocation, false)
                                });

                            yield return new InteractionRequest(
                                "Assign Existing Armor",
                                new List<IGameCommand>());
                        }
                        else if (loadoutSlotId == SlotIds.Loadout.HeldItem)
                        {
                            var newItem = new ItemBlueprint
                            {
                                Id = ItemId.New(),
                                Name = "New Item"
                            };

                            yield return new InteractionRequest(
                                "Create New Item",
                                new List<IGameCommand>
                                {
                                    new DatabaseAPI.Commands.CreateOrUpdateModel(newItem),
                                    new ItemsAPI.Commands.MoveEntity(actorId, newItem.Id, attachedLocation, false)
                                });
                            yield return new InteractionRequest(
                                "Assign Existing Item",
                                new List<IGameCommand>());
                        }
                    }
                }
            }
            else
            {
                if (location is AttachedLocation attachedLocation &&
                    HasRequiredGrants(
                        context,
                        new RequiredOperatingSystemGrant(
                            OperatingSystemGrantIds.Interactions.DetachEntity,
                            OperatingSystemAccessLevel.Write)))
                {
                    yield return new InteractionRequest(
                        "Detach",
                        new Commands.DetachEntity(actorId, targetEntityId));
                }
            }
        }

        // =====================================================
        // EMPTY HAND
        // =====================================================
        if (actingEntity == null)
        {
            bool isEmpty = true;

            if (targetEntity is Character targetCharacter)
            {
                isEmpty = false;
                foreach (var i in GetCharacterActions(context, actor, targetCharacter))
                    yield return i;
            }

            if (targetEntity is Item targetItem)
            {
                isEmpty = false;
                foreach (var i in Rulebook.InteractionSection
                    .GetAvailableActionsOnItem(
                        actor,
                        targetItem))
                    yield return i;
            }

            if (targetEntity is Prop targetProp)
            {
                isEmpty = false;
                foreach (var i in Rulebook.InteractionSection
                    .GetAvailableActionsOnWorldObject(
                        actor,
                        targetProp,
                        null))
                    yield return i;
            }

            // Empty tile → move
            if (isEmpty && location is MapLocation world)
            {
                if (!HasRequiredGrants(
                        context,
                        new RequiredOperatingSystemGrant(
                            OperatingSystemGrantIds.Interactions.Move,
                            OperatingSystemAccessLevel.Use)))
                {
                    yield break;
                }

                foreach (var i in Rulebook.InteractionSection
                    .GetAvailableActionsOnEmptyWorldTile(actor, world))
                {
                    yield return i;
                }
            }
        }
        else
        {
            if (actingEntity is Item actingItem)
            {
                // =====================================================
                // ITEM → ITEM
                // =====================================================
                if (targetEntity is Item targetItem && targetItem.Id != actingItem.Id)
                {
                    foreach (var i in Rulebook.InteractionSection
                        .GetAvailableActionsOnItem(
                            actor,
                            actingItem,
                            targetItem))
                        yield return i;
                }

                // =====================================================
                // ITEM → LOCATION
                // =====================================================
                foreach (var i in Rulebook.InteractionSection
                    .GetAvailableActionsOnLocation(
                        actor,
                        actingItem,
                        location))
                    yield return i;
            }
        }
    }

    private bool HasRequiredGrants(
        InteractionContext context,
        params RequiredOperatingSystemGrant[] grants)
    {
        return GameAPI.OperatingSystems.HasAllGrants(
            context.ActorId,
            grants);
    }

    private bool TrySelectPrimary(
        IEnumerable<InteractionRequest> actions,
        out InteractionRequest interaction)
    {
        interaction = default;
        var list = actions.ToList();
        if (!list.Any())
            return false;

        interaction = GameAPI.Rulebook.InteractionSection
            .SelectPrimaryAction(list);

        return interaction.Commands != null;
    }

    public bool TryGetPrimaryInteraction(
        InteractionContext context,
        out InteractionRequest interaction)
    {
        interaction = default;

        var actions = GetAvailableActions(context).ToList();
        if (!actions.Any())
            return false;

        return TrySelectPrimary(actions, out interaction);
    }

    public bool CanInteract(InteractionContext context)
    {
        return GetAvailableActions(context).Any();
    }

    // =========================================================
    // CONTEXT RESOLUTION
    // =========================================================

    internal bool TryGetInteraction(
        InteractionContext context,
        string optionKey,
        out InteractionRequest interaction)
    {
        interaction = default;

        if (string.IsNullOrEmpty(optionKey))
            return false;

        var actions = GetAvailableActions(context);

        foreach (var action in actions)
        {
            if (action.OptionKey == optionKey)
            {
                if (action.Commands == null)
                    return false;

                interaction = action;
                return true;
            }
        }

        return false;
    }

    private CommandResult HandleBuildPrototypeOnMap(Commands.BuildPrototypeOnMap command)
    {
        if (command.PrototypeEntityId == null || !command.MapId.IsValid)
            return Fail($"Build request is invalid. Prototype={command.PrototypeEntityId}, Map={command.MapId}.");

        if (!GameAPI.OperatingSystems.HasGrant(
                command.ActorId,
                OperatingSystemGrantIds.Interactions.Build,
                OperatingSystemAccessLevel.Write))
        {
            return Fail($"Actor {command.ActorId} does not have build permission.");
        }

        if (!GameAPI.Databases.TryDuplicateModel(command.PrototypeEntityId, out var newInstanceId))
            return Fail($"Failed to duplicate prototype {command.PrototypeEntityId} for build placement.");

        var addResult = GameAPI.TryExecuteCommand(new MapsAPI.Commands.AddEntityToMap(
                command.ActorId,
                command.MapId,
                newInstanceId,
                command.CellPosition));
        if (addResult.Ok)
        {
            return Ok();
        }

        GameAPI.TryExecuteCommand(new DatabaseAPI.Commands.RemoveEntity(newInstanceId));
        return Fail($"Failed to place duplicated prototype {newInstanceId} on map {command.MapId}: {addResult.ErrorMessage}");
    }

    private CommandResult HandleDeleteEntity(Commands.DeleteEntity command)
    {
        if (command.EntityId == null || !command.EntityId.IsValid)
            return Fail("Delete request did not include a valid entity id.");

        if (!GameAPI.OperatingSystems.HasGrant(
                command.ActorId,
                OperatingSystemGrantIds.Interactions.DeleteEntity,
                OperatingSystemAccessLevel.Write))
        {
            return Fail($"Actor {command.ActorId} does not have delete permission.");
        }

        var result = GameAPI.TryExecuteCommand(new DatabaseAPI.Commands.RemoveEntity(command.EntityId));
        return result.Ok
            ? Ok()
            : Fail($"Failed to delete entity {command.EntityId}: {result.ErrorMessage}");
    }

    private CommandResult HandleDetachEntity(Commands.DetachEntity command)
    {
        if (command.EntityId == null || !command.EntityId.IsValid)
            return Fail("Detach request did not include a valid entity id.");

        if (!GameAPI.OperatingSystems.HasGrant(
                command.ActorId,
                OperatingSystemGrantIds.Interactions.DetachEntity,
                OperatingSystemAccessLevel.Write))
        {
            return Fail($"Actor {command.ActorId} does not have detach permission.");
        }

        var result = GameAPI.TryExecuteCommand(new ItemsAPI.Commands.MoveEntity(
            command.ActorId,
            command.EntityId,
            new RootLocation(),
            false));

        return result.Ok
            ? Ok()
            : Fail($"Failed to detach entity {command.EntityId}: {result.ErrorMessage}");
    }

    internal bool TryCreatePreviewFromContext(
        InteractionContext context,
        out IPreviewPresentation preview)
    {
        preview = null;

        return context.Target switch
        {
            LocationTarget locationTarget =>
                TryCreatePreviewFromLocation(context.ActorId, locationTarget.Location, out preview),

            DirectionTarget directionTarget =>
                TryCreatePreviewFromDirection(context.ActorId, directionTarget.Direction, out preview),

            _ => false
        };
    }

    private bool TryCreatePreviewFromLocation(
        CharacterId actorId, 
        IGameModelLocation location, 
        out IPreviewPresentation preview)
    {
        preview = null;

        if (!GameAPI.Databases.EntityLocationAPI.TryGetEntityFromLocation(location, out var modelId))
            return false;

        if (!DatabaseAPI.TryGetModelUntypedReadOnly(modelId, out var model))
            return false;

        if (!TryCreatePreview(model, actorId, out preview))
            return false;

        return true;
    }

    private bool TryCreatePreviewFromDirection(
        CharacterId actorId, 
        Vec2 direction, 
        out IPreviewPresentation preview)
    {
        preview = null;
        return false;
    }

    // Maybe move to a factory or something
    internal bool TryCreatePreview(
        object candidate,
        CharacterId viewerCharacterUid,
        out IPreviewPresentation preview)
    {
        preview = null;

        // Later maybe we want to do a specific template preview
        if (candidate is Template template)
            candidate = template.EntityBlueprintRoot;

        if (candidate is IGameDbId id)
        {
            if (DatabaseAPI.TryGetModelUntypedReadOnly(id, out var model))
            {
                candidate = model;
            }
        }

        switch (candidate)
        {
            case Character character:
                preview = new CharacterPreviewPresentation(GameAPI, character.Id);
                return true;
            case Item item:
                preview = new ItemPreviewPresentation(GameAPI, viewerCharacterUid, item.Id);
                return true;
            case Prop prop:
                preview = new PropPreviewPresentation(GameAPI, prop);
                return true;
        }

        return false;
    }

    internal CommandResult UseItem(ItemsAPI.Commands.UseItemOnLocation cmd)
    {
        return Fail($"UseItemOnLocation is not implemented for item {cmd.ItemId}.");
    }
}


// =============================================================
// INTERACTION CONTEXT
// =============================================================

public readonly struct InteractionContext
{
    public CharacterId ActorId { get; }
    public IGameDbId ActingEntityId { get; }
    public IInteractionTarget Target { get; }

    public InteractionContext(
        CharacterId characterUid,
        IGameDbId actingEntityId,
        IInteractionTarget target)
    {
        ActorId = characterUid;
        ActingEntityId = actingEntityId;
        Target = target;
    }
}

public interface IInteractionTarget { }

public readonly struct EntityTarget : IInteractionTarget
{
    public IGameDbId Id { get; }
    public EntityTarget(IGameDbId id)
    {
        Id = id;
    }
}

public readonly struct LocationTarget : IInteractionTarget
{
    public IGameModelLocation Location { get; }
    public LocationTarget(IGameModelLocation location)
    {
        Location = location;
    }
}

public readonly struct DirectionTarget : IInteractionTarget
{
    public Vec2 Direction { get; }
    public DirectionTarget(Vec2 direction)
    {
        Direction = direction.normalized;
    }
}

public readonly struct BulkLocationTarget : IInteractionTarget
{
    public MapChunkId MapId { get; }
    public IReadOnlyList<CellPosition> Cells { get; }
    public CellSize PlacementSize { get; }
    public string ShapeId { get; }
    public CellPosition? AnchorStart { get; }
    public CellPosition? AnchorEnd { get; }

    public BulkLocationTarget(
        MapChunkId mapId,
        IReadOnlyList<CellPosition> cells,
        CellSize placementSize,
        string shapeId,
        CellPosition? anchorStart,
        CellPosition? anchorEnd)
    {
        MapId = mapId;
        Cells = cells ?? Array.Empty<CellPosition>();
        PlacementSize = placementSize;
        ShapeId = shapeId ?? string.Empty;
        AnchorStart = anchorStart;
        AnchorEnd = anchorEnd;
    }
}
