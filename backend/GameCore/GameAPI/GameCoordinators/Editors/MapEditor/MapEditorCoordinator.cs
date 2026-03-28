using System;
using System.Collections.Generic;
using System.Linq;

public record Tool(ToolDescription ToolDescription, IReadOnlyList<ToolDescription> SubTools);
public record ToolDescription(string Id, string Name);

public interface IMapEditorQueries : ICoordinatorQueries
{
    bool CanDropEntityAtLocation(IGameDbId entityId, MapLocation location);
    bool CanDropTemplateAtLocation(ITemplateDbId templateId, MapLocation location);
}

public interface IMapEditorCommands : ICoordinatorCommands
{
    void SetTool(string id);
    void SetToolSubTool(string id);
    void SaveCurrentMapToDisk();
    void HoverLocation(MapLocation location);
    void StopHoveringLocation(MapLocation location);
    void PrimaryCommitLocation(MapLocation location);
    void SecondaryCommitLocation(MapLocation location);
    void PlaceEntityAtLocation(IGameDbId entityId, MapLocation location);
    void PlaceTemplateAtLocation(ITemplateDbId templateId, MapLocation location);
}

public interface IMapEditorRenderer : IRenderer<MapEditorPresentation>
{
    void HandleWorldCoordinatorCreated(WorldCoordinator worldCoordinator);
    void RenderToolSet(IReadOnlyList<Tool> tools, ToolDescription currentMode, ToolDescription currentShape);
}

