using System.Collections.Generic;

public sealed record InteractionOptions(string Title, IReadOnlyList<InteractionRequest> Options);
