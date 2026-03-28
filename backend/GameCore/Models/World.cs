using System;
using System.Collections.Generic;

public class WorldBlueprint : IBlueprint
{
    IGameDbId IBlueprint.Id => Id;
    public string TypeId => "World";

    [HideInEditor] public IGameDbId Id;

    public string Name { get; set; }

    internal List<MapChunkId> Maps { get; set; } = new();

    public WorldBlueprint()
    {
        Id = ItemId.New(); // HACK
    }

    public WorldBlueprint(World world)
    {
        // Identity
        Id = world.Id;
    }
}

public sealed class World : Entity<WorldBlueprint>,
    IGameDbResolvable
{
    IDbId IDbResolvable.Id => Id;
    IGameDbId IGameDbResolvable.Id => Id;

    public IGameDbId Id { get; private set; }

    public string Name { get; private set; } = "World";
    protected override int Version => 1;
    protected override string TypeId => "World";

    public IGameModelLocation AttachedLocation => null;

    private List<MapChunkId> _maps = new();

    public World()
    {
        Id = ItemId.New(); // HACK
    }

    public override WorldBlueprint SaveToBlueprint()
    {
        WorldBlueprint blueprint = new();
        blueprint.Maps = new List<MapChunkId>(_maps);
        return blueprint;
    }

    // NOTE: I think remap happens after attach. So dont forget to implement remap
    internal void AttachEntities(WorldBlueprint saveData, Dictionary<IGameDbId, IGameDbResolvable> modelsLookup)
    {
        foreach (var map in saveData.Maps)
        {
            // In this case we don't need the actual model
            TryAttachMap(map);
        }
    }

    internal bool TryAttachMap(MapChunkId mapId)
    {
        if (!mapId.IsValid)
        {
            Debug.LogError($"Maps: Trying to Load from an invalid mapId");
            return false;
        }

        if (_maps.Contains(mapId))
        {
            Debug.LogError($"Maps: [{mapId}] already attached.");
            return false; 
        }

        _maps.Add(mapId);

        return true;
    }

    internal bool TryDetachMap(MapChunkId mapId)
    {
        if (!mapId.IsValid)
        {
            Debug.LogError($"Maps: Trying to Unload from an invalid mapId");
            return false;
        }

        return _maps.Remove(mapId);
    }

    // Simulation hook
    //private void IncrementTick()
    //{
    //    foreach (var map in GameDatabases.MapsDb.GetAllModels())
    //    {
                
    //    }

    //    foreach (var item in GameDatabases.ItemsDb.GetAllModels())
    //    {

    //    }

    //    foreach (var character in GameDatabases.CharactersDb.GetAllModels())
    //    {

    //    }
    //}

    //public void TickWorld()
    //{
    //    //foreach (var character in GetAllCharacters())
    //    //{
    //    //    character.OnNewTickStarted();
    //    //}
    //}

    //public void AddToGameLog<TPayload>(TPayload value) where TPayload : IModelMutationResult
    //{
    //    _saveGame.AddToGameLog(value);
    //}

    //public IEnumerable<MapChunkId> GetAllLoadedMaps() => LoadedMaps; // HACK

    //private IEnumerable<CharacterId> GetAllCharacters()
    //{
    //    foreach (var map in GetAllLoadedMaps())
    //    {
    //        foreach (var characterPlacement in map.GetAllCharacterPlacements())
    //        {
    //            yield return characterPlacement.CharacterId;
    //        }
    //    }
    //}

    public void ClearAttachedLocation()
    {
        throw new System.NotImplementedException();
    }

    public void SetAttachedLocation(IGameModelLocation targetLocation)
    {
        throw new System.NotImplementedException();
    }

    public void ReattachReferences(IGameDbResolvable originalModel)
    {
        if (originalModel is not World world)
            throw new InvalidOperationException("Expected World model");

        _maps = new List<MapChunkId>(world._maps);
    }

    public IEnumerable<IGameDbId> GetAttachedEntityIds()
    {
        foreach (var id in _maps)
            yield return id;
    }

    public IEnumerable<IGameDbId> GetReferencedEntityIds()
    {
        yield break;
    }

    public void AttachEntities(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        //TODO
    }

    public void ApplyBlueprint(IBlueprint blueprint)
    {
        Id = blueprint.Id;
        Name = blueprint.Name;
    }

    public IEnumerable<AttachmentChange> GetAttachmentChanges(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        yield break;
    }
}