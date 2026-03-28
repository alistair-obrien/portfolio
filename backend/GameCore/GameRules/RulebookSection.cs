internal class RulebookSection
{
    protected readonly Rulebook Rulebook;
    public RulebookSection(Rulebook rulebook)
    {
        Rulebook = rulebook;
    }

    protected bool TryResolve<TModel, TId>(TId id, out TModel model)
        where TModel : IGameDbResolvable
        where TId : IGameDbId
    {
        return Rulebook.GameAPI.Databases.TryGetModel(id, out model);
    }
}