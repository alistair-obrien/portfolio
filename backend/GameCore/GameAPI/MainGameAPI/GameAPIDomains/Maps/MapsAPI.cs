using System;

public sealed partial class MapsAPI : APIDomain
{
    public DatabaseAPI DatabasesAPI => GameAPI.Databases;
    public TemplatesAPI TemplatesAPI => GameAPI.Templates;

    public MapsAPI(GameInstance gameAPI) : base(gameAPI) 
    {

    }

    internal override void RegisterHandlers(CommandRouter router)
    {
        // MAPS
        router.Register<Commands.CreateMap>(TryCreateMap);
        //router.Register<Commands.LoadMap>(TryLoadMap);
        router.Register<Commands.DestroyMap>(TryDestroyMap);
        router.Register<Commands.ResizeMap>(TryResizeMap);
        router.Register<Commands.ClearMap>(TryClearMap);

        // GENERIC
        router.Register<Commands.AddEntityToMap>(TryAddEntityToMap);

        // CHARACTERS
        router.Register<Commands.AddCharacterToMap>(TryAddCharacterToMap);
        router.Register<Commands.MoveCharacterAlongPathToCell>(TryMoveCharacterAlongPathToCell);
        router.Register<Commands.MoveCharacterOneStepRelative>(TryMoveCharacterOneStepRelative);
        router.Register<Commands.MoveCharacterDirectlyToCell>(TryMoveCharacterDirectlyToCell);
        router.Register<Commands.RemoveCharacterFromMap>(TryRemoveCharacterFromMap);

        // PROPS
        router.Register<Commands.AddPropToMap>(TryAddPropToMap);
        router.Register<Commands.MovePropAlongPathToCell>(TryMovePropAlongPathToCell);
        router.Register<Commands.MovePropOneStepRelative>(TryMovePropOneStepRelative);
        router.Register<Commands.MovePropDirectlyToCell>(TryMovePropDirectlyToCell);
        router.Register<Commands.RemovePropFromMap>(TryRemovePropFromMap);

        // ITEMS
        router.Register<Commands.AddItemToMap>(TryAddItemToMap);
        router.Register<Commands.MoveItemAlongPathToCell>(TryMoveItemAlongPathToCell);
        router.Register<Commands.MoveItemOneStepRelative>(TryMoveItemOneStepRelative);
        router.Register<Commands.MoveItemDirectlyToCell>(TryMoveItemDirectlyToCell);
        router.Register<Commands.RemoveItemFromMap>(TryRemoveItemFromMap);

        router.Register<Commands.RemovePlacementFromMap>(TryRemoveEntityFromMap);

        //// TILES
        //router.Register<Commands.SetTileType>(TrySetTileType);
        //router.Register<Commands.SetTileBlocked>(TrySetTileBlocked);
        //router.Register<Commands.SetTileProperty>(TrySetTileProperty);
    }

    // =========================================================
    // Map Lifecycle
    // =========================================================
    // Could be generic
    internal CommandResult TryCreateMap(Commands.CreateMap cmd)
    {
        MapChunkBlueprint newMap = (MapChunkBlueprint)DatabaseAPI.BuildNewBlueprint(typeof(MapChunk));
        newMap.Id = cmd.MapId;

        var createResult = DatabasesAPI.TryCreateOrUpdateModel(new DatabaseAPI.Commands.CreateOrUpdateModel(newMap));
        if (!createResult.Ok)
            return Fail($"Failed to create map {cmd.MapId}: {createResult.ErrorMessage}");

        RaiseEvent(
            new Events.MapCreated(
                newMap.Id,
                new MapPresentation(GameAPI, newMap.Id)));

        return Ok();
    }

    //internal bool TryLoadMap(Commands.LoadMap cmd)
    //{
    //    if (!TryResolve(cmd.MapId, out MapChunk map))
    //        return false;

    //    if (!RootModel.TryLoadMap(cmd.MapId))
    //        return false;

    //    var presentation = new MapPresentation(GameAPI, cmd.MapId);
    //    RaiseEvent(
    //        new Events.MapLoaded(
    //            map.Id,
    //            presentation));

    //    return true;
    //}

