using System.Collections.Generic;

public interface IModelDatabase<TModel,TId> 
    where TModel : IDbResolvable
    where TId : IDbId
{
    IEnumerable<TModel> GetAllModels();
    bool TryAddModel(TModel model);
    bool TryGetModel(TId uid, out TModel model);
    bool TryRemoveModel(TId uid);
}
