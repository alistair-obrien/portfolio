using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IRenderer
{
    void Initialize();
    void Update(float deltaTime);
    void Shutdown();
    // If we want children to not be coordinated by parents in the view tree we need to reintroduce this
    //void BindRendererRouters(RendererRouter rendererRouter, RendererRouter previewRendererRouter);
    Task RenderPreviewEvents(IEnumerable<IGameEvent> gameEvents, bool animate);
    void ClearAllPreviews();
    Task RenderEvents(IEnumerable<IGameEvent> gameEvents, bool animate);

    void AddSubRenderer(IRenderer subRenderer);
    void RemoveSubRenderer(IRenderer subRenderer);
}

public interface IRenderer<T> : IRenderer where T : class
{
    void Sync(T presentation);
}

public interface ICoordinatorBinding<in TQueries, in TCommands>
{
    void Bind(TQueries queries, TCommands commands);
    void Unbind();
}

public interface ICoordinatorQueries 
{ 

}

public interface ICoordinatorCommands 
{
}

public abstract class CoordinatorBase
{
    private bool _initialized;

    private readonly List<CoordinatorBase> _subCoordinators = new();

    protected readonly GameInstance _gameInstance;

    private CoordinatorBase _parentCoordinator;

    protected CoordinatorBase(GameInstance gameInstance)
    {
        _gameInstance = gameInstance;
    }

    protected readonly List<IRenderer> UntypedRenderers = new();

    public void Initialize()
    {
        //Assert.IsFalse(_initialized);
        if (_initialized)
        {
            Debug.LogWarning("Was Already Initialized");
            return;
        }
        
        //foreach (var subCoordinator in _subCoordinators)
        //{
        //    subCoordinator.Initialize();
        //}

        OnInitialize();
        _initialized = true;
    }
    protected virtual void OnInitialize() { }

    public void DoUpdate(float deltaTime)
    {
        if (!_initialized) { return; }

        foreach (var subCoordinator in _subCoordinators)
        {
            subCoordinator.DoUpdate(deltaTime);
        }

        OnUpdate(deltaTime);
    }
    protected virtual void OnUpdate(float deltaTime) { }

    public void Shutdown()
    {
        if (!_initialized)
        {
            Debug.LogWarning("Trying to shutdown when not Initialized");
            return;
        }

        foreach (var subCoordinator in _subCoordinators)
        {
            subCoordinator.Shutdown();
        }
        _initialized = false;

        OnShutdown();
    }
    protected virtual void OnShutdown() { }

    internal void HandleGameEvent(IGameEvent evt) 
    { 
        DoHandleGameEvent(evt);
        ForEachSubCoordinator(sub => sub.HandleGameEvent(evt));
    }

    protected virtual void DoHandleGameEvent(IGameEvent evt) { }

    // Intent flow from bottom to top
    public void SubmitIntent(Intents.IIntent intent) 
    {
        HandleIntent(intent);

        if (_parentCoordinator != null) { _parentCoordinator.SubmitIntent(intent); }
    }

    protected virtual void HandleIntent(Intents.IIntent intent) { }

    protected void AddSubCoordinator(CoordinatorBase subCoordinator)
    {
        subCoordinator.Initialize();
        _subCoordinators.Add(subCoordinator);
        subCoordinator.SetParentCoordinator(this);
    }

    private void SetParentCoordinator(CoordinatorBase coordinatorBase)
    {
        _parentCoordinator = coordinatorBase;
    }

    protected void RemoveSubCoordinator(CoordinatorBase subCoordinator)
    {
        subCoordinator.Shutdown();
        _subCoordinators.Remove(subCoordinator);
        subCoordinator.SetParentCoordinator(null);
    }

    protected void ForEachSubCoordinator(Action<CoordinatorBase> action)
    {
        var subcoordinatorsSnapshot = _subCoordinators.ToArray();
        foreach (var subcoordinator in subcoordinatorsSnapshot)
        {
            action(subcoordinator);
        }
    }

    protected CommandResult ExecuteTracked(IGameCommand command)
    {
        return ExecuteTracked(new List<IGameCommand> { command });
    }