    // If any persistent characters are on the map make sure to move them first before calling this!!
    internal CommandResult TryDestroyMap(Commands.DestroyMap cmd)
    {
        if (!TryResolve(cmd.MapId, out MapChunk map))
            return Fail($"Could not resolve map {cmd.MapId} for destruction.");

        //RootModel.LoadedWorld.TryUnloadMap(cmd.MapId); // If its loaded

        if (!DatabasesAPI.TryRemoveModel(cmd.MapId))
            return Fail($"Failed to remove map {cmd.MapId} from the database.");

        // Destroy all entities that are placed on the map
        var ids = ((IHasGameDbResolvableReferences)map).GetChildIdReferences();
        foreach (var id in ids)
        {
            DatabasesAPI.TryRemoveModel(id);
        }

        RaiseEvent(new Events.MapDestroyed(cmd.MapId));

        return Ok();
    }

    internal CommandResult TryResizeMap(Commands.ResizeMap cmd)
    {
        if (!TryResolve(cmd.MapId, out MapChunk map))
            return Fail($"Could not resolve map {cmd.MapId} for resize.");

        var outOfBoundsPlacements = map.GetPlacementsOutOfBoundsForSize(new CellSize(cmd.NewWidth, cmd.NewHeight));
        foreach (var placement in outOfBoundsPlacements)
        {
            if (!DatabasesAPI.EntityMoverAPI.TryMoveEntity(
                new ItemsAPI.Commands.MoveEntity(
                    Root.SYSTEM_ID,
                    placement.Id,
                    new RootLocation(placement.Id),
                    false)).Ok)
            {
                return Fail($"Failed to move out-of-bounds placement {placement.Id} to the root while resizing map {cmd.MapId}.");
            }
        }

        if (!map.TryResize(cmd.NewWidth, cmd.NewHeight))
            return Fail($"Map {cmd.MapId} rejected resize to {cmd.NewWidth}x{cmd.NewHeight}.");

        RaiseEvent(
            new Events.MapResized(
                cmd.MapId,
                cmd.NewWidth,
                cmd.NewHeight));

        return Ok();
    }

    internal CommandResult TryClearMap(Commands.ClearMap cmd)
    {
        if (!TryResolve(cmd.MapId, out MapChunk map))
            return Fail($"Could not resolve map {cmd.MapId} to clear it.");

        map.Clear();

        RaiseEvent(
            new Events.MapCleared(
                cmd.MapId));

        return Ok();
    }

    // =========================================================
    // Character Movement
    // =========================================================

    private bool CanActorUseMapMovement(CharacterId actorId)
    {
        return GameAPI.OperatingSystems.HasGrant(
            actorId,
            OperatingSystemGrantIds.Interactions.Move,
            OperatingSystemAccessLevel.Use);
    }

    internal CommandResult TryMoveCharacterAlongPathToCell(Commands.MoveCharacterAlongPathToCell cmd)
    {
        if (!CanActorUseMapMovement(cmd.ActorId))
            return Fail($"Actor {cmd.ActorId} does not have permission to move on the map.");

        if (!TryGetMovementPath(
                cmd.CharacterId,
                cmd.MapId,
                new CellPosition(cmd.ToX, cmd.ToY),
                out var path))
            return Fail($"Could not find a path for character {cmd.CharacterId} to ({cmd.ToX}, {cmd.ToY}) on map {cmd.MapId}.");

        foreach (var step in path.Steps)
        {
            var moveResult = TryMoveCharacterDirectlyToCell(
                    new Commands.MoveCharacterDirectlyToCell(
                        cmd.ActorId,
                        cmd.MapId,
                        cmd.CharacterId,
                        step.x,
                        step.y));
            if (!moveResult.Ok)
                return moveResult; // atomic path
        }

        return Ok();
    }

