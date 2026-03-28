using Newtonsoft.Json;
using System.Collections.Generic;

public class Faction : BaseEntity, IGameDbResolvable
{
    IGameDbId IGameDbResolvable.Id => Id;
    IDbId IDbResolvable.Id => Id;
    IGameModelLocation IGameDbResolvable.AttachedLocation => new RootLocation(); // Later maybe maps need to be attached somewhere

    public FactionId Id { get; }

    public string Name { get; private set; }
    private string Description;

    [JsonConstructor] public Faction() { }
    public Faction(
        FactionId id, 
        string name, 
        string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    void IGameDbResolvable.ClearAttachedLocation()
    {
    }

    void IGameDbResolvable.SetAttachedLocation(IGameModelLocation targetLocation)
    {
    }

    public void ReattachReferences(IGameDbResolvable originalModel)
    {
        throw new System.NotImplementedException();
    }

    public IEnumerable<IGameDbId> GetAttachedEntityIds()
    {
        throw new System.NotImplementedException();
    }

    public IEnumerable<IGameDbId> GetReferencedEntityIds()
    {
        throw new System.NotImplementedException();
    }

    public void AttachEntities(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        //TODO
    }

    public void ApplyBlueprint(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        //TODO
    }

    public void ApplyBlueprint(IBlueprint blueprint)
    {
        throw new System.NotImplementedException();
    }

    public IEnumerable<AttachmentChange> GetAttachmentChanges(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        throw new System.NotImplementedException();
    }
}