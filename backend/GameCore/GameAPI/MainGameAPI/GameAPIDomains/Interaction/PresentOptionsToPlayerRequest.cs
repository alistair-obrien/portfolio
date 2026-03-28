using System.Collections.Generic;


public sealed record DirectionalTargetingEntered() : IGameEvent;
public sealed record DirectionalTargetingExited() : IGameEvent;
public sealed record CellTargetingEntered() : IGameEvent;
public sealed record CellTargetingExited() : IGameEvent;
public sealed record PresentOptionsToPlayerRequest(string Title, IEnumerable<InteractionRequest> Options) : IGameCommand;
