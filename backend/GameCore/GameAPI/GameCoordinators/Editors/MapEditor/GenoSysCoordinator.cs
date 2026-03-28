using System;
using System.Collections.Generic;
using System.Linq;
public record GenoSysPresentation(bool IsPlaying); // TODO

public interface IGenoSysQueries : ICoordinatorQueries
{
    bool TryGetPreviewOnEntity(IGameDbId id, out IPreviewPresentation preview);
    bool TryGetPreviewOnTemplate(ITemplateDbId id, out IPreviewPresentation preview);
}

public interface IGenoSysCommands : ICoordinatorCommands
{
    void Play();
    void Stop();
    void Flatten();
    void SelectEntity(IGameDbId id);
    void Undo();
    void Redo();
    void SetHistoryIndex(int index);
    void SelectContextMenuOption(string optionId);
    void CancelContextMenu();
}

public interface IGenoSysRenderer : IRenderer
{
    void RenderSystemMessage(SystemMessagePresentation systemMessage);

    void RenderTemplatesBrowserOpened(string id, TemplatesBrowserCoordinator coordinator);
    void RenderTemplatesBrowserClosed(string id);

    void RenderMapEditorOpened(string id, MapEditorCoordinator coordinator);
    void RenderMapEditorClosed(string id, MapEditorCoordinator coordinator);

    void RenderGameStart(MainGameCoordinator coordinator);
    void RenderGameStop(MainGameCoordinator coordinator);

    void RenderEntityBlueprintEditorOpened(string id, EntityBlueprintEditorCoordinator coordinator);
    void RenderEntityBlueprintEditorClosed(string id);
    
    void RenderHoverPreview(IPreviewPresentation preview);
    void ClearHoverPreview(IPreviewPresentation preview);
    
    void RenderGameDatabaseBrowserOpened(string id, GameDatabaseBrowserCoordinator coordinator);
    void RenderGameDatabaseBrowerClosed(string id);

    void RenderHistoryState(int index, int length, bool canUndo, bool canRedo);
    void RenderSimulationState(SimulationLayersPresentation presentation);
    void RenderEntitySelected(IGameDbId id);
    void RenderEntityDeselected(IGameDbId id);

    void ShowContextMenu(InteractionContext interactionContext, InteractionOptionsPresentation presentation);
}

