using System.Collections.Generic;

public record InteractionOptionsPresentation(
    string Title,
    IReadOnlyList<InteractionOptionPresentation> Options
)
{
    public InteractionOptionsPresentation(IEnumerable<InteractionRequest> interactionRequests) : this("", new List<InteractionOptionPresentation>())
    {
        var options = new List<InteractionOptionPresentation>();

        foreach (var availableAction in interactionRequests)
        {
            options.Add(new InteractionOptionPresentation(
                availableAction.OptionKey,
                availableAction.Name));
        }

        Options = options;
        Title = "Interact";
    }

    public override string ToString()
    {
        return $"{Title}: [{string.Join(", ", Options)}]";
    }
}
