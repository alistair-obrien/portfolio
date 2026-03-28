using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class Intents
{
    public interface IIntent { }
    public interface IMainGameIntent : IIntent { }

    // Editor Intents
    public class Editor
    {
        internal record DeleteEntity(IGameDbId EntityId) : IIntent;
        public record PreviewTemplateActionOverOther(ITemplateDbId TemplateDbId) : IIntent;
        public record CommitTemplateActionOverOther(ITemplateDbId TemplateDbId) : IIntent;

        public record SaveModelToTemplate(IGameDbId GameDbId, ITemplateDbId TemplateDbId) : IIntent;

        public record MoveEntity(IGameDbId GameDbId, IGameModelLocation Locaiton) : IIntent;
        public record CreateOrUpdateModel(IBlueprint Blueprint) : IIntent;
        public record CreateOrUpdateTemplate(string TemplateId, IBlueprint Blueprint) : IIntent;

        public record OpenEntityEditor(IGameDbId EntityId) : IIntent;
        public record CloseEntityEditor(string EditorId) : IIntent;

        public record OpenTemplateEditor(ITemplateDbId TemplateId) : IIntent;
        public record CloseItemTemplateEditor(string EditorId) : IIntent;

        public record OpenTemplateInMapEditor(MapChunkTemplateId MapTemplateId) : IIntent;
        public record OpenInstanceInMapEditor(MapChunkId MapInstanceId) : IIntent;
        public record OpenEntity(IGameDbId EntityId) : IIntent;
        public record FrameEntity(IGameDbId EntityId) : IIntent;
        public record OpenTemplate(ITemplateDbId TemplateId) : IIntent;
        public record FrameTemplate(ITemplateDbId TemplateId) : IIntent;
        public record OpenInteractionContextMenu(InteractionContext InteractionContext) : IIntent;

        public record CreateNewEntity(Type Type) : IIntent;
        public record TemplateHoverEnter(ITemplateDbId TemplateId) : IIntent;
        public record TemplateHoverExit(ITemplateDbId TemplateId) : IIntent;
        public record TemplatePrimaryCommit(ITemplateDbId TemplateId) : IIntent;
        public record TemplateSecondaryCommit(ITemplateDbId TemplateId) : IIntent;

        public record CreateAndAttachNewEntity(IGameDbId OwnerId, ISlotId SlotId, Type Type) : IIntent;
        public record OpenEntityPicker(IGameDbId OwnerId, ISlotId SlotId, Type Type) : IIntent;
        public record DetachEntity(IGameDbId OwnerId, ISlotId SlotId) : IIntent;
    }

    // Game Intents

    public record GameEntityLocationHoverEnterIntent(IGameModelLocation TargetEntityLocation) : IIntent;
    public record GameEntityLocationHoverExitIntent(IGameModelLocation TargetEntityLocation) : IIntent;
    public record GameEntityLocationPrimaryCommit(IGameModelLocation TargetEntityLocation) : IIntent;
    public record GameEntityLocationSecondaryCommit(IGameModelLocation TargetEntityLocation) : IIntent;
    
    public record DirectionUpdatedIntent(Vec2 Direction) : IIntent;
    public record DirectionPrimaryCommitWorldIntent(Vec2 Direction) : IIntent;
    public record DirectionSecondaryCommitWorldIntent(Vec2 Direction) : IIntent;

    public record GameEntityHoverEnter(IGameDbId EntityId) : IIntent;
    public record GameEntityHoverExit(IGameDbId EntityId) : IIntent;
    public record GameEntityPrimaryCommit(IGameDbId EntityId) : IIntent;
    public record GameEntitySecondaryCommit(IGameDbId EntityId, IGameDbId ActingEntityId = null) : IIntent;
    public record GameEntitySecondaryCommitFromTemplate(IGameDbId EntityId, ITemplateDbId TemplateId) : IIntent;

    public sealed record ToggleAttackTargetingIntent() : IMainGameIntent; // Maybe pass acting item in here and make cell targeting versions too
    public sealed record AttackTargetingCancelIntent() : IMainGameIntent;
    
    public sealed record EndTurnIntent() : IMainGameIntent;
    public sealed record StartNewGameIntent() : IMainGameIntent;

    public sealed record ContextMenuOptionSelectedIntent(
        InteractionContext InteractionContext,
        string OptionKey
    ) : IMainGameIntent;
}

