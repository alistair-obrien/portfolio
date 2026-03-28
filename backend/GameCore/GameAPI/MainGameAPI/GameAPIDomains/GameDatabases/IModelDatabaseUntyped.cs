using System.Collections.Generic;

public interface IModelDatabaseUntyped
{
    bool TryGet(IGameDbId id, out IGameDbResolvable model);
    bool TryAdd(IGameDbResolvable model);
    bool TryRemove(IGameDbId id);
}