    internal CommandResult TryMoveCharacterOneStepRelative(Commands.MoveCharacterOneStepRelative cmd)
    {
        if (!CanActorUseMapMovement(cmd.ActorId))
            return Fail($"Actor {cmd.ActorId} does not have permission to move on the map.");

        if (!TryResolve(cmd.CharacterId, out Character character))
            return Fail($"Could not resolve character {cmd.CharacterId}.");

        if (character.AttachedLocation is not MapLocation mapLocation)
            return Fail($"Character {cmd.CharacterId} is not currently on a map.");

        if (!TryResolve(mapLocation.MapId, out MapChunk map))
            return Fail($"Could not resolve map {mapLocation.MapId} for character {cmd.CharacterId}.");

        if (!map.TryGetCharacterPlacement(cmd.CharacterId, out var placement))
            return Fail($"Could not find placement for character {cmd.CharacterId} on map {map.Id}.");

        var newX = placement.Footprint.X + cmd.DirectionX;
        var newY = placement.Footprint.Y + cmd.DirectionY;

        return TryMoveCharacterDirectlyToCell(
            new Commands.MoveCharacterDirectlyToCell(
                cmd.ActorId,
                map.Id,
                cmd.CharacterId,
                newX,
                newY));
    }

    internal CommandResult TryMoveCharacterDirectlyToCell(Commands.MoveCharacterDirectlyToCell cmd)
    {
        return TryMoveEntityDirectlyToCell<CharacterId, Character, MapCharacterPlacementPresentation>(
            cmd.ActorId,
            cmd.CharacterId,
            cmd.MapId,
            new CellPosition(cmd.ToX, cmd.ToY),
            createPresentation: (mapId, entityId) =>
            {
                return CreateCharacterPlacementPresentation(mapId, entityId);
            },
            raiseSemanticEvent: (mapId, entityId, presentation, moveResult) =>
            {
                RaiseEvent(
                new Events.CharacterMovedOnMap(
                    mapId,
                    entityId,
                    presentation,
                    moveResult));
            });
    }

    // =========================================================
    // Prop Movement
    // =========================================================

    internal CommandResult TryMovePropAlongPathToCell(Commands.MovePropAlongPathToCell cmd)
    {
        if (!CanActorUseMapMovement(cmd.ActorId))
            return Fail($"Actor {cmd.ActorId} does not have permission to move on the map.");

        if (!TryGetPropMovementPath(
                cmd.PropId,
                cmd.MapId,
                new CellPosition(cmd.ToX, cmd.ToY),
                out var path))
            return Fail($"Could not find a path for prop {cmd.PropId} to ({cmd.ToX}, {cmd.ToY}) on map {cmd.MapId}.");

        foreach (var step in path.Steps)
        {
            var moveResult = TryMovePropDirectlyToCell(
                    new Commands.MovePropDirectlyToCell(
                        cmd.ActorId,
                        cmd.MapId,
                        cmd.PropId,
                        step.x,
                        step.y));
            if (!moveResult.Ok)
                return moveResult; // atomic path
        }

        return Ok();
    }

    internal CommandResult TryMovePropOneStepRelative(Commands.MovePropOneStepRelative cmd)
    {
        if (!CanActorUseMapMovement(cmd.ActorId))
            return Fail($"Actor {cmd.ActorId} does not have permission to move on the map.");

        if (!TryResolve(cmd.PropId, out Prop prop))
            return Fail($"Could not resolve prop {cmd.PropId}.");

        if (prop.AttachedLocation is not MapLocation mapLocation)
            return Fail($"Prop {cmd.PropId} is not currently on a map.");

        if (!TryResolve(mapLocation.MapId, out MapChunk map))
            return Fail($"Could not resolve map {mapLocation.MapId} for prop {cmd.PropId}.");

        if (!map.TryGetPlacement(cmd.PropId, out var placement))
            return Fail($"Could not find placement for prop {cmd.PropId} on map {map.Id}.");

        var newX = placement.Footprint.X + cmd.DirectionX;
        var newY = placement.Footprint.Y + cmd.DirectionY;

        return TryMovePropDirectlyToCell(
            new Commands.MovePropDirectlyToCell(
                cmd.ActorId,
                map.Id,
                cmd.PropId,
                newX,
                newY));
    }

    internal CommandResult TryMovePropDirectlyToCell(Commands.MovePropDirectlyToCell cmd)
    {
        return TryMoveEntityDirectlyToCell<PropId, Prop, MapPropPlacementPresentation>(
            cmd.ActorId,
            cmd.PropId,
            cmd.MapId,
            new CellPosition(cmd.ToX, cmd.ToY),
            createPresentation: (mapId, entityId) =>
            {
                return CreatePropPlacementPresentation(mapId, entityId);
            },
            raiseSemanticEvent: (mapId, entityId, presentation, moveResult) =>
            {
                RaiseEvent(
                    new Events.PropMovedOnMap(
                        mapId,
                        entityId,
                        presentation,
                    moveResult));
            });
    }