enum TurnState
{
    Suspended,
    Polling,
    WaitingOnPlayTurnToFinish
}

public interface IMainGameQueries : ICoordinatorQueries
{
    bool IsCharactersTurn(CharacterId characterUid);
    bool TryGetPropPreview(PropId propLocalUid, out IPreviewPresentation presentation);
    bool TryGetItemPreview(ItemId itemUid, out IPreviewPresentation presentation);
    bool TryPreviewCommand(
        IGameCommand command, 
        out List<IGameEvent> previewEvents);

    bool CanPreviewPrimaryWeaponAttack();
    bool TryGetPlayerCharacterLocation(out ICharacterLocation characterLocation);
    void TryGetItemPresentation(ItemId itemId, out ItemPresentation presentation);
    CharacterId GetPlayerCharacterId();
}

public interface IMainGameCommands : ICoordinatorCommands
{
    void BindPlayerInteractionRenderer(IPlayerInteractionRenderer playerHUDMenuViewInstance);
    void UnbindPlayerInteractionRenderer(IPlayerInteractionRenderer playerHUDMenuViewInstance);
}

public interface IMainGameRenderer : IRenderer<RootGameModelPresentation>
{

    // OLD probably
    void RenderMapPlacementHover(MapChunkId mapUid, IGameDbId entityId);
    void RenderMapPlacementHoverClear(MapChunkId mapUid, IGameDbId entityId);

    // EVEN NEWER! :D
    void ShowContextMenu(InteractionContext interactionContext, InteractionOptionsPresentation presentation);

    // Map Target Rendering
    void RenderMapCharacterTargeted(MapCharacterPlacementPresentation mapCharacterPresentation);
    void RenderMapItemTargeted(MapItemPlacementPresentation mapItemPresentation);
    void RenderMapTileTargeted(MapTilePresentation mapTilePresentation);
    void RenderMapCharacterTargetCleared(MapCharacterPlacementPresentation mapCharacterPresentation);
    void RenderMapItemTargetCleared(MapItemPlacementPresentation mapItemPresentation);
    void RenderMapTileTargetCleared(MapTilePresentation mapTilePresentation);

    // Inventory Target Rendering
    void RenderInventoryItemTargeted(ItemPlacementPresentation presentation);
    void RenderInventoryTargetCleared(ItemPlacementPresentation presentation);

    // Hover
    void RenderHoverPreview(IPreviewPresentation presentation);
    void RenderHoverPreviewCleared();
    void RenderEquippedItemTargeted(ItemSlotPresentation presentation);
    void RenderEquippedItemTargetCleared(ItemSlotPresentation presentation);
    void BindToWorldCoordinator(WorldCoordinator coordinator);
    void BindToInteractionCoordinator(PlayerInteractionCoordinator coordinator);
}

