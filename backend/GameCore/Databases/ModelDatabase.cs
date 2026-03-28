using System;
using System.Collections.Generic;
using System.Linq;

public sealed record ModelDatabaseSaveData(Dictionary<string, object> Models);

public interface IUntypedModelDatabase
{
    bool TryGetModel(IDbId id, out IDbResolvable model);
    bool TryAddModel(IDbResolvable model);
    bool TryAddOrUpdateModel(IDbResolvable model);
    bool TryRemoveModel(IDbId id);
    IEnumerable<IDbResolvable> GetAllModels();
}

public class ModelDatabase<TModel, TId>
    : Entity<ModelDatabaseSaveData>,
    IModelDatabase<TModel, TId>, IUntypedModelDatabase
    where TModel : IDbResolvable
    where TId : IDbId
{
    internal static ModelDatabase<TModel,TId> Load(ModelDatabaseSaveData data, int version)
    {
        var loadedGameModel = new ModelDatabase<TModel, TId>();
        loadedGameModel._models = data.Models.ToDictionary(
            kv => (TId)Activator.CreateInstance(typeof(TId), kv.Key),
            kv =>
            {
                object raw = kv.Value;

                if (raw is SavePacket rawSavePacket)
                    return SaveLoaderRegistry.Load<TModel>(rawSavePacket);

                if (raw is Newtonsoft.Json.Linq.JObject jobj)
                {
                    // Happens when JSON deserializer didn't know type
                    var savePacket = jobj.ToObject<SavePacket>();
                    return SaveLoaderRegistry.Load<TModel>(savePacket);
                }

                if (raw is TModel alreadyTyped)
                    return alreadyTyped;

                throw new Exception($"Cannot deserialize model of type {typeof(TModel).Name}");
            });

        return loadedGameModel;
    }

    public override ModelDatabaseSaveData SaveToBlueprint()
    {
        var dict = new Dictionary<string, object>();
        foreach (var kvp in _models)
        {
            object value = kvp.Value;
            if (kvp.Value is ICustomSerialization customSerialization)
                value = customSerialization.SaveAsPacket();

            dict.Add(kvp.Key.ToString(), value);
        }

        return new ModelDatabaseSaveData(dict);
    }

    private Dictionary<TId, TModel> _models = new();

    protected override string TypeId => throw new NotImplementedException();

    protected override int Version => throw new NotImplementedException();

    public IEnumerable<TModel> GetAllModels() => _models.Values;

    public bool TryAddModel(TModel model)
    {
        if (model == null)
            return false;

        if (!model.Id.IsValid)
            return false;

        if (_models.ContainsKey((TId)model.Id))
            return false;

        _models.Add((TId)model.Id, model);
        return true;
    }

    public bool TryAddOrUpdateModel(TModel model)
    {
        if (model == null)
            return false;

        if (!model.Id.IsValid)
            return false;

        if (_models.ContainsKey((TId)model.Id))
            _models[(TId)model.Id] = model;
        else
            _models.Add((TId)model.Id, model);

        return true;
    }

    public bool TryGetModel(TId id, out TModel model)
    {
        model = default;

        if (!id.IsValid)
            return false;

        if (!_models.TryGetValue(id, out model))
            return false;

        return true;
    }

    public bool TryRemoveModel(TId id)
    {
        if (!id.IsValid)
            return false;

        if (!_models.ContainsKey(id))
            return false;

        if (!_models.Remove(id))
            return false;

        return true;
    }

    // ================================
    // IUntypedModelDatabase
    // ================================

    bool IUntypedModelDatabase.TryGetModel(
        IDbId id,
        out IDbResolvable model)
    {
        model = null;

        //Debug.Log($"Trying to Get Model {id}");

        if (id is not TId typedId)
            return false;

        if (!TryGetModel(typedId, out var typedModel))
            return false;

        model = typedModel as IDbResolvable;

        //Debug.Log($"Found Model {id}");
        return model != null;
    }

    bool IUntypedModelDatabase.TryAddModel(IDbResolvable model)
    {
        if (model is not TModel typedModel)
            return false;

        return TryAddModel(typedModel);
    }

    bool IUntypedModelDatabase.TryAddOrUpdateModel(IDbResolvable model)
    {
        if (model is not TModel typedModel)
            return false;

        return TryAddOrUpdateModel(typedModel);
    }

    bool IUntypedModelDatabase.TryRemoveModel(IDbId id)
    {
        if (id is not TId typedId)
            return false;

        return TryRemoveModel(typedId);
    }

    IEnumerable<IDbResolvable> IUntypedModelDatabase.GetAllModels()
    {
        return _models.Values.Cast<IDbResolvable>();
    }

    internal void Clear()
    {
        _models.Clear();
    }

    internal Dictionary<TId, TModel> GetRaw()
    {
        return _models;
    }
}
