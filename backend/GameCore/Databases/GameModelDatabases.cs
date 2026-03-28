using FastCloner.Code;
using System;
using System.Collections.Generic;
using System.Linq;

class SimulationLayer
{
    public Dictionary<IGameDbId, IGameDbResolvable> Overrides = new();
}

public sealed class SimulationLayerSnapshot
{
    internal readonly Dictionary<IGameDbId, IGameDbResolvable> Overrides;

    internal SimulationLayerSnapshot(Dictionary<IGameDbId, IGameDbResolvable> overrides)
    {
        Overrides = overrides;
    }
}

public sealed class GameModelDatabases : BaseEntity
{
    private List<SimulationLayer> _layers = new();
    public int SimDepth => _layers.Count;

    private ModelDatabase<IGameDbResolvable, IGameDbId> Models { get; set; } = new();

    public GameModelDatabases()
    {
    }

    public void PushSimulation() => _layers.Add(new SimulationLayer());
    public void PopSimulation() => _layers.RemoveAt(_layers.Count - 1);
    public bool TryPopSimulationLayer(out SimulationLayerSnapshot snapshot)
    {
        snapshot = null;

        if (_layers.Count == 0)
            return false;

        var top = _layers[^1];
        _layers.RemoveAt(_layers.Count - 1);
        snapshot = new SimulationLayerSnapshot(CloneOverrides(top.Overrides));
        return true;
    }

    public void PushSimulationLayer(SimulationLayerSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        _layers.Add(new SimulationLayer
        {
            Overrides = CloneOverrides(snapshot.Overrides)
        });
    }

    public bool CommitOldestSimulationLayer()
    {
        if (_layers.Count == 0)
            return false;

        var oldest = _layers[0];
        _layers.RemoveAt(0);
        ApplyLayerToBase(oldest);
        return true;
    }

    public void ClearSimulationLayers()
    {
        _layers.Clear();
    }

    public void CommitSimulation()
    {
        var top = _layers[^1];
        _layers.RemoveAt(_layers.Count - 1);
        ApplyLayerToBase(top);
    }

    internal bool TryGetVisible(
        IGameDbId id,
        out IGameDbResolvable model)
    {
        if (id == null || !id.IsValid)
        {
            model = null;
            return false;
        }

        return TryResolveVisibleModel(id, _layers.Count - 1, out model);
    }

    internal IEnumerable<IGameDbResolvable> GetAllModels()
    {
        return BuildEffectiveMap().Values;
    }

    internal List<SavePacket> Save()
    {
        return Models.GetAllModels().ConvertAllToList(x => ((ICustomSerialization)x).SaveAsPacket());
    }

    public bool TryGetMutable(
    IGameDbId id,
    out IGameDbResolvable model)
    {
        if (_layers.Count == 0)
            return Models.TryGetModel(id, out model);

        var top = _layers[^1];

        if (top.Overrides.TryGetValue(id, out model))
            return model != null;

        if (!TryResolveVisibleModel(id, _layers.Count - 2, out var source))
            return false;

        var clone = GameModelFactory.CloneForSimulation(source);

        top.Overrides[id] = clone;
        model = clone;
        return true;
    }

    internal bool TryAddModel(IGameDbResolvable entity)
    {
        if (entity == null || entity.Id == null || !entity.Id.IsValid)
            return false;

        if (_layers.Count == 0)
            return Models.TryAddModel(entity);

        if (TryResolveVisibleModel(entity.Id, _layers.Count - 1, out _))
            return false;

        _layers[^1].Overrides[entity.Id] = entity;
        return true;
    }

    internal bool TryRemoveModel(IGameDbId id)
    {
        if (id == null || !id.IsValid)
            return false;

        if (_layers.Count == 0)
            return Models.TryRemoveModel(id);

        if (!TryResolveVisibleModel(id, _layers.Count - 1, out _))
            return false;

        _layers[^1].Overrides[id] = null;
        return true;
    }

    internal Dictionary<IGameDbId, IGameDbResolvable> GetRaw()
    {
        return BuildEffectiveMap();
    }