public class MainGameCoordinator : CoordinatorBase<
    IMainGameQueries, 
    IMainGameCommands, 
    IMainGameRenderer>, 
    IMainGameQueries, IMainGameCommands
{
    public override IMainGameQueries QueriesHandler => this;
    public override IMainGameCommands CommandsHandler => this;

    // Sub Coordinators
    public readonly WorldCoordinator WorldCoordinator;
    public readonly PlayerInteractionCoordinator PlayerInteractionCoordinator;
    private GenoSysCoordinator _genoSysCoordinator;

    private Stack<ITargetingSession> _targetingSessions = new Stack<ITargetingSession>();

    // State
    private TurnState _turnState = TurnState.Suspended;
    public bool AnimateEvents = true;
    private InteractionContext? _hoveredInteractionContext;
    private ITargetingSession _activeAttackTargetingSession;

    public MainGameCoordinator(GameInstance gameAPI) : base(gameAPI)
    {
        AddSubCoordinator(WorldCoordinator = new WorldCoordinator(gameAPI));
        AddSubCoordinator(PlayerInteractionCoordinator = new PlayerInteractionCoordinator(gameAPI));

        if (gameAPI.Characters.TryGetPlayerCharacter(out var characterId))
        {
            if (gameAPI.Databases.TryResolve(characterId.Value, out Character character))
            {
                if (character.AttachedLocation is MapLocation mapLocation)
                {
                    WorldCoordinator.SetMap(mapLocation.MapId);
                }
            }
        }

        var listener = new TargetingListener();
        listener.onLocationHovered += HandleLocationHoverEntered;
        listener.onLocationHoverCleared += HandleLocationHoverExited;
        listener.onLocationCommitted += HandleLocationPrimaryCommit;
        var session = new LocationTargetingSession(listener);
        StartTargetingSession(session);

        SyncOperatingSystemCoordinator();
    }

    // TODO: Later we will have an actual scenario
    public async void LoadTestScenario(MapChunkTemplateId mapChunkTemplateId)
    {
        // Template Ids
        var characterTemplateId = new CharacterTemplateId("player");
        var inventoryTemplateId = new ItemTemplateId("player_inventory");
        var smgTemplateId = new ItemTemplateId("smg");
        var ammoItemTemplateId = new ItemTemplateId("ammo");
        var shotgunTemplateId = new ItemTemplateId("shotgun");

        // Instance Ids
        var characterId = new CharacterId("player");
        var inventoryId = new ItemId("player_inventory");
        var mapId = new MapChunkId("mappy");

        var commands = new List<IGameCommand>
    {
        new SaveDataAPI.Commands.StartNewGame(),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(mapChunkTemplateId, mapId),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(characterTemplateId, characterId),

        new MapsAPI.Commands.AddCharacterToMap(
            Root.SYSTEM_ID, mapId, characterId, 0, 0),

        new CharactersAPI.Commands.AssignPlayerCharacter(characterId),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(
            inventoryTemplateId, inventoryId),

        new ItemsAPI.Commands.EquipItem(
            characterId, inventoryId, SlotIds.Loadout.Inventory),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(
            smgTemplateId, new ItemId("Gun One")),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(
            ammoItemTemplateId, new ItemId("Ammo One")),

        new ItemsAPI.Commands.LoadGun(
            characterId, new ItemId("Gun One"), new ItemId("Ammo One")),

        new ItemsAPI.Commands.EquipItem(
            characterId, new ItemId("Gun One"), SlotIds.Loadout.PrimaryWeapon),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(
            smgTemplateId, new ItemId("Gun Two")),

        new ItemsAPI.Commands.TakeItem(
            characterId, new ItemId("Gun Two")),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(
            shotgunTemplateId, new ItemId("Gun Three")),

        new ItemsAPI.Commands.TakeItem(
            characterId, new ItemId("Gun Three")),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(
            new ItemTemplateId("Ammo Two"), new ItemId("Ammo Two")),

        new MapsAPI.Commands.AddItemToMap(
            characterId, mapId, new ItemId("Ammo Two"), 3, 3),

        new TemplatesAPI.Commands.SpawnModelFromTemplate(
            ammoItemTemplateId, new ItemId("Ammo Three")),

        new ItemsAPI.Commands.TakeItem(
            characterId, new ItemId("Ammo Three")),
    };

        if (!_gameInstance.TryExecuteCommands(commands, out var events).Ok)
            return;

        foreach (var evt in events)
            ApplyEventToSubCoordinators(evt);

        Debug.Log("Lets render");

        await ForEachRendererAsync(r => r.RenderEvents(events, false));

        _gameInstance.Turns.TryAddCharacterToTurnQueue(characterId);
        _turnState = TurnState.Polling;
    }

    public bool CanAcceptPlayerInput()
    {
        return _turnState == TurnState.Polling
            && IsCharactersTurn(_gameInstance.Characters.PlayerCharacterId.Value);
    }

    //TODO: Actually just change this to primary action in direction. Roguelike style.
    public void RequestMovePlayerOneStepRelative(Vec2Int desiredDirection)
    {
        _gameInstance.EnqueueAction(
            _gameInstance.Characters.PlayerCharacterId.Value, 
            new MapsAPI.Commands.MoveCharacterOneStepRelative(
                Root.SYSTEM_ID,
                _gameInstance.Characters.PlayerCharacterId.Value, 
                desiredDirection.x,
                desiredDirection.y)
            );
    }

    public void RequestStartNewGame()
    {
        _gameInstance.EnqueueAction(Root.SYSTEM_ID, new SaveDataAPI.Commands.StartNewGame());
    }

    public void RequestSaveGameToDisk(string savePath)
    {
        _gameInstance.EnqueueAction(Root.SYSTEM_ID, new SaveDataAPI.Commands.SaveGame(savePath));
    }

    public void RequestLoadGameFromDisk(string savePath)
    {
        _gameInstance.EnqueueAction(Root.SYSTEM_ID, new SaveDataAPI.Commands.LoadGame(savePath));
    }

    public async Task PlayTurn(TurnOutcome outcome)
    {
        foreach (var evt in outcome.Events)
        {
            ApplyEventToSubCoordinators(evt);
        }

        await ForEachRendererAsync(r => r.RenderEvents(outcome.Events, true));
    }

    private void ApplyEventToSubCoordinators(IGameEvent evt)
    {
        ForEachSubCoordinator(sub => sub.HandleGameEvent(evt));
    }

    protected override void OnUpdate(float deltaTime)
    {
        // FORCE REFRESH ENTIRE VIEW
        //ForEachRenderer(r => r.Sync(_gameAPI.PullRootGameModelPresentation())); // Commented for now as testing the above works

        if (_turnState != TurnState.Polling)
            return;

        _ = UpdateTurnAsync();
    }

    protected override void DoHandleGameEvent(IGameEvent evt)
    {
        SyncOperatingSystemCoordinator();

        var presentation = _gameInstance.PullRootGameModelPresentation();
        ForEachRenderer(renderer => renderer.Sync(presentation));
    }

    private async Task UpdateTurnAsync()
    {
        if (!_gameInstance.Turns.TryProcessCurrentTurn(out var turnOutcome))
            return;

        _turnState = TurnState.WaitingOnPlayTurnToFinish;
        await PlayTurn(turnOutcome);
        _turnState = TurnState.Polling;
    }

    protected override void HandleIntent(Intents.IIntent intent)
    {
        if (TryHandleGlobalIntent(intent))
            return;

        if (!_targetingSessions.TryPeek(out var activeTargetingSession))
            return;

        if (activeTargetingSession != null &&
            activeTargetingSession.HandleIntent(intent))
            return;

        HandleDefaultIntent(intent);
    }

    // INTENTS HANDLERS
    private void HandleDefaultIntent(Intents.IIntent intent)
    {
        switch (intent)
        {
            case Intents.GameEntityLocationSecondaryCommit s:
                HandleLocationSecondaryCommit(s.TargetEntityLocation);
                break;
            case Intents.GameEntityPrimaryCommit pc:
                HandleEntityPrimaryCommit(pc.EntityId);
                break;
            case Intents.GameEntitySecondaryCommit sc:
                HandleEntitySecondaryCommit(sc.EntityId);
                break;
        }
    }

    private bool TryHandleGlobalIntent(Intents.IIntent intent)
    {
        switch (intent)
        {
            case Intents.StartNewGameIntent:
                RequestStartNewGame();
                return true;

            case Intents.ToggleAttackTargetingIntent:
                HandleToggleAttackTargeting();
                return true;
        }

        return false;
    }

    private void HandleLocationHoverEntered(IGameModelLocation location)
    {
        var locationTarget = new LocationTarget(location);
        HandleLocationHoverEntered(locationTarget);
    }

    private void HandleLocationHoverEntered(LocationTarget locationTarget)
    {
        if (!_gameInstance.Databases.TryGetModel(_gameInstance.Characters.PlayerCharacterId.Value, out Character character))
            return;

        IGameDbId actingItemEntityId = null;
        if (character.TryGetItemInSlot(SlotIds.Loadout.HeldItem, out var heldItemId))
            actingItemEntityId = heldItemId;

        _hoveredInteractionContext = new InteractionContext(
            _gameInstance.Characters.PlayerCharacterId.Value,
            actingItemEntityId,
            locationTarget);

        RenderHoverTarget(_hoveredInteractionContext.Value);
        RenderHoverPreview(_hoveredInteractionContext.Value);

        PreviewPrimaryActionAtLocation(locationTarget);
    }

    private void HandleLocationHoverExited()
    {
        ClearHoverVisuals();

        if (_hoveredInteractionContext.HasValue)
        {
            RenderHoverTargetCleared(_hoveredInteractionContext.Value);
            RenderHoverPreviewCleared(_hoveredInteractionContext.Value);
        }

        ClearAllPreviews();

        _hoveredInteractionContext = null;
    }

    private void ClearAllPreviews()
    {
        Renderers.ForEach(r => r.ClearAllPreviews());
    }

    private void HandleLocationSecondaryCommit(IGameModelLocation location)
    {
        if (!_gameInstance.Databases.TryGetModel(_gameInstance.Characters.PlayerCharacterId.Value, out Character character))
            return;

        IGameDbId actingItemEntityId = null;
        if (character.TryGetItemInSlot(SlotIds.Loadout.HeldItem, out var heldItemId))
            actingItemEntityId = heldItemId;

        var interactionTarget = new LocationTarget(location);
        InteractionContext interactionContext = new InteractionContext(
            _gameInstance.Characters.PlayerCharacterId.Value,
            actingItemEntityId,
            interactionTarget
            );

        var availableActions = _gameInstance.Interactions.GetAvailableActions(interactionContext);

        if (!availableActions.Any())
            return;

        var presentation = new InteractionOptionsPresentation(availableActions);

        Renderers.ForEach((renderer) =>  renderer.ShowContextMenu(interactionContext, presentation));
    }
    private void HandleLocationPrimaryCommit(IGameModelLocation mapLocation)
    {
        TryQueuePrimaryActionOnLocation(mapLocation);
    }

    private void HandleEntityHoverEnter(IGameDbId entityId)
    {
        throw new NotImplementedException();
    }

    private void HandleEntityHoverExit(IGameDbId entityId)
    {
        throw new NotImplementedException();
    }

    private void HandleEntityPrimaryCommit(IGameDbId entityId)
    {
        throw new NotImplementedException();
    }

    private void HandleEntitySecondaryCommit(IGameDbId entityId)
    {
        throw new NotImplementedException();
    }

    // OTHER
    private void CommitPrimaryAttackDirection(Vec2 direction)
    {
        RequestQueuePrimaryInteractionInDirection(direction);
        //EndTargetingSession();
    }

    internal void PreviewPrimaryAttackDirection(Vec2 direction)
    {
        if (!CanPreviewPrimaryWeaponAttack())
            return;

        if (!_gameInstance.Characters.TryGetWeapon(_gameInstance.RootModel.PlayerCharacterId.Value, out ItemId weaponId))
            return;

        var context = new InteractionContext(
            _gameInstance.RootModel.PlayerCharacterId.Value,
            weaponId,
            new DirectionTarget(direction)
        );

        if (!_gameInstance.Interactions.TryGetPrimaryInteraction(context, out var interaction))
            return;

        if (!TryPreviewCommands(interaction.Commands, out var previewEvents))
            return;

        ClearAllPreviews();
        Renderers.ForEach(r =>
        {
            r.RenderPreviewEvents(previewEvents, animate: false);
        });
    }

    private void HandleToggleAttackTargeting()
    {
        _targetingSessions.TryPeek(out var activeTargetingSession);

        if (activeTargetingSession != null && activeTargetingSession == _activeAttackTargetingSession)
        {
            EndTargetingSession();
            return;
        }

        if (!_gameInstance.Characters.TryGetPlayerCharacter(out var player))
            return;
        
        if (!_gameInstance.Characters.TryGetWeapon(player.Value, out var weapon))
            return;

        var listener = CreatePrimaryAttackTargetingListener();
        var attackSession = new DirectionalTargetingSession(listener);

        _activeAttackTargetingSession = attackSession;

        ClearAllPreviews();
        StartTargetingSession(attackSession);
    }

    private ITargetingListener CreatePrimaryAttackTargetingListener()
    {
        var listener = new TargetingListener();

        listener.onDirectionUpdated += PreviewPrimaryAttackDirection;
        listener.onDirectionCommitted += CommitPrimaryAttackDirection;
        listener.onTargetingCanceled += EndTargetingSession;
        listener.onTargetingCanceled += () => _activeAttackTargetingSession = null;

        return listener;
    }

    private void PreviewPrimaryActionAtLocation(LocationTarget locationTarget)
    {
        if (!_gameInstance.Characters.TryGetPlayerCharacter(out var playerId))
            return;

        if (!_gameInstance.Databases.TryGetModel(_gameInstance.Characters.PlayerCharacterId.Value, out Character character))
            return;

        IGameDbId actingItemEntityId = null;
        if (character.TryGetItemInSlot(SlotIds.Loadout.HeldItem, out var heldItemId))
            actingItemEntityId = heldItemId;

        var context = new InteractionContext(
            playerId.Value,
            actingItemEntityId,
            locationTarget
        );

        if (!_gameInstance.Interactions.TryGetPrimaryInteraction(
                context,
                out var interaction))
            return;

        if (!TryPreviewCommands(interaction.Commands, out var previewEvents))
            return;

        Renderers.ForEach(r =>
        {
            r.RenderPreviewEvents(previewEvents, animate: false);
        });
    }

    public bool TryQueuePrimaryActionOnLocation(IGameModelLocation location)
    {
        if (!_gameInstance.Characters.TryGetPlayerCharacter(out var playerId))
            return false;

        if (!_gameInstance.Databases.TryGetModel(_gameInstance.Characters.PlayerCharacterId.Value, out Character character))
            return false;

        IGameDbId actingItemEntityId = null;
        if (character.TryGetItemInSlot(SlotIds.Loadout.HeldItem, out var heldItemId))
            actingItemEntityId = heldItemId;

        var context = new InteractionContext(
            playerId.Value,
            actingEntityId: actingItemEntityId,
            new LocationTarget(location)
        );

        if (!_gameInstance.Interactions.TryGetPrimaryInteraction(
                context,
                out var interaction))
            return false;

        _gameInstance.EnqueueActions(playerId.Value, interaction.Commands);

        return true;
    }

    //public void RequestPreviewDirectionalInteraction(Vector2 direction, string activeItemUid)
    //{
    //    var context = new InteractionContext(
    //        _gameAPI.GameModel.PlayerCharacter.Uid,
    //        activeItemUid,
    //        new DirectionTarget(direction)
    //    );

    //    if (!_gameAPI.Interactions.TryGetPrimaryInteraction(
    //            context,
    //            out var interaction))
    //        return;

    //    if (!TryPreviewCommand(interaction.Command, out var previewEvents))
    //        return;

    //    ForEachRenderer(r =>
    //    {
    //        foreach (var evt in previewEvents)
    //            r.RenderPreviewEvent(evt, animate: false);
    //    });
    //}

    // Query Handlers
    public bool IsCharactersTurn(CharacterId characterId)
    {
        if (!_gameInstance.Turns.TryGetCurrentCharactersTurn(out var currentTurnCharacter)) return false;
        if (currentTurnCharacter != characterId) { return false; }

        return true;
    }

    public bool TryGetPropPreview(
        PropId propId,
        out IPreviewPresentation presentation)
    {
        return _gameInstance.Maps.TryGetPropPreview(propId, out presentation);
    }

    public bool TryGetItemPreview(ItemId itemUid, out IPreviewPresentation presentation)
    {
        return _gameInstance.Items.TryGetItemPreview(itemUid, _gameInstance.Characters.PlayerCharacterId, out presentation);
    } 

    public void RequestQueueMoveInDirection(Vec2Int direction)
    {
        //throw new NotImplementedException();
    }

    public void RequestEndTurn()
    {
        //throw new NotImplementedException();
    }

    public bool TryPreviewCommand(IGameCommand command, out List<IGameEvent> previewEvents)
    {        
        return _gameInstance.TryPreviewCommand(command, out previewEvents).Ok;
    }

    public bool TryPreviewCommands(List<IGameCommand> commands, out List<IGameEvent> previewEvents)
    {
        return _gameInstance.TryPreviewCommands(commands, out previewEvents).Ok;
    }

    public void RequestQueuePrimaryInteractionInDirection(
        Vec2 direction)
    {
        if (!_gameInstance.Characters.TryGetPlayerCharacter(out var player))
            return;

        if (!_gameInstance.Characters.TryGetWeapon(player.Value, out var weaponId))
            return;

        var context = new InteractionContext(
            _gameInstance.RootModel.PlayerCharacterId.Value,
            weaponId,
            new DirectionTarget(direction)
        );

        if (_gameInstance.Interactions.TryGetPrimaryInteraction(
                context,
                out var interaction))
        {
            _gameInstance.EnqueueActions(
                _gameInstance.RootModel.PlayerCharacterId.Value,
                interaction.Commands);
        }
    }

    public bool CanPreviewPrimaryWeaponAttack()
    {
        if (!_gameInstance.Characters.TryGetPlayerCharacter(out var player)) 
            return false;

        if (!_gameInstance.Characters.TryGetWeapon(player.Value, out var weaponId))
            return false;

        return true;
    }

    public bool TryGetPlayerCharacterLocation(out ICharacterLocation playerCharacterLocation)
    {
        playerCharacterLocation = default;

        if (!_gameInstance.Characters.TryGetPlayerCharacter(out var playerCharacterId))
            return false;

        return _gameInstance.Characters.TryFindCharacterLocation(
            playerCharacterId.Value,
            out playerCharacterLocation);
    }

    //public void StartAttackTargeting()
    //{
    //    // TODO: We will end up with a more complex wrapper for managing attack input
    //    // Since some may be cell, others direction, other multiple
    //    ChangeTargetingMode(TargetingMode.Direction);
    //}

    private void StartTargetingSession(ITargetingSession session)
    {
        _targetingSessions.Push(session);
        session.Enter();
    }

    public void EndTargetingSession()
    {
        if (_targetingSessions.TryPop(out var popped))
            popped.Exit();
    }

    private void ClearHoverVisuals()
    {
        if (_hoveredInteractionContext == null)
            return;

        //// clear renderer hover state
        //Renderers.ForEach(r =>
        //    r.RenderMapTileTargetCleared(
        //        new MapTilePresentation(_hoveredCell.Value)));

        ClearAllPreviews();
    }

    private void RenderHoverPreview(InteractionContext interactionContext)
    {
        if (!_gameInstance.Interactions.TryCreatePreviewFromContext(
            interactionContext, 
            out var presentation))
        return;
        
        ForEachRenderer(r => r.RenderHoverPreview(presentation));
    }

    private void RenderHoverPreviewCleared(InteractionContext interactionContext)
    {
        ForEachRenderer(r => r.RenderHoverPreviewCleared());
    }

    private void RenderHoverTarget(InteractionContext interactionContext)
    {
        if (interactionContext.Target is LocationTarget locationTarget)
        {
            if (locationTarget.Location is MapLocation mapLocation)
            {
                if (!_gameInstance.Databases.TryGetModel(mapLocation.MapId, out MapChunk map))
                    return;

                if (!map.TryGetTileAt(
                    mapLocation.CellFootprint.Position,
                    out TilePlacementOnMap tilePlacementOnMap))
                    return;

                var presentation = new MapTilePresentation(tilePlacementOnMap);

                ForEachRenderer(x => x.RenderMapTileTargeted(presentation));
            }
            else if (locationTarget.Location is InventoryLocation inventoryLocation)
            {
                if (!_gameInstance.Databases.TryResolve(inventoryLocation.InventoryItemId, out Item inventoryItem))
                    return;

                if (!inventoryItem.GetIsInventory())
                    return;

                if (inventoryItem.Inventory.TryGetItemPlacementAt(
                    inventoryLocation.CellPosition,
                    out var placement))
                {
                    var presentation = new ItemPlacementPresentation(_gameInstance, placement);
                    ForEachRenderer(x => x.RenderInventoryItemTargeted(presentation));
                }
                //else if (_actingEntityId != null)
                //{
                //    ForEachRenderer(x => x.RenderInventoryCellTargeted(presentation));
                //}
            }
            else if (locationTarget.Location is AttachedLocation equippedLocation)
            {
                if (equippedLocation.EntityId is not CharacterId characterId)
                    return;

                if (!_gameInstance.Databases.TryResolve(characterId, out Character character))
                    return;

                bool hasItemInSlot = character.TryGetItemInSlot(equippedLocation.SlotPath, out var equippedItem);

                var presentation = new ItemSlotPresentation(_gameInstance, character.Id, equippedLocation.SlotPath, hasItemInSlot ? equippedItem : null);

                ForEachRenderer(x => x.RenderEquippedItemTargeted(presentation));
            }
        }
    }

    private void RenderHoverTargetCleared(InteractionContext interactionContext)
    {
        if (interactionContext.Target is LocationTarget locationTarget)
        {
            if (locationTarget.Location is MapLocation mapLocation)
            {
                if (!_gameInstance.Databases.TryGetModel(mapLocation.MapId, out MapChunk map))
                    return;

                if (!map.TryGetTileAt(mapLocation.CellFootprint.Position, out TilePlacementOnMap tilePlacementOnMap))
                    return;

                var presentation = new MapTilePresentation(tilePlacementOnMap);
                ForEachRenderer(x => x.RenderMapTileTargetCleared(presentation));
            }
            else if (locationTarget.Location is InventoryLocation inventoryLocation)
            {
                if (!_gameInstance.Databases.TryGetModel(inventoryLocation.InventoryItemId, out Item inventoryItem))
                    return;

                if (!inventoryItem.GetIsInventory())
                    return;

                if (!inventoryItem.Inventory.TryGetItemPlacementAt(
                    inventoryLocation.CellPosition,
                    out var placement))
                    return;

                var presentation = new ItemPlacementPresentation(_gameInstance, placement);
                ForEachRenderer(x => x.RenderInventoryTargetCleared(presentation));
            }
            else if (locationTarget.Location is AttachedLocation equippedLocation)
            {
                if (equippedLocation.EntityId is not CharacterId characterId)
                    return;

                if (!_gameInstance.Databases.TryResolve(characterId, out Character character))
                    return;

                bool hasItemInSlot = character.TryGetItemInSlot(equippedLocation.SlotPath, out var equippedItem);

                var presentation = new ItemSlotPresentation(_gameInstance, character.Id, equippedLocation.SlotPath, hasItemInSlot ? equippedItem : null);
                ForEachRenderer(x => x.RenderEquippedItemTargetCleared(presentation));
            }
        }
    }

    public void Sync(RootGameModelPresentation rootGameModelPresentation)
    {
        ForEachRenderer((r) => r.Sync(rootGameModelPresentation));
    }

    protected override void OnRendererBound(IMainGameRenderer renderer)
    {
        renderer.BindToWorldCoordinator(WorldCoordinator);
        renderer.BindToInteractionCoordinator(PlayerInteractionCoordinator);

        SyncOperatingSystemCoordinator();

        if (renderer is IGenoSysRenderer genoSysRenderer && _genoSysCoordinator != null)
            _genoSysCoordinator.BindRenderer(genoSysRenderer);

        renderer.Sync(_gameInstance.PullRootGameModelPresentation());
    }

    protected override void OnRendererUnbound(IMainGameRenderer renderer)
    {
        if (renderer is IGenoSysRenderer genoSysRenderer && _genoSysCoordinator != null)
            _genoSysCoordinator.UnbindRenderer(genoSysRenderer);
    }

    private void SyncOperatingSystemCoordinator()
    {
        var operatingSystemId = ResolveActiveOperatingSystemId();
        var shouldHostGenoSys = string.Equals(
            operatingSystemId,
            OperatingSystemIds.GenoSys,
            StringComparison.OrdinalIgnoreCase);

        if (!shouldHostGenoSys)
        {
            if (_genoSysCoordinator == null)
                return;

            foreach (var renderer in Renderers.ToArray())
            {
                if (renderer is IGenoSysRenderer genoSysRenderer)
                    _genoSysCoordinator.UnbindRenderer(genoSysRenderer);
            }

            RemoveSubCoordinator(_genoSysCoordinator);
            _genoSysCoordinator = null;
            return;
        }

        if (_genoSysCoordinator != null)
            return;

        _genoSysCoordinator = new GenoSysCoordinator(_gameInstance);
        AddSubCoordinator(_genoSysCoordinator);

        foreach (var renderer in Renderers.ToArray())
        {
            if (renderer is IGenoSysRenderer genoSysRenderer)
                _genoSysCoordinator.BindRenderer(genoSysRenderer);
        }
    }

    private string ResolveActiveOperatingSystemId()
    {
        if (!_gameInstance.Characters.TryGetPlayerCharacter(out var playerId) ||
            !playerId.HasValue ||
            !_gameInstance.Databases.TryGetModel(playerId.Value, out Character playerCharacter))
        {
            return string.Empty;
        }

        return playerCharacter.OperatingSystem?.Id ?? string.Empty;
    }

    public void BindPlayerInteractionRenderer(IPlayerInteractionRenderer renderer)
    {
        PlayerInteractionCoordinator.BindRenderer(renderer);
    }

    public void UnbindPlayerInteractionRenderer(IPlayerInteractionRenderer renderer)
    {
        PlayerInteractionCoordinator.BindRenderer(null);
    }

    public void TryGetItemPresentation(ItemId itemUid, out ItemPresentation presentation)
    {
        presentation = new ItemPresentation(_gameInstance, itemUid);
    }

    public CharacterId GetPlayerCharacterId() => _gameInstance.Characters.PlayerCharacterId.Value;

    internal void TryExecuteCommand(IGameCommand gameCommand)
    {
        _gameInstance.TryExecuteCommand(gameCommand);
    }
}
