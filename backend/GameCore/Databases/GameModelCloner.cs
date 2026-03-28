using System;
using System.Collections.Generic;

public sealed record DuplicatedModelGraph(
    IGameDbId RootId,
    IReadOnlyDictionary<IGameDbId, IGameDbResolvable> ClonesByNewId,
    IReadOnlyDictionary<IGameDbId, IGameModelLocation> InternalLocationsByNewId);

public static class GameModelCloner
{
    public static bool TryDuplicateGraph(
        DatabaseAPI database,
        IGameDbId sourceRootId,
        out DuplicatedModelGraph duplicatedGraph,
        IGameDbId desiredRootId = null)
    {
        duplicatedGraph = null;

        if (database == null || sourceRootId == null || !sourceRootId.IsValid)
            return false;

        var sourceGraph = new Dictionary<IGameDbId, IGameDbResolvable>();
        if (!TryCollectGraph(database, sourceRootId, sourceGraph))
            return false;

        var idMap = BuildIdMap(sourceGraph.Keys, sourceRootId, desiredRootId);
        var clonesByNewId = new Dictionary<IGameDbId, IGameDbResolvable>();
        var internalLocationsByNewId = new Dictionary<IGameDbId, IGameModelLocation>();

        foreach (var entry in sourceGraph)
        {
            var sourceModel = entry.Value;
            var clone = GameModelFactory.CloneForSimulation(sourceModel);
            clone.ClearAttachedLocation();

            if (clone is IHasGameDbResolvableReferences refs)
                refs.RemapIds(idMap);

            var newId = (IGameDbId)idMap[entry.Key];
            clonesByNewId[newId] = clone;

            if (TryRemapInternalLocation(sourceModel.AttachedLocation, idMap, out var remappedLocation))
                internalLocationsByNewId[newId] = remappedLocation;
        }

        duplicatedGraph = new DuplicatedModelGraph(
            (IGameDbId)idMap[sourceRootId],
            clonesByNewId,
            internalLocationsByNewId);

        return true;
    }

    private static bool TryCollectGraph(
        DatabaseAPI database,
        IGameDbId currentId,
        Dictionary<IGameDbId, IGameDbResolvable> models)
    {
        if (models.ContainsKey(currentId))
            return true;

        if (!database.TryGetModelUntypedReadOnly(currentId, out var model))
            return false;

        models[currentId] = model;

        if (model is not IHasGameDbResolvableReferences refs)
            return true;

        foreach (var childId in refs.GetChildIdReferences())
        {
            if (childId == null || !childId.IsValid)
                continue;

            if (!TryCollectGraph(database, childId, models))
                return false;
        }

        return true;
    }

    private static Dictionary<ITypedStringId, ITypedStringId> BuildIdMap(
        IEnumerable<IGameDbId> sourceIds,
        IGameDbId sourceRootId,
        IGameDbId desiredRootId)
    {
        var idMap = new Dictionary<ITypedStringId, ITypedStringId>();

        foreach (var sourceId in sourceIds)
        {
            idMap[sourceId] = sourceId.Equals(sourceRootId) && desiredRootId != null
                ? desiredRootId
                : sourceId.NewOfSameType();
        }

        return idMap;
    }

    private static bool TryRemapInternalLocation(
        IGameModelLocation location,
        IReadOnlyDictionary<ITypedStringId, ITypedStringId> idMap,
        out IGameModelLocation remappedLocation)
    {
        remappedLocation = null;

        if (location == null || location.OwnerEntityId == null)
            return false;

        if (!idMap.TryGetValue(location.OwnerEntityId, out var remappedOwner))
            return false;

        remappedLocation = location switch
        {
            AttachedLocation equipped => new AttachedLocation((IGameDbId)remappedOwner, equipped.SlotPath),
            InventoryLocation inventory => new InventoryLocation((ItemId)remappedOwner, inventory.CellPosition),
            GunAmmoLocation gunAmmo => new GunAmmoLocation((ItemId)remappedOwner),
            MapLocation map => new MapLocation((MapChunkId)remappedOwner, map.CellFootprint),
            _ => null
        };

        return remappedLocation != null;
    }
}