    // =========================================================
    // Item Movement
    // =========================================================

    internal CommandResult TryMoveItemAlongPathToCell(Commands.MoveItemAlongPathToCell cmd)
    {
        if (!CanActorUseMapMovement(cmd.ActorId))
            return Fail($"Actor {cmd.ActorId} does not have permission to move on the map.");

        if (!TryGetItemMovementPath(
                cmd.ItemId,
                cmd.MapId,
                new CellPosition(cmd.ToX, cmd.ToY),
                out var path))
            return Fail($"Could not find a path for item {cmd.ItemId} to ({cmd.ToX}, {cmd.ToY}) on map {cmd.MapId}.");

        foreach (var step in path.Steps)
        {
            var moveResult = TryMoveItemDirectlyToCell(
                    new Commands.MoveItemDirectlyToCell(
                        cmd.ActorId,
                        cmd.MapId,
                        cmd.ItemId,
                        step.x,
                        step.y));
            if (!moveResult.Ok)
                return moveResult; // atomic path
        }

        return Ok();
    }

    internal CommandResult TryMoveItemOneStepRelative(Commands.MoveItemOneStepRelative cmd)
    {
        if (!CanActorUseMapMovement(cmd.ActorId))
            return Fail($"Actor {cmd.ActorId} does not have permission to move on the map.");

        if (!TryResolve(cmd.ItemId, out Item item))
            return Fail($"Could not resolve item {cmd.ItemId}.");

        if (item.AttachedLocation is not MapLocation mapLocation)
            return Fail($"Item {cmd.ItemId} is not currently on a map.");

        if (!TryResolve(mapLocation.MapId, out MapChunk map))
            return Fail($"Could not resolve map {mapLocation.MapId} for item {cmd.ItemId}.");

        if (!map.TryGetPlacement(item.Id, out var placement))
            return Fail($"Could not find placement for item {cmd.ItemId} on map {map.Id}.");

        var newX = placement.Footprint.X + cmd.DirectionX;
        var newY = placement.Footprint.Y + cmd.DirectionY;

        return TryMoveItemDirectlyToCell(
            new Commands.MoveItemDirectlyToCell(
                cmd.ActorId,
                map.Id,
                cmd.ItemId,
                newX,
                newY));
    }

    internal CommandResult TryMoveItemDirectlyToCell(Commands.MoveItemDirectlyToCell cmd)
    {
        return TryMoveEntityDirectlyToCell<ItemId, Item, MapItemPlacementPresentation>(
            cmd.ActorId,
            cmd.ItemId,
            cmd.MapId,
            new CellPosition(cmd.ToX, cmd.ToY),
            createPresentation: (mapId, entityId) =>
            {
                return CreateItemPlacementPresentation(mapId, entityId);
            },
            raiseSemanticEvent: (mapId, entityId, presentation, moveResult) =>
            {
                RaiseEvent(
                    new Events.ItemMovedOnMap(
                        mapId,
                        entityId,
                        presentation,
                    moveResult));
            });
    }

    internal CommandResult TryAddEntityToMap(Commands.AddEntityToMap cmd)
    {
        if (!TryResolveUntyped(cmd.EntityId, out var entity))
            return Fail($"Could not resolve entity {cmd.EntityId} for placement on map {cmd.MapId}.");

        if (entity is not IMapPlaceable mapPlaceable)
            return Fail($"Entity {cmd.EntityId} cannot be placed on a map.");

        MapLocation mapLocation = new MapLocation(cmd.MapId, new CellFootprint(cmd.CellPosition, mapPlaceable.SizeOnMap));

        // Events are raised inside the Move Entity
        var result = GameAPI.Databases.EntityMoverAPI.TryMoveEntity(new ItemsAPI.Commands.MoveEntity(
            cmd.ActorId,
            cmd.EntityId,
            mapLocation,
            false));

        if (!result.Ok)
            return Fail($"Failed to place entity {cmd.EntityId} at {mapLocation}: {result.ErrorMessage}");

        return Ok();
    }

