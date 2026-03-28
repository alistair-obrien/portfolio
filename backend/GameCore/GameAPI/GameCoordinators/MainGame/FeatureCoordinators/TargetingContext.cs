using System;
using System.Collections.Generic;

public sealed class TargetingContext
{
    public List<IGameModelLocation> LocationTargets { get; } = new();
    public List<Vec2> DirectionTargets { get; } = new();

    public void Clear()
    {
        LocationTargets.Clear();
        DirectionTargets.Clear();
    }
}

public interface ITargetingSession
{
    void Enter();
    void Exit();
    bool HandleIntent(Intents.IIntent intent);

    bool IsComplete { get; }
}

public interface ITargetingListener
{
    void OnLocationHovered(IGameModelLocation location);
    void OnLocationHoverCleared();

    void OnLocationCommitted(IGameModelLocation location);

    void OnDirectionUpdated(Vec2 direction);
    void OnDirectionCommitted(Vec2 direction);

    void OnTargetingCancelled();
}

public class TargetingListener : ITargetingListener
{
    public event Action<IGameModelLocation> onLocationCommitted;
    public event Action onLocationHoverCleared;
    public event Action<IGameModelLocation> onLocationHovered;
    public event Action<Vec2> onDirectionCommitted;
    public event Action<Vec2> onDirectionUpdated;
    public event Action onTargetingCanceled;

    public void OnLocationCommitted(IGameModelLocation location)
    {
        onLocationCommitted?.Invoke(location);
    }

    public void OnLocationHoverCleared()
    {
        onLocationHoverCleared?.Invoke();
    }

    public void OnLocationHovered(IGameModelLocation location)
    {
        onLocationHovered?.Invoke(location);
    }

    public void OnDirectionCommitted(Vec2 direction)
    {
        onDirectionCommitted?.Invoke(direction);
    }

    public void OnDirectionUpdated(Vec2 direction)
    {
        onDirectionUpdated?.Invoke(direction);
    }

    public void OnTargetingCancelled()
    {
        onTargetingCanceled?.Invoke();
    }
}

public sealed class DirectionalTargetingSession : ITargetingSession
{
    private readonly ITargetingListener _listener;
    private int _committed;

    public bool IsComplete => _committed >= 1;

    public DirectionalTargetingSession(ITargetingListener listener)
    {
        _listener = listener;
    }

    public void Enter() { }

    public void Exit() { }

    public bool HandleIntent(Intents.IIntent intent)
    {
        switch (intent)
        {
            case Intents.DirectionUpdatedIntent d:
                _listener.OnDirectionUpdated(d.Direction);
                return true;

            case Intents.DirectionPrimaryCommitWorldIntent d:
                _listener.OnDirectionCommitted(d.Direction);
                _committed++;
                return true;

            case Intents.AttackTargetingCancelIntent:
                _listener.OnTargetingCancelled();
                return true;
        }

        return false;
    }
}

public sealed class LocationTargetingSession : ITargetingSession
{
    private readonly ITargetingListener _listener;
    private int _committed;

    public bool IsComplete => _committed >= 1;

    public LocationTargetingSession(ITargetingListener listener)
    {
        _listener = listener;
    }

    public void Enter() { }

    public void Exit() { }

    public bool HandleIntent(Intents.IIntent intent)
    {
        switch (intent)
        {
            case Intents.GameEntityLocationHoverEnterIntent hover:
                _listener.OnLocationHovered(hover.TargetEntityLocation);
                return true;

            case Intents.GameEntityLocationHoverExitIntent hoverExit:
                _listener.OnLocationHoverCleared();
                return true;

            case Intents.AttackTargetingCancelIntent:
                _listener.OnTargetingCancelled();
                _committed--;
                return true;

            case Intents.GameEntityLocationPrimaryCommit commit:
                _listener.OnLocationCommitted(commit.TargetEntityLocation);
                _committed++;
                return true;
        }

        return false;
    }
}