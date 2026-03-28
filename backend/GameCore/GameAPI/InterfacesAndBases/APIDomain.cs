public abstract class APIDomain
{
    protected readonly GameInstance GameAPI;
    protected TemplatesGameModel TemplatesDatabase => GameAPI.TemplatesGameModel;
    protected Root RootModel => GameAPI.RootModel;
    protected Rulebook Rulebook => GameAPI.Rulebook;

    internal APIDomain(GameInstance gameAPI)
    {
        GameAPI = gameAPI;
    }

    internal virtual void RegisterHandlers(CommandRouter router) { }

    internal bool TryResolveUntyped(IGameDbId id, out IGameDbResolvable model)
    {
        return GameAPI.Databases.TryGetModelUntyped(id, out model);
    }

    internal bool TryResolve<TModel, TId>(TId id, out TModel model)
        where TModel : IGameDbResolvable
        where TId : struct, IGameDbId
    {
        return GameAPI.Databases.TryGetModel(id, out model);
    }

    protected void RaiseEvent(IGameEvent evt)
    {
        GameAPI.RaiseEvent(evt);
    }

    protected static CommandResult Ok()
    {
        return CommandResult.Success();
    }

    protected static CommandResult Fail(string message)
    {
        return CommandResult.Fail(message);
    }
}
