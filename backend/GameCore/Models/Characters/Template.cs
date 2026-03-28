using System.Collections.Generic;
using System.Linq;

public record TemplateSaveData(ITemplateDbId TemplateId, List<SavePacket> EntityDefinitions);

public sealed class Template : Entity<TemplateSaveData>, IDbResolvable
{
    protected override string TypeId => "template";
    protected override int Version => 1;

    IDbId IDbResolvable.Id => Id;
    public ITemplateDbId Id { get; set; }

    // This should not be live models
    // It should be Saved Data (ie construction blueprint)
    public List<IBlueprint> EntityBlueprints { get; }
    public IBlueprint EntityBlueprintRoot => EntityBlueprints.FirstOrDefault();

    public override TemplateSaveData SaveToBlueprint()
    {
        //var prototypes = PrototypeGraph.ConvertAllToList(x => new SavePacket(x));
        return new TemplateSaveData(Id, EntityBlueprints.ConvertAll(x => new SavePacket(x)));
    }

    public Template(ITemplateDbId id, List<IBlueprint> prototypeGraph)
    {
        Id = id;
        EntityBlueprints = prototypeGraph;
    }

    public Template(TemplateSaveData saveData)
    {
        Id = saveData.TemplateId;
        EntityBlueprints = saveData.EntityDefinitions.ConvertAll(packet => (IBlueprint)SaveLoaderRegistry.LoadUntyped(packet)); // (ISaveData) is hack
    }
}