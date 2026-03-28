using System.Collections.Generic;

public sealed record OptionsPresentedToPlayerCharacter(
    string Title,
    IEnumerable<InteractionRequest> Options
) : IGameEvent;