    // =========================================================
    // Add / Remove Characters
    // =========================================================
    internal CommandResult TryAddCharacterToMap(Commands.AddCharacterToMap cmd)
    {
        if (!TryResolve(cmd.CharacterId, out Character character))
            return Fail($"Could not resolve character {cmd.CharacterId}.");

        MapLocation mapLocation = new MapLocation(cmd.MapId, new CellFootprint(new CellPosition(cmd.TileX, cmd.TileY), character.SizeOnMap));

        if (!GameAPI.Databases.EntityMoverAPI.TryMoveEntity(new ItemsAPI.Commands.MoveEntity(
            cmd.ActorId,
            cmd.CharacterId,
            mapLocation,
            false)).Ok)
        {
            return Fail($"Failed to add character {cmd.CharacterId} to map {cmd.MapId} at ({cmd.TileX}, {cmd.TileY}).");
        }

        return Ok();
    }

    internal CommandResult TryRemoveCharacterFromMap(Commands.RemoveCharacterFromMap cmd)
    {
        if (!GameAPI.Databases.EntityMoverAPI.TryMoveEntity(new ItemsAPI.Commands.MoveEntity(
            cmd.ActorId,
            cmd.CharacterId,
            new RootLocation(cmd.CharacterId),
            false)).Ok)
        {
            return Fail($"Failed to remove character {cmd.CharacterId} from its map placement.");
        }

        return Ok();
    }

    // =========================================================
    // Add / Remove Props
    // =========================================================
    internal CommandResult TryAddPropToMap(Commands.AddPropToMap cmd)
    {
        if (!TryResolve(cmd.PropId, out Prop prop))
            return Fail($"Could not resolve prop {cmd.PropId}.");

        MapLocation mapLocation = new MapLocation(cmd.MapId, new CellFootprint(new CellPosition(cmd.TileX, cmd.TileY), prop.SizeOnMap));

        if (!GameAPI.Databases.EntityMoverAPI.TryMoveEntity(new ItemsAPI.Commands.MoveEntity(
            cmd.ActorId,
            cmd.PropId,
            mapLocation,
            false)).Ok)
        {
            return Fail($"Failed to add prop {cmd.PropId} to map {cmd.MapId} at ({cmd.TileX}, {cmd.TileY}).");
        }

        return Ok();
    }

    internal CommandResult TryRemovePropFromMap(Commands.RemovePropFromMap cmd)
    {
        if (!GameAPI.Databases.EntityMoverAPI.TryMoveEntity(new ItemsAPI.Commands.MoveEntity(
            cmd.ActorId, 
            cmd.PropId, 
            new RootLocation(cmd.PropId), 
            false)).Ok)
        {
            return Fail($"Failed to remove prop {cmd.PropId} from its map placement.");
        }

        return Ok();
    }

    // =========================================================
    // Add / Remove Items
    // =========================================================
    internal CommandResult TryAddItemToMap(Commands.AddItemToMap cmd)
    {
        if (!TryResolve(cmd.ItemId, out Item item))
            return Fail($"Could not resolve item {cmd.ItemId}.");

        MapLocation mapLocation = new MapLocation(cmd.MapId, new CellFootprint(new CellPosition(cmd.TileX, cmd.TileY), item.SizeOnMap));

        if (!GameAPI.Databases.EntityMoverAPI.TryMoveEntity(
            new ItemsAPI.Commands.MoveEntity(
                cmd.ActorId,
                cmd.ItemId,
                mapLocation,
                AllowSwap: false)).Ok)
            return Fail($"Failed to add item {cmd.ItemId} to map {cmd.MapId} at ({cmd.TileX}, {cmd.TileY}).");

        return Ok();
    }

    internal CommandResult TryRemoveItemFromMap(Commands.RemoveItemFromMap cmd)
    {
        if (!GameAPI.Databases.EntityMoverAPI.TryMoveEntity(
            new ItemsAPI.Commands.MoveEntity(
                cmd.ActorId,
                cmd.ItemId,
                new RootLocation(cmd.ItemId),
                AllowSwap: false)).Ok)
                    return Fail($"Failed to remove item {cmd.ItemId} from its map placement.");

        // Should raise event here probably

        return Ok();
    }

