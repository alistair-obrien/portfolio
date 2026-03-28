using System;

public interface IWorldQueries : ICoordinatorQueries
{
    //bool CanInteractWithMaps(); // Add when needed later
    bool TryGetPlayerCharacterLocationPresentation(out MapCharacterPlacementPresentation presentation);
    bool TryGetPlayerCharacterLocation(out ICharacterLocation characterLocation);
    bool IsValidMapLocation(MapLocation location);
    bool TryGetCurrentMap(out MapChunkId currentMapId);
}

public interface IWorldCommands : ICoordinatorCommands
{

}

public interface IWorldRenderer : IRenderer<MapPresentation>
{
    void ShowWorldInteractionPreview(WorldInteractionPreviewPresentation model);
    void HideWorldInteractionPreview();

    void HandleMapLoaded(MapPresentation map);
    void HandleMapUnloaded(MapPresentation map);

    void SetFramingTarget(IGameModelLocation location, bool immediate = false);
    void ClearFramingTarget();
}

public sealed class WorldCoordinator
    : CoordinatorBase<IWorldQueries, IWorldCommands, IWorldRenderer>,
        IWorldQueries,
        IWorldCommands
{
    private MapChunkId _mapId;

    public override IWorldQueries QueriesHandler => this;
    public override IWorldCommands CommandsHandler => this;

    public WorldCoordinator(GameInstance gameAPI) : base(gameAPI) { }

    // ─────────────────────────────────────────────────────────────
    // MAP CHUNK LIFECYCLE
    // ─────────────────────────────────────────────────────────────
    public void HandleMapChunkUnloaded(MapChunkId mapChunkId)
    {
        ForEachRenderer(r => r.HandleMapUnloaded(new MapPresentation(_gameInstance, mapChunkId)));
    }

    protected override void OnRendererBound(IWorldRenderer renderer)
    {
        if (!_mapId.IsValid)
            return;

        renderer.Sync(BuildMapPresentation());
    }

    private MapPresentation BuildMapPresentation()
    {
        return new MapPresentation(_gameInstance, _mapId);
    }

    // ─────────────────────────────────────────────────────────────
    // QUERIES
    // ─────────────────────────────────────────────────────────────

    protected override void DoHandleGameEvent(IGameEvent evt)
    {
        if (evt is MapsAPI.Events.MapLoaded e)
        {
            SetMap(e.MapId);
        }
    }

    internal void SetMap(MapChunkId mapChunkId)
    {
        _mapId = mapChunkId;

        if (!_mapId.IsValid)
            return;

        SyncRenderers();
    }

    internal void FrameLocation(IGameModelLocation location, bool immediate = false)
    {
        ForEachRenderer(r => r.SetFramingTarget(location, immediate));
    }

    internal void ClearFramingTarget()
    {
        ForEachRenderer(r => r.ClearFramingTarget());
    }

    private void SyncRenderers()
    {
        ForEachRenderer(r => r.Sync(BuildMapPresentation()));
    }

    public bool TryGetPlayerCharacterLocationPresentation(
        out MapCharacterPlacementPresentation presentation)
    {
        presentation = default;

        var playerUid = _gameInstance.Characters.PlayerCharacterId;
        
        if (playerUid == null) 
            return false;

        if (!_gameInstance.Characters.TryFindCharacterLocation(
                playerUid.Value,
                out var location))
            return false;

        if (location is not MapLocation mapLocation)
            return false;

        if (!_gameInstance.Maps.TryGetCharacterPlacementOnMap(mapLocation.MapId, playerUid.Value, out var placement))
            return false;

        presentation = new MapCharacterPlacementPresentation(_gameInstance, placement);

        return true;
    }

    public bool TryGetPlayerCharacterLocation(out ICharacterLocation location)
    {
        location = default;

        var playerId = _gameInstance.Characters.PlayerCharacterId;

        if (!playerId.HasValue)
            return false;

        if (!_gameInstance.Characters.TryFindCharacterLocation(playerId.Value, out location))
            return false;

        return true;
    }

    public bool IsValidMapLocation(MapLocation location)
    {
        return _gameInstance.Maps.TileIsValid(location.MapId, location.CellFootprint.X, location.CellFootprint.Y);
    }

    public bool TryGetCurrentMap(out MapChunkId currentMapId)
    {
        currentMapId = _mapId;

        if (!_mapId.IsValid)
            return false;

        return true;
    }
}
