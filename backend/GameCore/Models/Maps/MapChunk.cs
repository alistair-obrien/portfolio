using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

public record MapTileOverrideSaveData(int Index, bool Walkable, RenderKey RenderKey);

public record MapPlacementSaveData
{
    // The underlying string ID for whichever type is used
    public IGameDbId Id;

    public CellPosition Position;

    public MapPlacementSaveData() { }

    public MapPlacementSaveData(IMapPlacement placement)
    {
        Id = placement.Id;
        Position = placement.Footprint.Position;
    }
}

public class MapChunkBlueprint : IBlueprint
{
    IGameDbId IBlueprint.Id => Id;

    public string TypeId => "MapChunk";

    public MapChunkId Id;
    string IBlueprint.Name { get => Name; set => Name = value; }
    public string Name;

    public CellSize Size;
    public List<MapTileOverrideSaveData> TileOverrides;
    public List<MapPlacementSaveData> Placements;

    public MapChunkBlueprint()
    {
        Id = MapChunkId.New();
        Size = new CellSize(48, 48);
        TileOverrides = new();
        Placements = new();
    }

    public MapChunkBlueprint(MapChunk mapChunk)
    {
        Id = mapChunk.Id;
        Name = mapChunk.Name;
        Size = new CellSize(mapChunk.Width, mapChunk.Height);
        TileOverrides = new();
        Placements = mapChunk.GetAllPlacements().ConvertAllToList(x => x.Save());
    }
}