public sealed class GenoSysCoordinator :
    CoordinatorBase<IGenoSysQueries, IGenoSysCommands, IGenoSysRenderer>,
    IGenoSysQueries,
    IGenoSysCommands
{
    private ITemplateDbId _selectedTemplateId;
    private ITemplateDbId _hoveredTemplateId;
    private IGameDbId _selectedEntityId;
    private IGameDbId _hoveredEntityId;

    private CharacterId _viewer;

    private MainGameCoordinator _mainGameCoordinator;
    private TemplatesBrowserCoordinator _templatesBrowser;
    private GameDatabaseBrowserCoordinator _hierarchyBrowser;
    private EntityBlueprintEditorCoordinator _entityBlueprintEditor;
    private MapEditorCoordinator _mapEditor;

    private MapChunkTemplateId _lastMapOpened;
    private PendingAttachmentPicker _pendingAttachmentPicker;
    private Dictionary<string, InteractionRequest> _activeContextMenuOptions = new();
    private IGameDbId _contextMenuTransientEntityId;

    private void SyncAfterHistoryChange()
    {
        // HACK: Should build a presentation instead
        ForEachRenderer(r => r.RenderHistoryState(
            _gameInstance.RootModel.HistoryIndex, 
            _gameInstance.RootModel.HistoryLength, 
            _gameInstance.RootModel.CanUndo, 
            _gameInstance.RootModel.CanRedo));
        ForEachRenderer(r => r.RenderSimulationState(
            _gameInstance.RootModel.GameDatabases.BuildSimulationLayersPresentation()));

        // Kinda hacky maybe
        _entityBlueprintEditor?.SyncRenderers();
        _templatesBrowser?.SyncRenderers();
        _hierarchyBrowser?.SyncRenderers();
        _mapEditor?.SyncRenderers();
    }

    private sealed record PendingAttachmentPicker(IGameDbId OwnerId, ISlotId SlotId, Type IdType);

    public override IGenoSysQueries QueriesHandler => this;
    public override IGenoSysCommands CommandsHandler => this;

    public GenoSysCoordinator(GameInstance gameInstance) : base(gameInstance) { }

    protected override void DoHandleGameEvent(IGameEvent evt)
    {
        SyncAfterHistoryChange();
    }

    protected override void OnInitialize()
    {
        _viewer = Root.SYSTEM_ID;
        if (!_gameInstance.Characters.PlayerCharacterId.HasValue)
            ExecuteCommand(new CharactersAPI.Commands.AssignPlayerCharacter(Root.SYSTEM_ID));

        string templatesBrowserId = Guid.NewGuid().ToString();
        _templatesBrowser = new TemplatesBrowserCoordinator(_gameInstance, templatesBrowserId);
        AddSubCoordinator(_templatesBrowser);
        ForEachRenderer(r => r.RenderTemplatesBrowserOpened(templatesBrowserId, _templatesBrowser));

        string hierarchyBrowserId = Guid.NewGuid().ToString();
        _hierarchyBrowser = new GameDatabaseBrowserCoordinator(hierarchyBrowserId, _gameInstance);
        AddSubCoordinator(_hierarchyBrowser);
        ForEachRenderer(r => r.RenderGameDatabaseBrowserOpened(hierarchyBrowserId, _hierarchyBrowser));

        string mapEditorId = Guid.NewGuid().ToString();
        _mapEditor = new MapEditorCoordinator(_gameInstance, mapEditorId);
        AddSubCoordinator(_mapEditor);
        ForEachRenderer(r => r.RenderMapEditorOpened(mapEditorId, _mapEditor));

        string templateEditorId = Guid.NewGuid().ToString();
        _entityBlueprintEditor = new EntityBlueprintEditorCoordinator(_gameInstance, templateEditorId);
        AddSubCoordinator(_entityBlueprintEditor);
        ForEachRenderer(r => r.RenderEntityBlueprintEditorOpened(templateEditorId, _entityBlueprintEditor));

        _hierarchyBrowser.Populate(Root.SYSTEM_ID);
        SyncAfterHistoryChange();
    }

    protected override void OnRendererBound(IGenoSysRenderer renderer)
    {
        Debug.Log("Renderer Bound");
        // HACK: Should build a presentation instead
        renderer.RenderHistoryState(_gameInstance.RootModel.HistoryIndex, _gameInstance.RootModel.HistoryLength, _gameInstance.RootModel.CanUndo, _gameInstance.RootModel.CanRedo);
        renderer.RenderSimulationState(_gameInstance.RootModel.GameDatabases.BuildSimulationLayersPresentation());
    }

    public void PopulateMapEditor(MapChunkTemplateId mapChunkTemplateId)
    {
        _mapEditor.LoadMapFromTemplate(mapChunkTemplateId);
        _lastMapOpened = mapChunkTemplateId;
    }

    public void CloseMapEditor(MapEditorCoordinator coordinator)
    {
        RemoveSubCoordinator(coordinator);
        ForEachRenderer(r => r.RenderMapEditorClosed(coordinator.Id, coordinator));
    }

    public void PopulateEntityBlueprintEditor(ITemplateDbId templateId)
    {
        if (templateId == null || !templateId.IsValid)
            return;

        _entityBlueprintEditor.Populate(templateId);
    }

    public bool TryGetPreviewOnTemplate(ITemplateDbId id, out IPreviewPresentation preview) => _gameInstance.Templates.TryCreateTemplatePreview(id, _viewer, out preview);

    public bool TryGetPreviewOnEntity(IGameDbId id, out IPreviewPresentation preview) => _gameInstance.Interactions.TryCreatePreview(id, _viewer, out preview);

    private void RaiseSystemMessage(string msg, MessageType type) => ForEachRenderer(r => r.RenderSystemMessage(new SystemMessagePresentation(new MessagePayload(msg, type))));

    protected override void HandleIntent(Intents.IIntent intent)
    {
        switch (intent)
        {
            //case Intents.Editor.SaveModelToTemplate i:
            //    ExecuteCommand(new TemplatesAPI.Commands.CreateNewTemplateFromModel(i.GameDbId, i.TemplateDbId));
            //    break;

            case Intents.Editor.MoveEntity i:
                ExecuteTracked(new ItemsAPI.Commands.MoveEntity(Root.SYSTEM_ID, i.GameDbId, i.Locaiton, false));
                break;

            case Intents.Editor.CreateOrUpdateModel i:
                ExecuteTracked(new DatabaseAPI.Commands.CreateOrUpdateModel(i.Blueprint));
                break;

            case Intents.Editor.CreateOrUpdateTemplate i:
                ExecuteTracked(new TemplatesAPI.Commands.CreateOrUpdateTemplate(i.TemplateId, new List<IBlueprint> { i.Blueprint }));
                ExecuteCommand(new TemplatesAPI.Commands.SaveAllTemplates());
                break;

            case Intents.Editor.OpenTemplateEditor i:
                PopulateEntityBlueprintEditor(i.TemplateId);
                break;

            case Intents.Editor.OpenTemplateInMapEditor i:
                PopulateMapEditor(i.MapTemplateId);
                break;

            case Intents.Editor.OpenEntity i:
                HandleOpenEntity(i.EntityId);
                break;

            case Intents.Editor.FrameEntity i:
                HandleFrameEntity(i.EntityId);
                break;

            case Intents.Editor.OpenTemplate i:
                HandleOpenTemplate(i.TemplateId);
                break;

            case Intents.Editor.FrameTemplate i:
                HandleFrameTemplate(i.TemplateId);
                break;

            case Intents.Editor.OpenInteractionContextMenu i:
                OpenContextMenu(i.InteractionContext);
                break;

            case Intents.Editor.CreateNewEntity i:
                var blueprint = DatabaseAPI.BuildNewBlueprint(i.Type);
                ExecuteTracked(new DatabaseAPI.Commands.CreateOrUpdateModel(blueprint));
                SetSelectedEntity(blueprint.Id);
                SetSelectedTemplate(null);
                break;

            case Intents.Editor.TemplateHoverEnter i:
                HandleTemplateHoverEnter(i);
                break;

            case Intents.Editor.TemplateHoverExit i:
                HandleTemplateHoverExit();
                break;

            case Intents.Editor.TemplatePrimaryCommit i:
                HandleTemplatePrimaryCommit(i);
                break;

            case Intents.Editor.OpenEntityPicker i:
                _pendingAttachmentPicker = new PendingAttachmentPicker(i.OwnerId, i.SlotId, i.Type);
                RaiseSystemMessage("Select an entity in the hierarchy browser to assign it to the attachment slot.", MessageType.None);
                break;

            case Intents.Editor.PreviewTemplateActionOverOther i:
                HandlePreviewTemplateActionOverOther(i);
                break;

            case Intents.Editor.CommitTemplateActionOverOther i:
                HandleCommitTemplateActionOverOther(i);
                break;

            case Intents.GameEntityHoverEnter i:
                HandleGameEntityHoverEnter(i);
                break;

            case Intents.GameEntityHoverExit i:
                HandleGameEntityHoverExit(i);
                break;

            case Intents.GameEntityPrimaryCommit i:
                HandleGameEntityPrimaryCommit(i);
                break;

            case Intents.GameEntitySecondaryCommit i:
                HandleGameEntitySecondaryCommit(i);
                break;

            case Intents.GameEntitySecondaryCommitFromTemplate i:
                HandleGameEntitySecondaryCommitFromTemplate(i);
                break;

            case Intents.GameEntityLocationHoverEnterIntent i:
                HandleGameEntityLocationHoverEnter(i);
                break;

            case Intents.GameEntityLocationHoverExitIntent i:
                HandleGameEntityLocationHoverExit(i);
                break;

            case Intents.GameEntityLocationPrimaryCommit i:
                HandleGameEntityLocationPrimaryCommit(i);
                break;

            case Intents.GameEntityLocationSecondaryCommit i:
                HandleGameEntityLocationSecondaryCommit(i);
                break;

            case Intents.Editor.OpenInstanceInMapEditor i:
                _mapEditor?.LoadMapFromInstance(i.MapInstanceId);
                break;
        }
    }

    private void HandleGameEntitySecondaryCommit(Intents.GameEntitySecondaryCommit i)
    {
        CleanupContextMenuTransientEntity();

        if (!_gameInstance.Databases.TryGetModel(_gameInstance.Characters.PlayerCharacterId.Value, out Character character))
            return;

        IGameDbId actingItemEntityId = i.ActingEntityId;
        if (actingItemEntityId == null &&
            character.TryGetItemInSlot(SlotIds.Loadout.HeldItem, out var heldItemId))
        {
            actingItemEntityId = heldItemId;
        }

        var interactionTarget = new EntityTarget(i.EntityId);
        InteractionContext interactionContext = new InteractionContext(
            _gameInstance.Characters.PlayerCharacterId.Value,
            actingItemEntityId,
            interactionTarget
            );

        var availableActions = _gameInstance.Interactions.GetAvailableActions(interactionContext);
        var presentation = BuildContextMenuPresentation(availableActions);

        Renderers.ForEach((renderer) => renderer.ShowContextMenu(interactionContext, presentation));
    }

    private void HandleGameEntitySecondaryCommitFromTemplate(Intents.GameEntitySecondaryCommitFromTemplate i)
    {
        CleanupContextMenuTransientEntity();

        if (i.TemplateId == null || !i.TemplateId.IsValid)
            return;

        if (!_gameInstance.Databases.TryGetModel(_gameInstance.Characters.PlayerCharacterId.Value, out Character character))
            return;

        if (!_gameInstance.Templates.CreateModelInstanceIdFromTemplateId(i.TemplateId, out var instanceId))
            return;

        if (!ExecuteCommand(new TemplatesAPI.Commands.SpawnModelFromTemplate(i.TemplateId, instanceId)).Ok)
            return;

        _contextMenuTransientEntityId = instanceId;

        var interactionTarget = new EntityTarget(i.EntityId);
        var interactionContext = new InteractionContext(
            _gameInstance.Characters.PlayerCharacterId.Value,
            instanceId,
            interactionTarget);

        var availableActions = _gameInstance.Interactions.GetAvailableActions(interactionContext);
        var presentation = BuildContextMenuPresentation(availableActions);

        Renderers.ForEach(renderer => renderer.ShowContextMenu(interactionContext, presentation));
    }

    private void OpenContextMenu(InteractionContext interactionContext)
    {
        CleanupContextMenuTransientEntity();

        var availableActions = _gameInstance.Interactions.GetAvailableActions(interactionContext);
        var presentation = BuildContextMenuPresentation(availableActions);

        Renderers.ForEach(renderer => renderer.ShowContextMenu(interactionContext, presentation));
    }

    private void HandlePreviewTemplateActionOverOther(Intents.Editor.PreviewTemplateActionOverOther i)
    {
        if (_hoveredEntityId != null)
        {
            Debug.Log($"Preview {i.TemplateDbId} on {_hoveredEntityId}");
        }
        else if (_hoveredTemplateId != null)
        {
            Debug.Log($"Preview {i.TemplateDbId} on {_hoveredEntityId}");
        }
    }

    private void HandleCommitTemplateActionOverOther(Intents.Editor.CommitTemplateActionOverOther i)
    {
        if (_hoveredEntityId != null)
        {
            Debug.Log($"Commit {i.TemplateDbId} on {_hoveredTemplateId}");
        }
        else if (_hoveredTemplateId != null)
        {
            Debug.Log($"Commit {i.TemplateDbId} on {_hoveredTemplateId}");
        }
    }

    private void SetSelectedEntity(IGameDbId id)
    {
        var previous = _selectedEntityId;
        _selectedEntityId = id;
        _hierarchyBrowser.SetSelected(_selectedEntityId);
        _entityBlueprintEditor.Populate(_selectedEntityId);

        if (previous != null && !Equals(previous, id))
            ForEachRenderer(r => r.RenderEntityDeselected(previous));

        if (id != null)
            ForEachRenderer(r => r.RenderEntitySelected(id));
    }

    private void SetSelectedTemplate(ITemplateDbId templateId)
    {
        _selectedTemplateId = templateId;
        _templatesBrowser.SetSelected(_selectedTemplateId);
        _entityBlueprintEditor.Populate(_selectedTemplateId);

        if (_selectedTemplateId != null)
            _mapEditor?.SetStagedTemplate(_selectedTemplateId);
    }

    private void HandleOpenEntity(IGameDbId entityId)
    {
        if (entityId == null)
            return;

        SetSelectedEntity(entityId);
        SetSelectedTemplate(null);

        if (_mapEditor == null)
            return;

        if (entityId is MapChunkId mapId)
        {
            _mapEditor.LoadMapFromInstance(mapId);
            _mapEditor.ClearFramingTarget();
            return;
        }

        if (TryResolveDirectMapLocation(entityId, out var directMapLocation))
        {
            _mapEditor.LoadMapFromInstance(directMapLocation.MapId);
            _mapEditor.FrameLocation(directMapLocation, immediate: true);
            return;
        }

        if (TryResolveContainingMapLocation(entityId, out var containingMapLocation))
        {
            _mapEditor.LoadMapFromInstance(containingMapLocation.MapId);
            _mapEditor.FrameLocation(containingMapLocation, immediate: true);
        }
    }

    private void HandleFrameEntity(IGameDbId entityId)
    {
        if (entityId == null)
            return;

        SetSelectedEntity(entityId);
        SetSelectedTemplate(null);

        if (_mapEditor == null)
            return;

        if (entityId is MapChunkId mapId)
        {
            _mapEditor.LoadMapFromInstance(mapId);
            _mapEditor.ClearFramingTarget();
            return;
        }

        if (TryResolveContainingMapLocation(entityId, out var mapLocation))
        {
            _mapEditor.LoadMapFromInstance(mapLocation.MapId);
            _mapEditor.FrameLocation(mapLocation, immediate: true);
            return;
        }

        HandleOpenEntity(entityId);
    }

    private void HandleOpenTemplate(ITemplateDbId templateId)
    {
        if (templateId == null)
            return;

        SetSelectedEntity(null);
        SetSelectedTemplate(templateId);

        if (_mapEditor == null)
            return;

        if (templateId is MapChunkTemplateId mapTemplateId)
        {
            _mapEditor.LoadMapFromTemplate(mapTemplateId);
            _mapEditor.ClearFramingTarget();
        }
    }

    private void HandleFrameTemplate(ITemplateDbId templateId)
    {
        if (templateId == null)
            return;

        SetSelectedEntity(null);
        SetSelectedTemplate(templateId);

        if (_mapEditor == null)
            return;

        if (templateId is MapChunkTemplateId mapTemplateId)
        {
            _mapEditor.LoadMapFromTemplate(mapTemplateId);
            _mapEditor.ClearFramingTarget();
            return;
        }

        HandleOpenTemplate(templateId);
    }

    private bool TryResolveDirectMapLocation(IGameDbId entityId, out MapLocation mapLocation)
    {
        mapLocation = default;

        if (!_gameInstance.Databases.EntityLocationAPI.TryFindEntityLocation(entityId, out var location))
            return false;

        if (location is not MapLocation directMapLocation)
            return false;

        mapLocation = directMapLocation;
        return true;
    }

    private bool TryResolveContainingMapLocation(IGameDbId entityId, out MapLocation mapLocation)
    {
        return TryResolveContainingMapLocation(entityId, new HashSet<IGameDbId>(), out mapLocation);
    }

    private bool TryResolveContainingMapLocation(
        IGameDbId entityId,
        HashSet<IGameDbId> visited,
        out MapLocation mapLocation)
    {
        mapLocation = default;

        if (entityId == null || !visited.Add(entityId))
            return false;

        if (!_gameInstance.Databases.EntityLocationAPI.TryFindEntityLocation(entityId, out var location))
            return false;

        return TryResolveContainingMapLocation(location, visited, out mapLocation);
    }

    private bool TryResolveContainingMapLocation(
        IGameModelLocation location,
        HashSet<IGameDbId> visited,
        out MapLocation mapLocation)
    {
        mapLocation = default;

        switch (location)
        {
            case MapLocation directMapLocation:
                mapLocation = directMapLocation;
                return true;

            case AttachedLocation attachedLocation:
                return TryResolveContainingMapLocation(attachedLocation.EntityId, visited, out mapLocation);

            case InventoryLocation inventoryLocation:
                return TryResolveContainingMapLocation(inventoryLocation.InventoryItemId, visited, out mapLocation);

            case GunAmmoLocation gunAmmoLocation:
                return TryResolveContainingMapLocation(gunAmmoLocation.GunItemId, visited, out mapLocation);

            default:
                return false;
        }
    }

    private void HandleTemplatePrimaryCommit(Intents.Editor.TemplatePrimaryCommit i)
    {
        SetSelectedEntity(null);
        SetSelectedTemplate(i.TemplateId);
    }

    private void HandleTemplateHoverEnter(Intents.Editor.TemplateHoverEnter i)
    {
        var prevHovered = _hoveredTemplateId;
        _hoveredTemplateId = i.TemplateId;

        if (TryGetPreviewOnTemplate(_hoveredTemplateId, out var preview))
            ForEachRenderer(r => r.RenderHoverPreview(preview));
    }

    private void HandleTemplateHoverExit()
    {
        var prevHovered = _hoveredTemplateId;
        _hoveredTemplateId = null;

        if (TryGetPreviewOnTemplate(prevHovered, out var preview))
            ForEachRenderer(r => r.ClearHoverPreview(preview));
    }

    private void HandleGameEntityHoverEnter(Intents.GameEntityHoverEnter i)
    {
        var prevHovered = _hoveredEntityId;
        _hoveredEntityId = i.EntityId;
        //_hierarchyBrowser.SetHovered(prevHovered, _hoveredEntityId);

        if (TryGetPreviewOnEntity(i.EntityId, out var preview))
            ForEachRenderer(r => r.RenderHoverPreview(preview));
    }

    private void HandleGameEntityHoverExit(Intents.GameEntityHoverExit i)
    {
        var prevHovered = _hoveredEntityId;
        _hoveredEntityId = null; // Bug fix: was incorrectly assigning i.EntityId instead of null
        //_hierarchyBrowser.SetHovered(prevHovered, _hoveredEntityId);

        if (TryGetPreviewOnEntity(prevHovered, out var preview))
            ForEachRenderer(r => r.ClearHoverPreview(preview));
    }

    private void HandleGameEntityPrimaryCommit(Intents.GameEntityPrimaryCommit i)
    {
        if (TryCommitPendingAttachmentSelection(i.EntityId))
            return;

        SetSelectedEntity(i.EntityId);
        SetSelectedTemplate(null);
    }

    private void HandleGameEntityLocationPrimaryCommit(Intents.GameEntityLocationPrimaryCommit i)
    {
        if (_gameInstance.Items.EntityLocationAPI.TryGetEntityFromLocation(i.TargetEntityLocation, out var entityId))
        {
            if (TryCommitPendingAttachmentSelection(entityId))
                return;

            SetSelectedEntity(entityId);
            SetSelectedTemplate(null);
            return;
        }

        ExecutePrimaryInteractionAtLocation(i.TargetEntityLocation);
    }

    private bool TryCommitPendingAttachmentSelection(IGameDbId selectedEntityId)
    {
        if (_pendingAttachmentPicker == null)
            return false;

        var pending = _pendingAttachmentPicker;

        if (pending.IdType != null &&
            !pending.IdType.IsAssignableFrom(selectedEntityId.GetType()))
        {
            RaiseSystemMessage(
                $"Selected entity type {selectedEntityId.GetType().Name} is not valid for slot {pending.SlotId}.",
                MessageType.Warning);
            return true;
        }

        SubmitIntent(new Intents.Editor.MoveEntity(selectedEntityId, new AttachedLocation(pending.OwnerId, pending.SlotId)));

        _pendingAttachmentPicker = null;
        SetSelectedEntity(pending.OwnerId);
        SetSelectedTemplate(null);

        return true;
    }

    private void HandleGameEntityLocationSecondaryCommit(Intents.GameEntityLocationSecondaryCommit i)
    {
        if (!_gameInstance.Databases.TryGetModel(_gameInstance.Characters.PlayerCharacterId.Value, out Character character))
            return;

        IGameDbId actingItemEntityId = null;
        if (character.TryGetItemInSlot(SlotIds.Loadout.HeldItem, out var heldItemId))
            actingItemEntityId = heldItemId;

        var interactionTarget = new LocationTarget(i.TargetEntityLocation);
        InteractionContext interactionContext = new InteractionContext(
            _gameInstance.Characters.PlayerCharacterId.Value,
            actingItemEntityId,
            interactionTarget
            );

        var availableActions = _gameInstance.Interactions.GetAvailableActions(interactionContext);
        var presentation = BuildContextMenuPresentation(availableActions);

        Renderers.ForEach((renderer) => renderer.ShowContextMenu(interactionContext, presentation));
    }

    public void SelectContextMenuOption(string optionId)
    {
        if (string.IsNullOrWhiteSpace(optionId))
            return;

        if (!_activeContextMenuOptions.TryGetValue(optionId, out var selectedOption))
            return;

        _activeContextMenuOptions.Clear();

        if (selectedOption.Commands == null || selectedOption.Commands.Count == 0)
        {
            CleanupContextMenuTransientEntity();
            return;
        }

        var execution = ExecuteTracked(selectedOption.Commands);
        if (!execution.Ok)
        {
            CleanupContextMenuTransientEntity();
            return;
        }

        // Selection committed successfully; ownership is transferred to the game state.
        _contextMenuTransientEntityId = null;
    }

    public void CancelContextMenu()
    {
        _activeContextMenuOptions.Clear();
        CleanupContextMenuTransientEntity();
    }

    private InteractionOptionsPresentation BuildContextMenuPresentation(IEnumerable<InteractionRequest> actions)
    {
        _activeContextMenuOptions.Clear();

        var options = new List<InteractionOptionPresentation>();
        foreach (var action in actions)
        {
            var id = Guid.NewGuid().ToString();
            _activeContextMenuOptions[id] = action;
            options.Add(new InteractionOptionPresentation(id, action.Name));
        }

        return new InteractionOptionsPresentation("Interact", options);
    }

    private void CleanupContextMenuTransientEntity()
    {
        if (_contextMenuTransientEntityId == null)
            return;

        if (_gameInstance.Databases.TryGetModelUntypedReadOnly(_contextMenuTransientEntityId, out _))
        {
            ExecuteCommand(new DatabaseAPI.Commands.RemoveEntity(_contextMenuTransientEntityId));
        }

        _contextMenuTransientEntityId = null;
    }

    private void HandleGameEntityLocationHoverEnter(Intents.GameEntityLocationHoverEnterIntent i)
    {
        ClearLocationInteractionPreviews();

        if (_gameInstance.Items.EntityLocationAPI.TryGetEntityFromLocation(i.TargetEntityLocation, out var entity) &&
            TryGetPreviewOnEntity(entity, out var preview))
            ForEachRenderer(r => r.RenderHoverPreview(preview));

        RenderLocationInteractionPreview(i.TargetEntityLocation);
    }

    private void HandleGameEntityLocationHoverExit(Intents.GameEntityLocationHoverExitIntent i)
    {
        if (_gameInstance.Items.EntityLocationAPI.TryGetEntityFromLocation(i.TargetEntityLocation, out var entity) &&
            TryGetPreviewOnEntity(entity, out var preview))
            ForEachRenderer(r => r.ClearHoverPreview(preview));

        ClearLocationInteractionPreviews();
    }

    private void RenderLocationInteractionPreview(IGameModelLocation location)
    {
        if (!TryBuildLocationInteractionContext(location, out var context))
            return;

        if (!_gameInstance.Interactions.TryGetPrimaryInteraction(context, out var interaction))
            return;

        if (!_gameInstance.TryPreviewCommands(interaction.Commands, out var previewEvents).Ok)
            return;

        ForEachRenderer(r => r.RenderPreviewEvents(previewEvents, animate: false));
    }

    private void ClearLocationInteractionPreviews()
    {
        ForEachRenderer(r => r.ClearAllPreviews());
    }

    private void ExecutePrimaryInteractionAtLocation(IGameModelLocation location)
    {
        if (!TryBuildLocationInteractionContext(location, out var context))
            return;

        if (!_gameInstance.Interactions.TryGetPrimaryInteraction(context, out var interaction))
            return;

        ExecuteTracked(interaction.Commands);
    }

    private bool TryBuildLocationInteractionContext(IGameModelLocation location, out InteractionContext context)
    {
        context = default;

        if (!_gameInstance.Characters.TryGetPlayerCharacter(out var playerId))
            return false;

        if (!_gameInstance.Databases.TryGetModel(playerId.Value, out Character character))
            return false;

        IGameDbId actingItemEntityId = null;
        if (character.TryGetItemInSlot(SlotIds.Loadout.HeldItem, out var heldItemId))
            actingItemEntityId = heldItemId;

        context = new InteractionContext(
            playerId.Value,
            actingItemEntityId,
            new LocationTarget(location));
        return true;
    }

    public void Play()
    {
        if (!_gameInstance.Characters.PlayerCharacterId.HasValue)
        {
            RaiseSystemMessage("No player character is assigned in the backend.", MessageType.Warning);
            return;
        }

        var playableCharacterId = _gameInstance.Characters.PlayerCharacterId.Value;
        if (!_gameInstance.Databases.TryGetModel(playableCharacterId, out Character selectedCharacter))
        {
            RaiseSystemMessage("Assigned player character could not be resolved.", MessageType.Warning);
            return;
        }

        if (playableCharacterId == Root.SYSTEM_ID)
        {
            RaiseSystemMessage("Assign a non-root player character to enter play mode.", MessageType.Warning);
            return;
        }

        if (!selectedCharacter.HasOperatingSystemId(OperatingSystemIds.GenoSys))
        {
            RaiseSystemMessage("Assigned player character does not have GenoSYS installed.", MessageType.Warning);
            return;
        }

        if (_mainGameCoordinator == null)
        {
            _gameInstance.PushSimulation();

            _mainGameCoordinator = new MainGameCoordinator(_gameInstance);
            AddSubCoordinator(_mainGameCoordinator);
            ForEachRenderer(r => r.RenderGameStart(_mainGameCoordinator));
        }

    }

    public void Stop()
    {
        if (_mainGameCoordinator == null)
        {
            return;
        }

        _gameInstance.PopSimulation();
        ForEachRenderer(r => r.RenderGameStop(_mainGameCoordinator));
        RemoveSubCoordinator(_mainGameCoordinator);
        _mainGameCoordinator = null;
    }

    public void Undo()
    {
        if (!_gameInstance.RootModel.TryUndoStep())
            return;
        SyncAfterHistoryChange();
    }

    public void Flatten()
    {
        if (!_gameInstance.RootModel.FlattenSimulationLayers())
            return;

        SyncAfterHistoryChange();
    }

    public void SelectEntity(IGameDbId id)
    {
        if (id == null || !id.IsValid)
            return;

        SetSelectedEntity(id);
        SetSelectedTemplate(null);
    }

    public void Redo()
    {
        if (!_gameInstance.RootModel.TryRedoStep())
            return;
        SyncAfterHistoryChange();
    }

    public void SetHistoryIndex(int index)
    {
        _gameInstance.RootModel.SetHistoryIndex(index);
        SyncAfterHistoryChange();
    }
}
