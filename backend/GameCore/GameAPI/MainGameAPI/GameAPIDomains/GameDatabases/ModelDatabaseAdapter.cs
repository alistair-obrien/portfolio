using System.Collections.Generic;

public sealed class ModelDatabaseAdapter<TModel, TId>
    : IModelDatabaseUntyped
    where TModel : IGameDbResolvable
    where TId : IGameDbId
{
    private readonly IModelDatabase<TModel, TId> _database;

    public ModelDatabaseAdapter(IModelDatabase<TModel, TId> database)
    {
        _database = database;
    }

    public bool TryGet(IGameDbId id, out IGameDbResolvable model)
    {
        model = default;

        if (id is not TId typedId)
            return false;

        if (!_database.TryGetModel(typedId, out var typedModel))
            return false;

        model = typedModel;
        return true;
    }

    public bool TryAdd(IGameDbResolvable model)
    {
        if (model is not TModel typedModel)
            return false;

        return _database.TryAddModel(typedModel);
    }

    public bool TryRemove(IGameDbId id)
    {
        if (id is not TId typedId)
            return false;

        return _database.TryRemoveModel(typedId);
    }
}