    // TODO: This is generic so other specific methods should funnel through here
    internal CommandResult TryRemoveEntityFromMap(Commands.RemovePlacementFromMap cmd)
    {
        if (!GameAPI.Databases.EntityMoverAPI.TryMoveEntity(
            new ItemsAPI.Commands.MoveEntity(
                cmd.ActorId,
                cmd.GameDbId,
                new RootLocation(cmd.GameDbId),
                AllowSwap: false)).Ok)
            return Fail($"Failed to remove entity {cmd.GameDbId} from its map placement.");

        return Ok();
    }

    //// =========================================================
    //// Tiles
    //// =========================================================

    //internal bool TrySetTileType(Commands.SetTileType cmd)
    //{
    //    if (!TryResolve(cmd.MapUid, out MapChunk map))
    //        return false;

    //    var context = new MapChunkMutationContext();

    //    if (!map.TrySetTileType(
    //            cmd.TileX,
    //            cmd.TileY,
    //            cmd.TileTypeUid,
    //            context))
    //        return false;

    //    Record(context);

    //    GameAPI.RaiseEvent(
    //        new Events.TileTypeChanged(
    //            cmd.MapUid,
    //            cmd.TileX,
    //            cmd.TileY,
    //            cmd.TileTypeUid));

    //    return true;
    //}

    //internal bool TrySetTileBlocked(Commands.SetTileBlocked cmd)
    //{
    //    if (!TryResolve(cmd.MapUid, out MapChunk map))
    //        return false;

    //    var context = new MapChunkMutationContext();

    //    if (!map.TrySetTileBlocked(
    //            cmd.TileX,
    //            cmd.TileY,
    //            cmd.IsBlocked,
    //            context))
    //        return false;

    //    Record(context);

    //    GameAPI.RaiseEvent(
    //        new Events.TileBlockedChanged(
    //            cmd.MapUid,
    //            cmd.TileX,
    //            cmd.TileY,
    //            cmd.IsBlocked));

    //    return true;
    //}

    //internal bool TrySetTileProperty(Commands.SetTileProperty cmd)
    //{
    //    if (!TryResolve(cmd.MapUid, out MapChunk map))
    //        return false;

    //    var context = new MapChunkMutationContext();

    //    if (!map.TrySetTileProperty(
    //            cmd.TileX,
    //            cmd.TileY,
    //            cmd.PropertyName,
    //            cmd.PropertyValue,
    //            context))
    //        return false;

    //    Record(context);

    //    GameAPI.RaiseEvent(
    //        new Events.TilePropertyChanged(
    //            cmd.MapUid,
    //            cmd.TileX,
    //            cmd.TileY,
    //            cmd.PropertyName,
    //            cmd.PropertyValue));

    //    return true;
    //}

    // =========================================================
    // Queries
    // =========================================================

    public bool TileIsValid(MapChunkId mapId, int x, int y)
    {
        return TryResolve(mapId, out MapChunk map) && map.InBounds(x, y);
    }

    public bool TryGetPlacement(
        MapChunkId mapId,
        IGameDbId gameId,
    out IMapPlacement placement)
    {
        placement = default;

        return TryResolve(mapId, out MapChunk map)
            && map.TryGetPlacement(gameId, out placement);
    }

    public bool TryGetPlacementOnMapTile(
        MapChunkId mapId,
        CellPosition cellPosition,
        out IMapPlacement placement)
    {
        placement = default;

        return TryResolve(mapId, out MapChunk map)
            && map.TryGetPlacementAt(cellPosition, out placement);
    }

    public bool TryGetCharacterPlacementOnMap(
        MapChunkId mapId,
        CharacterId characterId,
        out CharacterPlacementOnMap placement)
    {
        placement = default;

        if (!TryResolve(mapId, out MapChunk map))
            return false;

        return map.TryGetCharacterPlacement(characterId, out placement);
    }

