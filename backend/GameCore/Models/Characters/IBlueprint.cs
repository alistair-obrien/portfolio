public interface IBlueprint 
{
    public IGameDbId Id { get; }
    string TypeId { get; }
    string Name { get; set; }
}