    protected CommandResult ExecuteTracked(IEnumerable<IGameCommand> commands)
    {
        if (_parentCoordinator != null)
        {
            return _parentCoordinator.ExecuteTracked(commands);
        }

        var result = _gameInstance.TryExecuteCommandsTracked(commands, out var events);

        foreach (var evt in events)
        {
            HandleGameEvent(evt);
        }
        ForEachRenderer(r => r.RenderEvents(events, false));

        return result;
    }

    protected CommandResult ExecuteCommand(IGameCommand command)
    {
        return ExecuteCommands(new List<IGameCommand> { command });
    }

    protected CommandResult ExecuteCommands(IEnumerable<IGameCommand> commands)
    {
        // Execution should always be executed from the root so that events are handled from top down
        if (_parentCoordinator != null)
        {
            return _parentCoordinator.ExecuteCommands(commands);
        }

        var result = _gameInstance.TryExecuteCommands(commands, out var events);

        foreach (var evt in events)
        {
            HandleGameEvent(evt);
        }
        ForEachRenderer(r => r.RenderEvents(events, false));

        return result;
    }

    protected CommandResult ExecutePreviewCommands(IEnumerable<IGameCommand> commands)
    {
        // Execution should always be executed from the root so that events are handled from top down
        if (_parentCoordinator != null)
        {
            return _parentCoordinator.ExecutePreviewCommands(commands);
        }

        var result = _gameInstance.TryPreviewCommands(commands, out var events);

        UntypedRenderers.ForEach(r => r.ClearAllPreviews());
        UntypedRenderers.ForEach(r =>
        {
            r.RenderPreviewEvents(events, animate: false);
        });

        return result;
    }

    protected CommandResult ExecutePreviewCommand(IGameCommand command)
    {
        // Execution should always be executed from the root so that events are handled from top down
        if (_parentCoordinator != null)
        {
            return _parentCoordinator.ExecutePreviewCommand(command);
        }

        var result = _gameInstance.TryPreviewCommand(command, out var events);

        UntypedRenderers.ForEach(r => r.ClearAllPreviews());
        UntypedRenderers.ForEach(r =>
        {
            r.RenderPreviewEvents(events, animate: false);
        });

        return result;
    }

    protected void ForEachRenderer(Action<IRenderer> action)
    {
        var renderersSnapshot = UntypedRenderers.ToArray();
        foreach (var renderer in renderersSnapshot)
        {
            action(renderer);
        }
    }

    protected async Task ForEachRendererAsync(Func<IRenderer, Task> action)
    {
        var renderersSnapshot = UntypedRenderers.ToArray();
        foreach (var renderer in renderersSnapshot)
        {
            await action(renderer);
        }
    }
}

public abstract class CoordinatorBase<
    TQueries,
    TCommands,
    TRenderer> : CoordinatorBase
    where TQueries : ICoordinatorQueries
    where TCommands : ICoordinatorCommands
    where TRenderer : IRenderer
{
    public abstract TQueries QueriesHandler { get; }
    public abstract TCommands CommandsHandler { get; }

    protected readonly List<TRenderer> Renderers = new();

    protected CoordinatorBase(GameInstance gameAPI) : base(gameAPI)
    {
    }

    public void BindRenderer(TRenderer renderer)
    {
        Renderers.Add(renderer);
        UntypedRenderers.Add(renderer);
        renderer.Initialize();

        if (renderer is ICoordinatorBinding<TQueries, TCommands> binding)
        {
            binding.Bind(QueriesHandler, CommandsHandler);
        }

        OnRendererBound(renderer);
    }
    protected virtual void OnRendererBound(TRenderer renderer) { }

    public void UnbindRenderer(TRenderer renderer)
    {
        if (renderer is ICoordinatorBinding<TQueries, TCommands> binding)
        {
            binding.Unbind();
        }

        renderer.Shutdown();
        Renderers.Remove(renderer);
        UntypedRenderers.Remove(renderer);

        OnRendererUnbound(renderer);
    }

    protected virtual void OnRendererUnbound(TRenderer renderer) { }

    protected void ForEachRenderer(Action<TRenderer> action)
    {
        var renderersSnapshot = Renderers.ToArray();
        foreach (var renderer in renderersSnapshot)
        {
            action(renderer);
        }
    }

    protected async Task ForEachRendererAsync(Func<TRenderer, Task> action)
    {
        var renderersSnapshot = Renderers.ToArray();
        foreach (var renderer in renderersSnapshot)
        {
            await action(renderer);
        }
    }
}
