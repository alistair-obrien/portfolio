using System;

public class MainGameModule : EditorRuntimeModule
{
    private readonly MainGameCoordinator _mainGameCoordinator; // We only have one Main Coordinator
   
    public MainGameModule(GameInstance gameInstance = null)
    {
        Debug.Log("[MainGameModule] Launching Main game Module");
        if (gameInstance == null)
        {
            Debug.Log("[MainGameModule] No Game Instance Provided. Creating new Game Instance");
            var rootModel = new Root();
            gameInstance = new GameInstance(rootModel, null, null);
        }

        Debug.Log("[MainGameModule] Creating Main game Coordinator");
        _mainGameCoordinator = new MainGameCoordinator(gameInstance);
        Debug.Log("[MainGameModule] Binding Main game Coordinator");
        BindRootCoordinator(_mainGameCoordinator);
    }

    public new void BindRootRenderer(IRenderer rootRenderer)
    {
        base.BindRootRenderer(rootRenderer);

        if (rootRenderer is IMainGameRenderer mainGameRenderer)
            _mainGameCoordinator.BindRenderer(mainGameRenderer);
    }

    public void SubmitCommand(IGameCommand gameCommand)
    {
        _mainGameCoordinator.TryExecuteCommand(gameCommand);
    }
}
