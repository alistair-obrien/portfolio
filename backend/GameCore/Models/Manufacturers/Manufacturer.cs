using Newtonsoft.Json;
using System.Collections.Generic;


public class Manufacturer : BaseEntity, IGameDbResolvable
{
    IGameDbId IGameDbResolvable.Id => Id;
    IDbId IDbResolvable.Id => Id;
    IGameModelLocation IGameDbResolvable.AttachedLocation => new RootLocation(); // Later maybe maps need to be attached somewhere

    public ManufacturerId Id { get; }
    public string Name { get; private set; }
    public string Description;
    
    public Manufacturer() { }
    public Manufacturer(
        ManufacturerId id, 
        string name, 
        string description
        ) 
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
        yield break;
    }

    public IEnumerable<IGameDbId> GetReferencedEntityIds()
    {
        yield break;
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
