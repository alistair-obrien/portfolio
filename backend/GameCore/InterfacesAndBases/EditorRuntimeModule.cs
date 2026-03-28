using System;

public abstract class EditorRuntimeModule : IRuntimeModule
{
    private bool _isInitialized;

    private CoordinatorBase _rootCoordinator;
    private IRenderer _rootRenderer;

    public void Initialize()
    {
        _rootCoordinator?.Initialize();
        _isInitialized = true;
    }

    public void Shutdown()
    {
        _isInitialized = false;
        _rootCoordinator?.Shutdown();
    }

    public void DoUpdate(float deltaTime) 
    {
        if (!_isInitialized) 
            return;
        
        _rootCoordinator?.DoUpdate(deltaTime);
        _rootRenderer?.Update(deltaTime);
    } 

    public void BindRootRenderer(IRenderer rootRenderer)
    {
        _rootRenderer = rootRenderer;
    }

    public void BindRootCoordinator(CoordinatorBase rootCoordinator)
    {
        _rootCoordinator = rootCoordinator;
        if (_isInitialized)
            _rootCoordinator.Initialize();
    }
}