    internal SimulationLayersPresentation BuildSimulationLayersPresentation()
    {
        var baseMap = new Dictionary<IGameDbId, IGameDbResolvable>(Models.GetRaw());
        var currentMap = BuildEffectiveMap();
        var allIds = new HashSet<IGameDbId>(baseMap.Keys);
        allIds.UnionWith(currentMap.Keys);

        foreach (var layer in _layers)
        {
            allIds.UnionWith(layer.Overrides.Keys);
        }

        var entities = new List<SimulationEntityPresentation>();

        foreach (var id in allIds)
        {
            baseMap.TryGetValue(id, out var baseModel);
            currentMap.TryGetValue(id, out var currentModel);
            var depth = GetOwningDepth(id);

            var model = currentModel ?? baseModel;
            var changeKind =
                baseModel == null && currentModel != null ? SimulationEntityChangeKind.Added :
                baseModel != null && currentModel == null ? SimulationEntityChangeKind.Removed :
                baseModel != null && currentModel != null && depth > 0 ? SimulationEntityChangeKind.Changed :
                SimulationEntityChangeKind.Unchanged;

            entities.Add(new SimulationEntityPresentation(
                id,
                id.ToString(),
                model?.Name ?? id.Value,
                model?.GetType().Name ?? "Unknown",
                depth,
                changeKind,
                BuildDetails(model, id, depth, changeKind)));
        }

        return new SimulationLayersPresentation(entities, SimDepth);
    }

    private void ApplyLayerToBase(SimulationLayer layer)
    {
        foreach (var kv in layer.Overrides)
        {
            if (kv.Value == null)
                Models.TryRemoveModel(kv.Key);
            else
                Models.TryAddOrUpdateModel(kv.Value);
        }
    }

    private bool TryResolveVisibleModel(IGameDbId id, int maxLayerIndex, out IGameDbResolvable model)
    {
        model = null;

        for (int i = maxLayerIndex; i >= 0; i--)
        {
            if (!_layers[i].Overrides.TryGetValue(id, out var layerModel))
                continue;

            if (layerModel == null)
                return false;

            model = layerModel;
            return true;
        }

        return Models.TryGetModel(id, out model);
    }

    private Dictionary<IGameDbId, IGameDbResolvable> BuildEffectiveMap()
    {
        var effective = new Dictionary<IGameDbId, IGameDbResolvable>(Models.GetRaw());

        foreach (var layer in _layers)
        {
            foreach (var kv in layer.Overrides)
            {
                if (kv.Value == null)
                    effective.Remove(kv.Key);
                else
                    effective[kv.Key] = kv.Value;
            }
        }

        return effective;
    }

    private static Dictionary<IGameDbId, IGameDbResolvable> CloneOverrides(
        Dictionary<IGameDbId, IGameDbResolvable> source)
    {
        var clone = new Dictionary<IGameDbId, IGameDbResolvable>();
        foreach (var kv in source)
        {
            clone[kv.Key] = kv.Value == null ? null : GameModelFactory.CloneForSimulation(kv.Value);
        }

        return clone;
    }

    private static string GetSortKey(IGameDbResolvable model)
    {
        if (model == null)
            return string.Empty;

        return $"{model.GetType().Name}:{model.Name}:{model.Id}";
    }

    private static string BuildDetails(
        IGameDbResolvable model,
        IGameDbId id,
        int depth,
        SimulationEntityChangeKind changeKind)
    {
        if (model == null)
            return $"{id}\nChange: {changeKind}";

        return string.Join("\n", new[]
        {
            model.Name,
            model.GetType().Name,
            id.ToString(),
            $"Depth {depth} | {changeKind}"
        });
    }

    private int GetOwningDepth(IGameDbId id)
    {
        for (int i = _layers.Count - 1; i >= 0; i--)
        {
            if (_layers[i].Overrides.ContainsKey(id))
                return i + 1;
        }

        return 0;
    }
}