public class MapEditorCoordinator
    : CoordinatorBase<IMapEditorQueries, IMapEditorCommands, IMapEditorRenderer>,
      IMapEditorQueries,
      IMapEditorCommands
{
    public override IMapEditorQueries QueriesHandler => this;
    public override IMapEditorCommands CommandsHandler => this;

    // --- State ---
    public readonly string Id;
    private readonly WorldCoordinator _worldCoordinator;
    private readonly Dictionary<string, Tool> _tools = new();

    private CellPosition? _dragStart;

    private ITemplateDbId _selectedTemplateDbId;
    private IGameDbId _stagedEntityId;
    private bool _stagedEntityIsTemporary;

    private MapChunkTemplateId? _templateId;
    private MapChunkId? _mapId;

    private ToolDescription _currentMode;
    private ToolDescription _currentShape;

    // --- Convenience properties ---
    private bool HasMap => _mapId.HasValue;
    private MapChunkId MapId => _mapId!.Value;
    private bool HasTemplate => _templateId.HasValue;
    private MapChunkTemplateId TemplateId => _templateId!.Value;

    // --- Constructor ---
    public MapEditorCoordinator(GameInstance gameAPI, string id) : base(gameAPI)
    {
        Id = id;

        _worldCoordinator = new WorldCoordinator(gameAPI);
        AddSubCoordinator(_worldCoordinator);

        RegisterTools();
        _currentMode = _tools["add"].ToolDescription;
        _currentShape = _tools["add"].SubTools.FirstOrDefault();
    }

    private void RegisterTools()
    {
        var shapes = new List<ToolDescription>
        {
            new("single",         "Single"),
            new("line",           "Line"),
            new("fill",           "Fill"),
            new("square-outline", "Square-Outline"),
            new("square",         "Square"),
        };

        AddTool("pick", "Pick", noSubTools: true);
        AddTool("add", "Add", shapes);
        AddTool("delete", "Delete", shapes);
        AddTool("replace", "Replace", shapes);

        void AddTool(string id, string name, IReadOnlyList<ToolDescription> subTools = null, bool noSubTools = false) =>
            _tools[id] = new Tool(new ToolDescription(id, name), noSubTools ? new List<ToolDescription>() : subTools ?? new List<ToolDescription>());
    }

    // --- Lifecycle ---
    protected override void OnRendererBound(IMapEditorRenderer renderer)
    {
        renderer.HandleWorldCoordinatorCreated(_worldCoordinator);
        renderer.Sync(BuildPresentation());
    }

    internal void CreateNewMap()
    {
        _templateId = MapChunkTemplateId.New();
        MapChunkId mapChunkId = MapChunkId.New();

        ExecuteTracked(new MapsAPI.Commands.CreateMap(Root.SYSTEM_ID, mapChunkId, 48, 48));
        SetMap(mapChunkId);
    }

    internal void LoadMapFromInstance(MapChunkId mapInstanceId)
    {
        SetMap(mapInstanceId);
    }

    internal void LoadMapFromTemplate(MapChunkTemplateId mapChunkTemplateId)
    {
        _templateId = mapChunkTemplateId;

        MapChunkId mapChunkId = MapChunkId.New();

        ExecuteCommand(new TemplatesAPI.Commands.SpawnModelFromTemplate(mapChunkTemplateId, mapChunkId));

        SetMap(mapChunkId);
    }

    private void SetMap(MapChunkId mapChunkId)
    {
        _mapId = mapChunkId;

        if (!_mapId.HasValue) return;

        _worldCoordinator.SetMap(_mapId.Value);

        if (!_mapId.Value.IsValid) return;

        ForEachRenderer(r => r.Sync(BuildPresentation()));
    }

    internal void SetStagedEntity(IGameDbId entityId)
    {
        // Only delete if the previous staged entity was a temporary template instance
        if (_stagedEntityIsTemporary && _stagedEntityId != null)
            _gameInstance.TryExecuteCommand(new DatabaseAPI.Commands.RemoveEntity(_stagedEntityId));

        _selectedTemplateDbId = null;
        _stagedEntityId = entityId;
        _stagedEntityIsTemporary = false;
    }

    internal void SetStagedTemplate(ITemplateDbId selectedTemplateId)
    {
        // Clean up previous temporary instance
        if (_stagedEntityIsTemporary && _stagedEntityId != null)
            _gameInstance.TryExecuteCommand(new DatabaseAPI.Commands.RemoveEntity(_stagedEntityId));

        _selectedTemplateDbId = selectedTemplateId;

        if (_selectedTemplateDbId == null)
            return;

        if (!_gameInstance.Templates.CreateModelInstanceIdFromTemplateId(_selectedTemplateDbId, out var instanceId))
            return;

        if (_gameInstance.TryExecuteCommand(
            new TemplatesAPI.Commands.SpawnModelFromTemplate(
                _selectedTemplateDbId, 
                instanceId)).Ok)
        {
            _stagedEntityId = instanceId;
            _stagedEntityIsTemporary = true;
        }
    }

    internal void FrameLocation(IGameModelLocation location, bool immediate = false)
    {
        _worldCoordinator.FrameLocation(location, immediate);
    }

    internal void ClearFramingTarget()
    {
        _worldCoordinator.ClearFramingTarget();
    }

    // --- Commands ---
    public void SetTool(string id)
    {
        var availableTools = GetAvailableTools();
        var tool = availableTools.FirstOrDefault(candidate => candidate.ToolDescription.Id == id);
        if (tool == null)
            return;

        _currentMode = tool.ToolDescription;
        _currentShape = tool.SubTools.FirstOrDefault();

        BroadcastToolState();
    }

    public void SetToolSubTool(string id)
    {
        var subTool = GetAvailableTools()
            .SelectMany(t => t.SubTools)
            .FirstOrDefault(st => st.Id == id);

        if (subTool == null) return;

        _currentShape = subTool;
        BroadcastToolState();
    }

    public void ResizeMap(int w, int h)
    {
        ExecuteTracked(new MapsAPI.Commands.ResizeMap(Root.SYSTEM_ID, MapId, w, h));
    }

    public bool CanDropEntityAtLocation(IGameDbId entityId, MapLocation location)
    {
        if (!HasMap || location.MapId != MapId || entityId == null)
            return false;

        return _gameInstance.Maps.CanAdd(location.MapId, (IDbId)entityId, location.CellFootprint);
    }

    public bool CanDropTemplateAtLocation(ITemplateDbId templateId, MapLocation location)
    {
        if (!HasMap || location.MapId != MapId || templateId == null)
            return false;

        if (!_gameInstance.Templates.CreateModelInstanceIdFromTemplateId(templateId, out var instanceId))
            return false;

        return _gameInstance.Maps.CanAdd(location.MapId, (IDbId)instanceId, location.CellFootprint);
    }

    public void ClearMap()
    {
        if (!HasMap) return;
        ExecuteTracked(new MapsAPI.Commands.ClearMap(Root.SYSTEM_ID, MapId));
    }

    public void DeleteMapTemplate(MapChunkTemplateId id)
    {
        ExecuteTracked(new TemplatesAPI.Commands.DeleteTemplate(id));
        ExecuteCommand(new TemplatesAPI.Commands.SaveAllTemplates());
    }

    public void ProcessActionOnCell(CellPosition cellPosition, string key)
    {
        if (!_gameInstance.Maps.TryGetPlacementOnMapTile(MapId, cellPosition, out _)) return;
        // TODO: process action on entity
    }

    public void HoverLocation(MapLocation location)
    {
        if (TryHandleBuildHover(location))
            return;

        SubmitIntent(new Intents.GameEntityLocationHoverEnterIntent(location));
    }

    public void StopHoveringLocation(MapLocation location)
    {
        if (TryHandleBuildHoverExit())
            return;

        SubmitIntent(new Intents.GameEntityLocationHoverExitIntent(location));
    }

    public void PrimaryCommitLocation(MapLocation location)
    {
        if (TryHandleBuildPrimaryCommit(location))
            return;

        SubmitIntent(new Intents.GameEntityLocationPrimaryCommit(location));
    }

    public void SecondaryCommitLocation(MapLocation location)
    {
        if (TryHandleBuildSecondaryCommit(location))
            return;

        SubmitIntent(new Intents.GameEntityLocationSecondaryCommit(location));
    }

    public void PlaceEntityAtLocation(IGameDbId entityId, MapLocation location)
    {
        if (entityId == null || !CanDropEntityAtLocation(entityId, location))
            return;

        ExecuteTracked(new MapsAPI.Commands.AddEntityToMap(
            Root.SYSTEM_ID,
            location.MapId,
            entityId,
            location.CellFootprint.Position));
    }

    public void PlaceTemplateAtLocation(ITemplateDbId templateId, MapLocation location)
    {
        if (templateId == null || !CanDropTemplateAtLocation(templateId, location))
            return;

        if (!_gameInstance.Templates.CreateModelInstanceIdFromTemplateId(templateId, out var instanceId))
            return;

        ExecuteTracked(new List<IGameCommand>
        {
            new TemplatesAPI.Commands.SpawnModelFromTemplate(templateId, instanceId),
            new MapsAPI.Commands.AddEntityToMap(
                Root.SYSTEM_ID,
                location.MapId,
                instanceId,
                location.CellFootprint.Position)
        });
    }

    // --- Intent handling ---
    protected override void HandleIntent(Intents.IIntent intent)
    {
        switch (intent)
        {
            case Intents.GameEntityLocationHoverEnterIntent { TargetEntityLocation: MapLocation mapLocation }:
                TryHandleBuildHover(mapLocation);
                break;

            case Intents.GameEntityLocationPrimaryCommit { TargetEntityLocation: MapLocation mLoc }:
                TryHandleBuildPrimaryCommit(mLoc);
                break;

            case Intents.GameEntityLocationSecondaryCommit { TargetEntityLocation: MapLocation mLoc }:
                TryHandleBuildSecondaryCommit(mLoc);
                break;
        }
    }

    private bool TryHandleBuildHover(MapLocation mapLocation)
    {
        if (!TryBuildInteractionContext(mapLocation, out var context))
            return false;

        ForEachRenderer(r => r.ClearAllPreviews());

        if (!_gameInstance.Interactions.TryGetPrimaryInteraction(context, out var interaction))
            return true;

        ExecutePreviewCommands(interaction.Commands);
        return true;
    }

    private bool TryHandleBuildHoverExit()
    {
        if (!IsBuildModeActive())
            return false;

        ForEachRenderer(r => r.ClearAllPreviews());
        return true;
    }

    private bool TryHandleBuildPrimaryCommit(MapLocation location)
    {
        if (!IsBuildModeActive())
            return false;

        ForEachRenderer(r => r.ClearAllPreviews());

        if (_currentShape?.Id != "single" && !_dragStart.HasValue)
        {
            _dragStart = location.CellFootprint.Position;
            return true;
        }

        if (!TryBuildInteractionContext(location, out var context))
        {
            ResetDrag();
            return true;
        }

        if (_gameInstance.Interactions.TryGetPrimaryInteraction(context, out var interaction))
            ExecuteTracked(interaction.Commands);

        ResetDrag();
        return true;
    }

    private bool TryHandleBuildSecondaryCommit(MapLocation location)
    {
        if (!TryBuildInteractionContext(location, out var context))
            return false;

        SubmitIntent(new Intents.Editor.OpenInteractionContextMenu(context));
        return true;
    }

    private bool TryBuildInteractionContext(MapLocation location, out InteractionContext context)
    {
        context = default;

        if (!TryBuildBulkLocationTarget(location, out var target))
            return false;

        if (!TryResolveInteractionActor(out var actorId))
            return false;

        context = new InteractionContext(
            actorId,
            _stagedEntityId,
            target);
        return true;
    }

    private bool TryBuildBulkLocationTarget(MapLocation location, out BulkLocationTarget target)
    {
        target = default;

        if (!IsBuildModeActive() || !HasMap || location.MapId != MapId)
            return false;

        if (!_gameInstance.Databases.TryGetModelUntypedReadOnly(_stagedEntityId, out var model) ||
            model is not IMapPlaceable placeable)
        {
            return false;
        }

        var anchorStart = _currentShape?.Id == "single"
            ? location.CellFootprint.Position
            : _dragStart ?? location.CellFootprint.Position;
        var anchorEnd = location.CellFootprint.Position;
        var cells = ResolveCells(anchorStart, anchorEnd, placeable.SizeOnMap).ToList();

        if (cells.Count == 0)
            return false;

        target = new BulkLocationTarget(
            MapId,
            cells,
            placeable.SizeOnMap,
            _currentShape?.Id ?? "single",
            anchorStart,
            anchorEnd);
        return true;
    }

    private bool TryResolveInteractionActor(out CharacterId actorId)
    {
        actorId = default;

        if (_gameInstance.Characters.TryGetPlayerCharacter(out var playerId) && playerId.HasValue)
        {
            actorId = playerId.Value;
            return true;
        }

        if (_gameInstance.Databases.TryGetModel(Root.SYSTEM_ID, out Character _))
        {
            actorId = Root.SYSTEM_ID;
            return true;
        }

        return false;
    }

    private bool IsBuildModeActive()
    {
        return HasMap &&
               _currentMode?.Id == "add" &&
               _stagedEntityId != null;
    }

    private void ResetDrag()
    {
        _dragStart = null;
    }

    // --- Cell operations ---
    private void ApplyToCell(CellPosition cellPosition)
    {
        if (!HasMap) return;

        switch (_currentMode?.Id)
        {
            case "add": break;
            case "delete": TryDelete(cellPosition); break;
            case "replace": TryDelete(cellPosition); TryAdd(cellPosition); break;
            case "pick":    /* TODO: select entity */   break;
        }
    }

    private void TryAdd(CellPosition cellPosition)
    {
        if (!_gameInstance.Databases.TryGetModelUntypedReadOnly(_stagedEntityId, out var model))
            return;

        if (model is not IMapPlaceable placeable)
            return;

        ExecuteTracked(new MapsAPI.Commands.AddEntityToMap(Root.SYSTEM_ID, MapId, _stagedEntityId, cellPosition));

        // TODO
        //if (_gameInstance.Databases.TryDuplicateModel(_stagedEntityId, out var newId))
        //    _stagedEntityId = newId;
    }

    private void TryDelete(CellPosition cellPosition)
    {
        if (!_gameInstance.Maps.TryGetPlacementOnMapTile(MapId, cellPosition, out var placement)) return;

        ExecuteTracked(new MapsAPI.Commands.RemovePlacementFromMap(Root.SYSTEM_ID, MapId, placement.Id));
    }

    // --- Cell geometry ---
    private IEnumerable<CellPosition> ResolveCells(
        CellPosition startPosition,
        CellPosition endPosition,
        CellSize cellSize)
    {
        if (_currentShape == null) yield break;

        switch (_currentShape.Id)
        {
            case "single":
                yield return new CellPosition(startPosition.X, startPosition.Y);
                yield break;

            case "line":
                foreach (var cell in ResolveLineCells(startPosition, endPosition))
                    yield return cell;
                yield break;

            case "square":
            case "square-outline":
            case "fill":
                foreach (var cell in ResolveGridCells(startPosition, endPosition, cellSize))
                    yield return cell;
                break;
        }
    }

    private IEnumerable<CellPosition> ResolveLineCells(CellPosition start, CellPosition end)
    {
        var positions = new List<CellPosition>();

        int x0 = start.X;
        int y0 = start.Y;
        int x1 = end.X;
        int y1 = end.Y;
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            positions.Add(new CellPosition(x0, y0));

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return positions;
    }

    private IEnumerable<CellPosition> ResolveGridCells(
        CellPosition start, 
        CellPosition end,
        CellSize placementSize)
    {
        var minX = Math.Min(start.X, end.X);
        var maxX = Math.Max(start.X, end.X);
        var minY = Math.Min(start.Y, end.Y);
        var maxY = Math.Max(start.Y, end.Y);

        int stepsX = Math.Max(1, (int)Math.Ceiling((maxX - minX + 1) / (double)placementSize.Width));
        int stepsY = Math.Max(1, (int)Math.Ceiling((maxY - minY + 1) / (double)placementSize.Height));

        bool isOutlineMode = _currentShape.Id == "square-outline";

        for (int ix = 0; ix < stepsX; ix++)
            for (int iy = 0; iy < stepsY; iy++)
            {
                bool isOutline = ix == 0 || iy == 0 || ix == stepsX - 1 || iy == stepsY - 1;
                if (isOutlineMode && !isOutline) continue;

                yield return new CellPosition(minX + ix * placementSize.Width, minY + iy * placementSize.Height);
            }
    }

    // --- Helpers ---
    private MapEditorPresentation BuildPresentation()
    {
        var availableTools = GetAvailableTools();
        EnsureCurrentToolSelection(availableTools);

        MapPresentation mapPresentation = null;

        if (_mapId.HasValue)
            mapPresentation = new MapPresentation(_gameInstance, _mapId.Value);

        return new MapEditorPresentation(
            _mapId,
            _templateId,
            mapPresentation,
            availableTools,
            _currentMode,
            _currentShape
        );
    }

    public void SyncRenderers()
    {
        var presentation = BuildPresentation();
        ForEachRenderer(r => r.Sync(presentation));
    }

    private void BroadcastToolState()
    {
        var availableTools = GetAvailableTools();
        EnsureCurrentToolSelection(availableTools);
        ForEachRenderer(r => r.RenderToolSet(availableTools, _currentMode, _currentShape));
    }

    private IReadOnlyList<Tool> GetAvailableTools()
    {
        var tools = _tools.Values.ToList();

        if (!CanShowBuildTools())
            tools = tools.Where(tool => tool.ToolDescription.Id != "add").ToList();

        return tools;
    }

    private void EnsureCurrentToolSelection(IReadOnlyList<Tool> availableTools)
    {
        if (availableTools == null || availableTools.Count == 0)
        {
            _currentMode = null;
            _currentShape = null;
            ResetDrag();
            return;
        }

        var currentTool = availableTools.FirstOrDefault(tool => tool.ToolDescription.Id == _currentMode?.Id);
        if (currentTool == null)
        {
            currentTool = availableTools[0];
            _currentMode = currentTool.ToolDescription;
            _currentShape = currentTool.SubTools.FirstOrDefault();
            ResetDrag();
            return;
        }

        if (_currentShape == null || !currentTool.SubTools.Any(subTool => subTool.Id == _currentShape.Id))
            _currentShape = currentTool.SubTools.FirstOrDefault();
    }

    private bool CanShowBuildTools()
    {
        if (!TryResolveInteractionActor(out var actorId))
            return false;

        return _gameInstance.OperatingSystems.HasGrant(
            actorId,
            OperatingSystemGrantIds.Interactions.Build,
            OperatingSystemAccessLevel.Write);
    }

    private void DestroyCurrentMapIfValid()
    {
        if (HasMap)
            ExecuteTracked(new MapsAPI.Commands.DestroyMap(Root.SYSTEM_ID, _mapId!.Value));
    }

    public void SaveCurrentMapToDisk()
    {
        if (!HasMap)
            return;

        ExecuteCommand(new TemplatesAPI.Commands.CreateNewTemplateFromModel(_mapId, _templateId));
        ExecuteCommand(new TemplatesAPI.Commands.SaveAllTemplates());
    }
}
