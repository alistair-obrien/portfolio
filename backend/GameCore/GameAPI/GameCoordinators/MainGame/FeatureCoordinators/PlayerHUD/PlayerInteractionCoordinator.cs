public interface IPlayerInteractionQueries : ICoordinatorQueries { }
public interface IPlayerInteractionCommands : ICoordinatorCommands { }

public interface IPlayerInteractionRenderer : IRenderer<PlayerInteractionPresentation>
{
    IHealthRenderer HealthRenderer { get; }
    ILoadoutRenderer LoadoutRenderer { get; }
}

public class PlayerInteractionCoordinator : CoordinatorBase<
    IPlayerInteractionQueries, 
    IPlayerInteractionCommands, 
    IPlayerInteractionRenderer>,
    IPlayerInteractionQueries,
    IPlayerInteractionCommands
{
    public string _boundCharacterUid;
    private PlayerInteractionPresentation _presentation;
    public readonly CharacterLoadoutCoordinator CharacterLoadoutCoordinator;

    public override IPlayerInteractionQueries QueriesHandler => this;
    public override IPlayerInteractionCommands CommandsHandler => this;

    public PlayerInteractionCoordinator(GameInstance gameAPI) : base(gameAPI)
    {
        AddSubCoordinator(CharacterLoadoutCoordinator = new CharacterLoadoutCoordinator(gameAPI));
    }

    protected override void OnRendererBound(IPlayerInteractionRenderer renderer)
    {
        if (_presentation == null)
            return;

        renderer.Sync(_presentation);

        CharacterLoadoutCoordinator.BindRenderer(renderer.LoadoutRenderer); // Hack?
    }

    protected override void DoHandleGameEvent(IGameEvent evt)
    {
        switch (evt)
        {
            case CharactersAPI.Events.PlayerCharacterAssigned assigned:
                _presentation = assigned.PlayerInteractionPresentation;
                CharacterLoadoutCoordinator.BindCharacter(assigned.NewCharacterId);
                break;
        }
    }
}