    internal bool TryGetMovementPath(
        CharacterId characterId,
        MapChunkId mapId,
        CellPosition toCellPosition,
        out PathResult path)
    {
        path = null;

        if (!TryResolve(characterId, out Character character))
            return false;

        if (!TryResolve(mapId, out MapChunk map))
            return false;

        return map.TryFindPathForCharacter(
            characterId,
            toCellPosition,
            out path);
    }

    internal bool TryGetPropMovementPath(
        PropId propId,
        MapChunkId mapId,
        CellPosition toCellPosition,
        out PathResult path)
    {
        path = null;

        if (!TryResolve(mapId, out MapChunk map))
            return false;

        if (!map.TryGetPropPlacement(propId, out var placementOnMap))
            return false;

        return map.TryFindPathForProp(
            placementOnMap.PropId,
            toCellPosition,
            out path);
    }

    internal bool TryGetItemMovementPath(
        ItemId itemId,
        MapChunkId mapId,
        CellPosition toCellPosition,
        out PathResult path)
    {
        path = null;

        if (!TryResolve(mapId, out MapChunk map))
            return false;

        return map.TryFindPathForItem(
            itemId,
            toCellPosition,
            out path);
    }

    internal bool TryGetPropPreview(PropId propId, out IPreviewPresentation presentation)
    {
        presentation = default;

        if (!TryResolve(propId, out Prop prop))
            return false;

        presentation = new PropPreviewPresentation(GameAPI, prop);
        return true;
    }

    internal MapPropPlacementPresentation CreatePropPlacementPresentation(
        MapChunkId mapId,
        PropId propId)
    {
        if (!TryResolve(mapId, out MapChunk mapChunk))
            return null;

        if (!mapChunk.TryGetPropPlacement(propId, out var propPlacement))
            return null;

        RenderKey GetNeighbor(CellPosition direction)
        {
            if (!mapChunk.TryGetPropAt(propPlacement.Footprint.Position + direction, out var neighborPlacement))
                return null;

            if (!TryResolve(neighborPlacement, out Prop prop))
                return null;

            return prop.RenderKey;
        }

        return new MapPropPlacementPresentation(
            GameAPI,
            propPlacement,
            north: GetNeighbor(CellPosition.North),
            south: GetNeighbor(CellPosition.South),
            east: GetNeighbor(CellPosition.East),
            west: GetNeighbor(CellPosition.West)
        );
    }

    internal MapItemPlacementPresentation CreateItemPlacementPresentation(ItemPlacementOnMap itemPlacementOnMap)
    {
        return new MapItemPlacementPresentation(GameAPI, itemPlacementOnMap);
    }

    internal MapItemPlacementPresentation CreateItemPlacementPresentation(MapChunkId mapId, ItemId itemId)
    {
        if (!TryResolve(mapId, out MapChunk mapChunk))
            return null;

        if (!mapChunk.TryGetItemPlacement(itemId, out var itemPlacement))
            return null;

        return new MapItemPlacementPresentation(GameAPI, itemPlacement);
    }

    internal MapCharacterPlacementPresentation CreateCharacterPlacementPresentation(MapChunkId mapId, CharacterId characterId)
    {
        if (!TryResolve(mapId, out MapChunk mapChunk))
            return null;

        if (!mapChunk.TryGetCharacterPlacement(characterId, out var characterPlacement))
            return null;

        return new MapCharacterPlacementPresentation(GameAPI, characterPlacement);
    }

    internal bool CanAdd(MapChunkId mapId, IDbId stagedId, CellFootprint footprint)
    {
        if (!TryResolve(mapId, out MapChunk mapChunk))
            return false;

        return mapChunk.CanAdd(stagedId, footprint);
    }

    internal bool TryRemovePlacementFromMapTile(
        MapChunkId mapId,
        CellPosition cellPosition,
        out IMapPlacement placement)
    {
        placement = default;

        if (!TryResolve(mapId, out MapChunk map))
            return false;

        if (!map.TryGetPlacementAt(cellPosition, out placement))
            return false;

        TryRemovePlacement(mapId, placement.Id);

        return true;
    }