/// <summary>
/// Represents a rectangular grid-based map chunk containing tiles and entity placements.
/// Supports placement operations, pathfinding, and spatial queries.
/// </summary>
public class MapChunk : Entity<MapChunkBlueprint>, 
    IGameDbResolvable,
    IHasGameDbResolvableReferences,
    IHasAttachments
{
    protected override string TypeId => "MapChunk";
    protected override int Version => 1;
    IGameDbId IGameDbResolvable.Id => Id;
    IDbId IDbResolvable.Id => Id;


    private IMapLocation _location;
    IGameModelLocation IGameDbResolvable.AttachedLocation => _location;

    public string Name { get; private set; }

    internal void AttachEntities(MapChunkBlueprint saveData, Dictionary<IGameDbId, IGameDbResolvable> modelsLookup)
    {
        foreach (var placement in saveData.Placements)
        {
            if (modelsLookup.TryGetValue(placement.Id, out var gameDbResolvable))
            {
                TryAttach(gameDbResolvable, placement.Position, out _);
            }
        }
    }

    public List<IGameDbId> GetChildIdReferences()
    {
        List<IGameDbId> referenceIds = new();

        foreach (var placement in _placements)
        {
            if (placement is CharacterPlacementOnMap characterPlacement)
                referenceIds.AddRange(characterPlacement.GetChildIdReferences());
            else if (placement is PropPlacementOnMap propPlacement)
                referenceIds.AddRange(propPlacement.GetChildIdReferences());
            if (placement is ItemPlacementOnMap itemPlacement)
                referenceIds.AddRange(itemPlacement.GetChildIdReferences());
        }

        return referenceIds;
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        Id = (MapChunkId)idMap[Id];

        foreach (var placement in _placements)
        {
            if (placement is CharacterPlacementOnMap characterPlacement)
                characterPlacement.RemapIds(idMap);
            else if (placement is PropPlacementOnMap propPlacement)
                propPlacement.RemapIds(idMap);
            if (placement is ItemPlacementOnMap itemPlacement)
                itemPlacement.RemapIds(idMap);
        }

        RebuildIdDictionary();
    }

    // ============================================================================
    // CONSTANTS
    // ============================================================================

    private static readonly Vec2Int[] Directions = {
        Vec2Int.up,
        Vec2Int.down,
        Vec2Int.left,
        Vec2Int.right
    };

    // ============================================================================
    // FIELDS & PROPERTIES
    // ============================================================================


    public MapChunkId Id { get; private set; }
    private CellSize _size;
    public int Width => _size.Width;
    public int Height => _size.Height;
    private MapTile[] _tiles;
    public MapTile[] Tiles => _tiles;
    private List<IMapPlacement> _placements;

    private Dictionary<IGameDbId, IMapPlacement> _placementById;

    // Pathfinding cache
    private int _mapVersion;
    private int _cachedVersion;
    private int[] _cameFrom;
    private bool[] _visited;
    private int[] _queue;
    private int _queueHead;
    private int _queueTail;
    private CellFootprint _cachedStartfootprint;
    private bool _hasCachedField;
    private bool[] _occupancyGrid;

    // ============================================================================
    // CONSTRUCTORS
    // ============================================================================


    private void RebuildIdDictionary()
    {
        _placementById = _placements?.ToDictionary(p => p.Id, p => p)
                        ?? new Dictionary<IGameDbId, IMapPlacement>();
    }

    // ============================================================================
    // INITIALIZATION
    // ============================================================================

    private void InitializeTiles()
    {
        for (int i = 0; i < _tiles.Length; i++)
        {
            _tiles[i] ??= new MapTile(true, new RenderKey("hey"));
        }
    }

    private void InitializePathfindingBuffers()
    {
        int size = Width * Height;
        _cameFrom = new int[size];
        _visited = new bool[size];
        _queue = new int[size];
        _occupancyGrid = new bool[size];
    }

    // ============================================================================
    // GRID COORDINATE HELPERS
    // ============================================================================

    internal bool InBounds(MapLocation mapLocation) => InBounds(mapLocation.CellFootprint);

    internal bool InBounds(CellFootprint footprint) =>
        InBounds(footprint.X, footprint.Y, footprint.Width, footprint.Height);

    internal bool InBounds(int x, int y, int width, int height) =>
        InBounds(x, y) && InBounds(x + width - 1, y + height - 1);

    internal bool InBounds(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height;

    internal int GetIndex(int x, int y) => y * Width + x;

    internal Vec2Int GetGridPositionOfTileFromFlatIndex(int index) =>
        new Vec2Int(index % Width, index / Width);

    internal MapTile GetTile(int x, int y) =>
        InBounds(x, y) ? _tiles[GetIndex(x, y)] : null;

    internal IEnumerable<TilePlacementOnMap> GetTiles()
    {
        for (int i = 0; i < _tiles.Length; i++)
        {
            var position = GetGridPositionOfTileFromFlatIndex(i);
            yield return new TilePlacementOnMap(Id, position.x, position.y, _tiles[i]);
        }
    }

    // ============================================================================
    // PLACEMENT - CORE OPERATIONS
    // ============================================================================

    private bool TryAddPlacement(IMapPlacement placement)
    {
        if (!placement.Id.IsValid || _placementById.ContainsKey(placement.Id))
            return false;

        if (IsCellBlockedOrOutOfBounds(placement.Footprint))
            return false;

        _placements.Add(placement);
        _placementById[placement.Id] = placement;
        _mapVersion++;

        return true;
    }

    internal bool TryRemovePlacement(
        IGameDbId id,
        out IMapPlacement removed)
    {
        removed = null;

        if (!_placementById.TryGetValue(id, out var placement))
            return false;

        _placements.Remove(placement);
        _placementById.Remove(id);
        _mapVersion++;

        removed = placement;
        return true;
    }

    private bool TryMovePlacement(
        IGameDbId id,
        Vec2Int targetCell,
        out MoveResult moveResult)
    {
        moveResult = default;

        if (!_placementById.TryGetValue(id, out var existing))
            return false;

        var projected = new CellFootprint(
            targetCell.x,
            targetCell.y,
            existing.Footprint.Width,
            existing.Footprint.Height);

        if (IsCellBlockedOrOutOfBounds(projected, id))
            return false;

        _placements.Remove(existing);

        var updated = existing.WithFootprint(projected);
        _placements.Add(updated);
        _placementById[id] = updated;

        _mapVersion++;

        moveResult = new MoveResult(
            true,
            Id,
            existing.Footprint,
            Id,
            projected);

        return true;
    }

    // ============================================================================
    // PLACEMENT - CHARACTER OPERATIONS
    // ============================================================================

    internal bool TryAttach(
        IGameDbResolvable model,
        CellPosition cellPosition,
        out IMapPlacement placement)
    {
        placement = default;

        if (model is not IMapPlaceable placeable)
            return false;

        if (!TryCreatePlacement(placeable, cellPosition, out placement))
            return false;

        if (!TryAddPlacement(placement))
            return false;

        return true;
    }

    private bool TryCreatePlacement(IMapPlaceable placeable, CellPosition cellPosition, out IMapPlacement mapPlacement)
    {
        var footprint = new CellFootprint(cellPosition, placeable.SizeOnMap);
        switch (placeable)
        {
            case Character character:
                mapPlacement = new CharacterPlacementOnMap(character.Id, footprint);
                return true;
            case Prop prop:
                mapPlacement = new PropPlacementOnMap(prop.Id, footprint);
                return true;
            case Item item:
                mapPlacement = new ItemPlacementOnMap(item.Id, footprint);
                return true;
            default:
                throw new Exception($" {placeable.GetType().Name} not supported");
        }
    }

    internal bool TryMoveInDirection(
        IGameDbId id,
        Vec2Int direction,
        int tiles,
        out MoveResult moveResult)
    {
        moveResult = default;

        if (!_placementById.TryGetValue(id, out var existing))
            return false;

        var originalFootprint = existing.Footprint;

        direction = ClampDirection(direction);
        var targetFootprint = CalculateMaxMovement(originalFootprint, direction, tiles);

        if (!TryMovePlacement(id, new Vec2Int(targetFootprint.X, targetFootprint.Y), out moveResult))
            return false;

        return true;
    }

    // ============================================================================
    // MOVEMENT HELPERS
    // ============================================================================
    private Vec2Int ClampDirection(Vec2Int direction) =>
        new Vec2Int(
            Mathf.Clamp(direction.x, -1, 1),
            Mathf.Clamp(direction.y, -1, 1));

    private CellFootprint CalculateMaxMovement(CellFootprint start, Vec2Int direction, int maxTiles)
    {
        CellFootprint lastValidFootprint = start;

        for (int i = 0; i < maxTiles; i++)
        {
            CellFootprint nextPos = lastValidFootprint + direction;

            if (IsCellBlockedOrOutOfBounds(nextPos))
                break;

            lastValidFootprint = nextPos;
        }

        return lastValidFootprint;
    }

    // ============================================================================
    // PLACEMENT - VALIDATION
    // ============================================================================

    public bool CanAdd(IDbId id, CellFootprint footprint)
    {
        if (!id.IsValid)
            return false;

        if (IsCellBlockedOrOutOfBounds(footprint))
            return false;

        if (id is ITemplateDbId)
            return true;

        if (id is IGameDbId gameId && !TryGetPlacement(gameId, out _))
            return true;

        return false;
    }

    public bool CanRemove(IGameDbId id)
    {
        if (!id.IsValid)
            return false;

        return TryGetPlacement(id, out _);
    }

    internal bool CanAddItem(ItemId itemId, CellFootprint footprint) => CanAdd(itemId, footprint);
    internal bool CanRemoveItem(ItemId itemUid) => CanRemove(itemUid);

    internal bool CanAddProp(PropId propId, CellFootprint footprint) => CanAdd(propId, footprint);
    internal bool CanRemoveProp(PropId propId) => CanRemove(propId);

    internal bool CanAddCharacter(CharacterId characterId, CellFootprint footprint) => CanAdd(characterId, footprint);
    internal bool CanRemoveCharacter(CharacterId characterId) => CanRemove(characterId);

    // ============================================================================
    // PLACEMENT - QUERIES
    // ============================================================================

    internal bool TryGetCharacterPlacement(CharacterId id, out CharacterPlacementOnMap placement) =>
        TryGetPlacement<CharacterId, CharacterPlacementOnMap>(id, out placement);

    internal bool TryGetItemPlacement(ItemId id, out ItemPlacementOnMap placement) =>
        TryGetPlacement<ItemId, ItemPlacementOnMap>(id, out placement);

    internal bool TryGetPropPlacement(PropId id, out PropPlacementOnMap placement) =>
        TryGetPlacement<PropId, PropPlacementOnMap>(id, out placement);

    internal bool TryGetCharacterAt(CellPosition cellPosition, out CharacterId characterId)
    {
        if (TryGetPlacementAtCell<CharacterPlacementOnMap>(cellPosition, out var placement))
        {
            characterId = placement.CharacterId;
            return true;
        }

        characterId = default;
        return false;
    }

    internal bool TryGetItemAt(CellPosition cellPosition, out ItemId itemId)
    {
        if (TryGetPlacementAtCell<ItemPlacementOnMap>(cellPosition, out var placement))
        {
            itemId = placement.ItemId;
            return true;
        }

        itemId = default;
        return false;
    }

    internal bool TryGetPropAt(CellPosition cellPosition, out PropId propId)
    {
        if (TryGetPlacementAtCell<PropPlacementOnMap>(cellPosition, out var placement))
        {
            propId = placement.PropId;
            return true;
        }

        propId = default;
        return false;
    }

    internal bool TryGetPlacementAt(CellPosition cellPosition, out IMapPlacement placement)
    {
        placement = null;

        if (!InBounds(cellPosition.X, cellPosition.Y))
            return false;

        var cell = new CellFootprint(cellPosition, new CellSize(1, 1));

        foreach (var p in _placements)
        {
            if (p.Footprint.Intersects(cell))
            {
                placement = p;
                return true;
            }
        }

        return false;
    }

    internal bool TryGetTileAt(CellPosition position, out TilePlacementOnMap tilePlacementOnMap)
    {
        tilePlacementOnMap = null;

        if (!InBounds(position.X, position.Y))
            return false;

        var tile = GetTile(position.X, position.Y);
        if (tile == null)
            return false;

        tilePlacementOnMap = new TilePlacementOnMap(Id, position.X, position.Y, tile);
        return true;
    }

    public bool TryGetPlacement(IGameDbId id, out IMapPlacement placement) =>
        _placementById.TryGetValue(id, out placement);

    public bool TryGetPlacement<TId, TPlacement>(TId id, out TPlacement placement)
        where TId : struct, IGameDbId
        where TPlacement : class, IMapPlacement
    {
        placement = null;

        if (!_placementById.TryGetValue(id, out var raw))
            return false;

        if (raw is not TPlacement typed)
            return false;

        placement = typed;
        return true;
    }

    private bool TryGetPlacementAtCell<TPlacement>(CellPosition cellPosition, out TPlacement placement)
        where TPlacement : class, IMapPlacement
    {
        placement = null;

        if (!InBounds(cellPosition.X, cellPosition.Y))
            return false;

        var cell = new CellFootprint(cellPosition, new CellSize(1, 1));

        foreach (var p in _placements)
        {
            if (p is TPlacement typed && typed.Footprint.Intersects(cell))
            {
                placement = typed;
                return true;
            }
        }

        return false;
    }

    // ============================================================================
    // OCCUPANCY CHECKS
    // ============================================================================

    private bool IsCellBlockedOrOutOfBounds(CellFootprint footprint, IGameDbId ignore = null)
    {
        if (!InBounds(footprint))
            return true;

        // check tiles
        for (int dy = 0; dy < footprint.Height; dy++)
            for (int dx = 0; dx < footprint.Width; dx++)
            {
                var tile = GetTile(footprint.X + dx, footprint.Y + dy);
                if (tile == null || !tile.Walkable)
                    return true;
            }

        // check other placements
        foreach (var placement in _placements)
        {
            if (ignore != null && placement.Id.Equals(ignore))
                continue;

            if (footprint.Intersects(placement.Footprint))
                return true;
        }

        return false;
    }

    internal bool HasPlacementAt(CellPosition cellPosition) => TryGetPlacementAt(cellPosition, out _);

    // ============================================================================
    // LOCATION FINDING
    // ============================================================================

    internal IItemLocation FindItemLocation(ItemId itemId)
    {
        if (!TryGetItemPlacement(itemId, out var placement))
            return null;

        return new MapLocation(Id, placement.Footprint);
    }

    internal ICharacterLocation FindCharacterLocation(CharacterId characterId)
    {
        if (!TryGetCharacterPlacement(characterId, out var placement))
            return null;

        return new MapLocation(Id, placement.Footprint);
    }

    internal bool FindFirstAvailableMapTileForItem(
        ItemId itemId,
        MapLocation fromTile,
        out MapLocation availableTile)
    {
        availableTile = default;

        if (fromTile.MapId != Id || !InBounds(fromTile.CellFootprint.X, fromTile.CellFootprint.Y))
            return false;

        var start = fromTile.CellFootprint;
        var visited = new HashSet<CellFootprint>();
        var frontier = new Queue<CellFootprint>();

        frontier.Enqueue(start);
        visited.Add(start);

        while (frontier.Count > 0)
        {
            var currFootprint = frontier.Dequeue();

            if (CanAddItem(itemId, currFootprint))
            {
                availableTile = new MapLocation(Id, currFootprint);
                return true;
            }

            foreach (var dir in Directions)
            {
                var next = currFootprint + dir;
                if (InBounds(next) && visited.Add(next))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        return false;
    }

    // ============================================================================
    // PATHFINDING
    // ============================================================================

    internal bool TryFindPathForCharacter(CharacterId characterId, CellPosition goal, out PathResult pathResult)
    {
        pathResult = null;

        if (!TryGetCharacterPlacement(characterId, out var placement))
            return false;

        return FindPathFromFootprint(placement.Footprint, goal, out pathResult);
    }

    internal bool TryFindPathForItem(ItemId itemId, CellPosition goal, out PathResult pathResult)
    {
        pathResult = null;

        if (!TryGetItemPlacement(itemId, out var placement))
            return false;

        return FindPathFromFootprint(placement.Footprint, goal, out pathResult);
    }

    internal bool TryFindPathForProp(PropId propId, CellPosition goal, out PathResult pathResult)
    {
        pathResult = null;

        if (!TryGetPropPlacement(propId, out var placement))
            return false;

        return FindPathFromFootprint(placement.Footprint, goal, out pathResult);
    }

    private bool FindPathFromFootprint(CellFootprint startFootprint, CellPosition goal, out PathResult pathResult)
    {
        bool needsRecompute = !_hasCachedField ||
                              _cachedVersion != _mapVersion ||
                              _cachedStartfootprint != startFootprint;

        if (needsRecompute)
        {
            PrecomputeReachable(startFootprint);
        }

        return TryGetCachedPath(goal, out pathResult);
    }

    internal void PrecomputeReachable(CellFootprint footprint)
    {
        int total = Width * Height;
        if (_visited == null || _visited.Length != total)
        {
            InitializePathfindingBuffers();
        }

        _cachedStartfootprint = footprint;
        _hasCachedField = true;
        _cachedVersion = _mapVersion;

        BuildOccupancyGrid(footprint);

        Array.Clear(_visited, 0, total);

        _queueHead = 0;
        _queueTail = 0;

        int startIndex = GetIndex(footprint.X, footprint.Y);
        _visited[startIndex] = true;
        _cameFrom[startIndex] = startIndex;
        _queue[_queueTail++] = startIndex;

        while (_queueHead < _queueTail)
        {
            int currentIndex = _queue[_queueHead++];
            int cx = currentIndex % Width;
            int cy = currentIndex / Width;

            ExpandNeighbor(cx + 1, cy, currentIndex, footprint.Size);
            ExpandNeighbor(cx - 1, cy, currentIndex, footprint.Size);
            ExpandNeighbor(cx, cy + 1, currentIndex, footprint.Size);
            ExpandNeighbor(cx, cy - 1, currentIndex, footprint.Size);
        }
    }

    private void BuildOccupancyGrid(CellFootprint ignore)
    {
        Array.Clear(_occupancyGrid, 0, _occupancyGrid.Length);

        foreach (var placement in _placements)
        {
            if (placement.Footprint == ignore)
                continue;

            for (int dy = 0; dy < placement.Footprint.Height; dy++)
                for (int dx = 0; dx < placement.Footprint.Width; dx++)
                {
                    int x = placement.Footprint.X + dx;
                    int y = placement.Footprint.Y + dy;

                    if (InBounds(x, y))
                        _occupancyGrid[GetIndex(x, y)] = true;
                }
        }
    }

    private void ExpandNeighbor(int nx, int ny, int parentIndex, CellSize size)
    {
        if (!InBounds(nx, ny) || !CanStandAtFast(size, nx, ny))
            return;

        int nextIndex = GetIndex(nx, ny);
        if (_visited[nextIndex])
            return;

        _visited[nextIndex] = true;
        _cameFrom[nextIndex] = parentIndex;
        _queue[_queueTail++] = nextIndex;
    }

    private bool CanStandAtFast(CellSize size, int x, int y)
    {
        if (!InBounds(x, y) || !InBounds(x + size.Width - 1, y + size.Height - 1))
            return false;

        for (int dy = 0; dy < size.Height; dy++)
            for (int dx = 0; dx < size.Width; dx++)
            {
                int ix = x + dx;
                int iy = y + dy;
                int index = GetIndex(ix, iy);

                if (_occupancyGrid[index])
                    return false;

                var tile = _tiles[index];
                if (tile == null || !tile.Walkable)
                    return false;
            }

        return true;
    }

    internal bool TryGetCachedPath(CellPosition goal, out PathResult result)
    {
        result = null;

        if (!_hasCachedField || !InBounds(goal.X, goal.Y))
            return false;

        int goalIndex = GetIndex(goal.X, goal.Y);
        if (!_visited[goalIndex])
            return false;

        var path = new List<Vec2Int>(32);
        int current = goalIndex;
        int startIndex = GetIndex(_cachedStartfootprint.X, _cachedStartfootprint.Y);

        while (current != startIndex)
        {
            path.Add(new Vec2Int(current % Width, current / Width));
            current = _cameFrom[current];
        }

        path.Reverse();
        result = new PathResult(path);
        return true;
    }

    // ============================================================================
    // RAYCASTING
    // ============================================================================

    internal List<LinecastHit> GetPlacementsIntersectedByRay(GridRaycaster.GridRay ray, float bulletHitRadius)
    {
        var hits = new List<LinecastHit>();
        var placements = new HashSet<IGridPlacement>();

        foreach (var step in ray)
        {
            if (!TryGetPlacementAt(step.Cell, out var placement))
                continue;

            if (placements.Contains(placement))
                continue;

            placements.Add(placement);

            if (placement is ItemPlacementOnMap or TilePlacementOnMap)
                continue;

            Vec2 tileCenter = new Vec2(step.Cell.X + 0.5f, step.Cell.Y + 0.5f);
            float t = Vec2.Dot(tileCenter - ray.StartPos, ray.EndPos - ray.StartPos) /
                      Vec2.Dot(ray.EndPos - ray.StartPos, ray.EndPos - ray.StartPos);
            float tClamped = Mathf.Clamp01(t);
            float distanceAlongRay = tClamped * Vec2.Distance(ray.StartPos, ray.EndPos);
            Vec2 pointAlongRay = Vec2.Lerp(ray.StartPos, ray.EndPos, tClamped);

            hits.Add(CreateLinecastHit(placement, step.Cell, pointAlongRay, step.DistanceTiles, distanceAlongRay));
        }

        return hits;
    }

    private static LinecastHit CreateLinecastHit(
        IGridPlacement placement,
        CellPosition hitCell,
        Vec2 hitPoint,
        int distanceTiles,
        float distance)
    {
        return placement switch
        {
            CharacterPlacementOnMap c => new CharacterLinecastHit(c, hitCell, hitPoint, distanceTiles, distance),
            PropPlacementOnMap p => new PropObjectLinecastHit(p, hitCell, hitPoint, distanceTiles, distance),
            _ => new GenericPlacementLinecastHit(placement, hitCell, hitPoint, distanceTiles, distance)
        };
    }

    // ============================================================================
    // COLLECTIONS & ENUMERATION
    // ============================================================================

    public IReadOnlyList<TilePlacementOnMap> GetAllTiles() => GetTiles().ToList();

    public IReadOnlyList<IMapPlacement> GetAllPlacements() => _placements;

    // ============================================================================
    // BULK OPERATIONS
    // ============================================================================

    internal void Clear()
    {
        _placements.Clear();
        _placementById.Clear();
        _mapVersion++;
    }

    internal bool TryRemovePlacementAt(CellPosition cellPosition, out IMapPlacement placement)
    {
        placement = default;

        if (!TryGetPlacementAt(cellPosition, out placement))
            return false;

        return TryRemovePlacement(placement.Id, out placement);
    }

    internal bool TryResize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return false;

        // Create and populate new tile array
        var newTiles = new MapTile[width * height];
        CopyTilesToNewArray(newTiles, width, height);
        InitializeNewTiles(newTiles);

        // Update dimensions
        _size = new CellSize(width, height);
        _tiles = newTiles;

        InitializePathfindingBuffers();
        RemoveOutOfBoundsPlacements();
        RebuildIdDictionary();

        _mapVersion++;
        _hasCachedField = false;

        return true;
    }

    internal IReadOnlyList<IMapPlacement> GetPlacementsOutOfBoundsForSize(CellSize size)
    {
        if (_placements == null || _placements.Count == 0)
            return Array.Empty<IMapPlacement>();

        return _placements
            .Where(p => !FitsWithinSize(p.Footprint, size))
            .ToList();
    }

    private void CopyTilesToNewArray(MapTile[] newTiles, int newWidth, int newHeight)
    {
        for (int y = 0; y < Math.Min(Height, newHeight); y++)
        {
            for (int x = 0; x < Math.Min(Width, newWidth); x++)
            {
                int oldIndex = GetIndex(x, y);
                int newIndex = y * newWidth + x;
                newTiles[newIndex] = _tiles[oldIndex];
            }
        }
    }

    private void InitializeNewTiles(MapTile[] tiles)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] ??= new MapTile(true, new RenderKey("hey"));
        }
    }

    private void RemoveOutOfBoundsPlacements()
    {
        var toRemove = _placements
            .Where(p => !InBounds(p.Footprint))
            .Select(p => p.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            if (_placementById.TryGetValue(id, out var placement))
            {
                _placements.Remove(placement);
                _placementById.Remove(id);
            }
        }
    }

    // ============================================================================
    // UTILITY METHODS
    // ============================================================================

    internal static float DistancePointToSegment(Vec2 point, Vec2 a, Vec2 b)
    {
        Vec2 ab = b - a;
        float t = Mathf.Clamp01(Vec2.Dot(point - a, ab) / Vec2.Dot(ab, ab));
        Vec2 closest = a + t * ab;
        return Vec2.Distance(point, closest);
    }

    void IGameDbResolvable.ClearAttachedLocation()
    {
        _location = null;
    }

    void IGameDbResolvable.SetAttachedLocation(IGameModelLocation targetLocation)
    {
        if (targetLocation is not IMapLocation mapLocation)
            return;

        _location = mapLocation;
    }

    public override MapChunkBlueprint SaveToBlueprint()
    {
        return new MapChunkBlueprint(this);
    }

    public void ReattachReferences(IGameDbResolvable originalModel)
    {
        if (originalModel is not MapChunk mapChunk)
            return;

        _placements = new List<IMapPlacement>(mapChunk._placements);
        _placementById = new Dictionary<IGameDbId, IMapPlacement>(mapChunk._placementById);
    }

    public IEnumerable<IGameDbId> GetAttachedEntityIds()
    {
        foreach (var id in _placementById.Keys)
            yield return id;
    }

    public IEnumerable<IGameDbId> GetReferencedEntityIds()
    {
        yield break;
    }

    public void AttachEntities(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        Debug.Log("Attaching Entities to map");
        if (blueprint is not MapChunkBlueprint mapChunkBlueprint)
            return;


        Debug.Log("Placements: " + mapChunkBlueprint.Placements.Count);
        foreach (var item in mapChunkBlueprint.Placements)
        {
            if (databaseDict.TryGetValue(item.Id, out var model))
            {
                TryAddEntity(model, item.Position);
            }
        }
    }

    private bool TryAddEntity(IGameDbResolvable model, CellPosition position)
    {
        if (model is not IMapPlaceable placeable)
            return false;

        if (!TryCreatePlacement(placeable, position, out var placement))
            return false;

        if (!TryAddPlacement(placement))
            return false;

        return true;
    }

    public void ApplyBlueprint(IBlueprint blueprint)
    {
        if (blueprint is not MapChunkBlueprint data)
            return;

        _placements ??= new();
        _placementById ??= new();

        Id = data.Id;
        Name = data.Name;
        _size = data.Size;
        _tiles = new MapTile[Width * Height];

        RebuildIdDictionary();
        InitializeTiles();
        InitializePathfindingBuffers();
    }

    public IEnumerable<AttachmentChange> GetAttachmentChanges(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        if (blueprint is not MapChunkBlueprint data)
            yield break;

        foreach (var placement in GetPlacementsOutOfBoundsForSize(data.Size))
        {
            yield return new AttachmentChange(
                placement.Id,
                new MapLocation(Id, placement.Footprint),
                new RootLocation(placement.Id));
        }
    }

    private static bool FitsWithinSize(CellFootprint footprint, CellSize size)
    {
        if (footprint.X < 0 || footprint.Y < 0)
            return false;

        if (footprint.X + footprint.Width > size.Width)
            return false;

        if (footprint.Y + footprint.Height > size.Height)
            return false;

        return true;
    }
}