public static class GameModelFactory
{
    public static bool TryBuildEntities(
        List<IBlueprint> constructionDataGraph, 
        IGameDbId rootInstanceId, 
        out IGameDbId resolvedInstanceId,
        out List<IGameDbResolvable> buildModels)
    {
        resolvedInstanceId = null;

        string desiredId = rootInstanceId != null ? rootInstanceId.Value : Guid.NewGuid().ToString();

        buildModels = new();

        Dictionary<IGameDbId, IGameDbResolvable> builtModelsMap = new();
        Dictionary<IGameDbId, IBlueprint> constructionMap = new();

        foreach (var constructionData in constructionDataGraph)
        {
            if (TryBuildBaseEntity(constructionData, out var builtEntity))
            {
                builtModelsMap.Add(constructionData.Id, builtEntity);
                buildModels.Add(builtEntity);
                constructionMap.Add(constructionData.Id, constructionData);
            }
        }

        // We have all built models, so lets now assign them
        foreach (var builtModel in builtModelsMap)
        {
            var constructionData = constructionMap[builtModel.Key];
            AttachEntities(builtModel.Value, constructionData, builtModelsMap);
        }

        // Gather Ids
        Dictionary<ITypedStringId, ITypedStringId> idMap = new();
        foreach (var builtModel in builtModelsMap.Values)
        {
            if (builtModel is IHasGameDbResolvableReferences dbResolveableRef)
            {
                var idsToRemap = dbResolveableRef.GetChildIdReferences();

                resolvedInstanceId = (IGameDbId)builtModel.Id.NewOfSameType(desiredId);
                idMap[builtModel.Id] = resolvedInstanceId;

                for (int i = 0; i < idsToRemap.Count; i++)
                {
                    if (idMap.ContainsKey(builtModel.Id))
                        continue;

                    // These Ids are generated as Guids
                    idMap[builtModel.Id] = idsToRemap[i].NewOfSameType();
                }
            }
        }

        // Assign Remapped Ids
        foreach (var builtModel in builtModelsMap.Values)
        {
            if (builtModel is IHasGameDbResolvableReferences dbResolveableRef)
            {
                dbResolveableRef.RemapIds(idMap);
            }
        }

        return true;
    }

    public static bool TryBuildBaseEntity(IBlueprint blueprint, out IGameDbResolvable entity)
    {
        entity = null;

        // TODO: Constructor Activator function
        if (blueprint is CharacterBlueprint)
        {
            entity = new Character();
        }
        else if (blueprint is ItemBlueprint)
        {
            entity = new Item();
        }
        else if (blueprint is PropBlueprint)
        {
            entity = new Prop();
        }
        else if (blueprint is MapChunkBlueprint)
        {
            entity = new MapChunk();
        }
        else if (blueprint is WorldBlueprint)
        {
            entity = new World();
        }

        entity.ApplyBlueprint(blueprint);

        return entity != null;
    }

    public static void AttachEntities(IGameDbResolvable builtModel, IBlueprint saveData, Dictionary<IGameDbId, IGameDbResolvable> modelsLookup)
    {
        if (builtModel is Character character)
        {
            character.AttachEntities((CharacterBlueprint)saveData, modelsLookup);
            //character.AttachEntities((CharacterSaveData)saveData, modelsLookup);
        }
        else if (builtModel is Item item)
        {
            //item.AttachEntities((ItemSaveData)saveData, modelsLookup);
        }
        else if (builtModel is Prop prop)
        {
            //prop.AttachEntities((PropSaveData)saveData, modelsLookup);
        }
        else if (builtModel is MapChunk mapChunk)
        {
            mapChunk.AttachEntities((MapChunkBlueprint)saveData, modelsLookup);
        }
        else if (builtModel is World world)
        {
            world.AttachEntities((WorldBlueprint)saveData, modelsLookup);
        }
    }

    public static IGameDbResolvable CloneForSimulation(IGameDbResolvable originalModel)
    {
        return FastCloner.FastCloner.DeepClone(originalModel);
    }
}