    internal bool TryRemovePlacement(
        MapChunkId mapId,
        IGameDbId entityId)
    {
        if (!TryResolve(mapId, out MapChunk map))
            return false;

        // The map doesnt have this placement so lets not try remove it
        if (!map.TryGetPlacement(entityId, out var _))
            return false;

        return GameAPI.Databases.EntityMoverAPI.TryMoveEntity(
            new ItemsAPI.Commands.MoveEntity(
                Root.SYSTEM_ID,
                entityId,
                new RootLocation(entityId),
                false)).Ok;
    }

    internal bool TryAddPlacementAtMapTile(
        IGameDbId entityId,
        MapChunkId mapId,
        CellPosition cellPosition)
    {
        if (!TryResolveUntyped(entityId, out var entity))
            return false;

        if (entity is not IMapPlaceable mapPlaceable)
            return false;

        var mapLocation = new MapLocation(mapId, new CellFootprint(cellPosition, mapPlaceable.SizeOnMap));

        return GameAPI.Databases.EntityMoverAPI.TryMoveEntity(
            new ItemsAPI.Commands.MoveEntity(
                Root.SYSTEM_ID, 
                entityId,
                mapLocation, 
                false)).Ok;
    }

    CommandResult TryMoveEntityDirectlyToCell<TId, TEntity, TPresentation>(
        CharacterId actorId,
        TId entityId,
        MapChunkId mapId,
        CellPosition toCellPosition,
        Func<MapChunkId, TId, TPresentation> createPresentation,
        Action<MapChunkId, TId, TPresentation, MoveResult> raiseSemanticEvent)
        where TId : struct, IGameDbId
        where TEntity : IGameDbResolvable, IMapPlaceable
    {
        if (!TryResolve(mapId, out MapChunk map))
            return Fail($"Could not resolve map {mapId}.");

        if (!TryResolve(entityId, out TEntity entity))
            return Fail($"Could not resolve entity {entityId}.");

        if (!DatabasesAPI.EntityLocationAPI.TryFindEntityLocation(entityId, out var originalLocation))
            return Fail($"Could not find the current location for entity {entityId}.");

        if (originalLocation is not MapLocation originalMapLocation)
            return Fail($"Entity {entityId} is not currently placed on a map.");

        if (originalMapLocation.MapId.Equals(mapId) &&
            originalMapLocation.CellFootprint.X == toCellPosition.X &&
            originalMapLocation.CellFootprint.Y == toCellPosition.Y)
            return Ok();

        var newLocation = new MapLocation(
            mapId,
            new CellFootprint(toCellPosition, entity.SizeOnMap));

        if (!DatabasesAPI.EntityMoverAPI.TryMoveEntity(
                new ItemsAPI.Commands.MoveEntity(
                    actorId,
                    entityId,
                    newLocation,
                    false)).Ok)
            return Fail($"Failed to move entity {entityId} to {newLocation}.");

        if (!DatabasesAPI.EntityLocationAPI.TryFindEntityLocation(entityId, out var resolvedLocation))
            return Fail($"Entity {entityId} moved, but its resolved location could not be found afterward.");

        if (resolvedLocation is not MapLocation resolvedMapLocation)
            return Fail($"Entity {entityId} moved, but did not end up on a map location.");

        MoveResult result = new MoveResult(
            true,
            originalMapLocation.MapId,
            originalMapLocation.CellFootprint,
            resolvedMapLocation.MapId,
            resolvedMapLocation.CellFootprint);

        var presentation = createPresentation(mapId, entityId);

        if (presentation != null)
            raiseSemanticEvent(map.Id, entityId, presentation, result);

        return Ok();
    }

    internal void RaisePropUpdatedForNeighborhood(MapChunk map, CellPosition centerCell)
    {
        // The changed prop + its 4 cardinal neighbors
        Span<CellPosition> cellsToRefresh = stackalloc CellPosition[]
        {
            centerCell,
            centerCell + CellPosition.North,
            centerCell + CellPosition.South,
            centerCell + CellPosition.East,
            centerCell + CellPosition.West,
        };

        foreach (var cell in cellsToRefresh)
        {
            if (!map.TryGetPlacementAt(cell, out var placement) || placement is not PropPlacementOnMap propPlacement)
                continue;

            RaiseEvent(new Events.PropUpdatedOnMap(
                map.Id,
                propPlacement.PropId,
                CreatePropPlacementPresentation(map.Id, propPlacement.PropId)));
        }
    }
}
