using System.Collections.Generic;

public sealed record TurnResult
{
    public bool Success { get; init; }
    public string Error { get; init; }

    public IReadOnlyList<IGameEvent> Events { get; init; }
    public IReadOnlyList<TurnMutation> Mutations { get; init; } // optional internal
}