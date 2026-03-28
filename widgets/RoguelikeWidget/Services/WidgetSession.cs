namespace RoguelikeWidget.Services;

public sealed class WidgetSession
{
    private readonly HeadlessGameSession _session = new();

    public RootGameModelPresentation State { get; private set; } = null!;

    public WidgetSession()
    {
        Reset();
    }

    public void Reset()
    {
        State = _session.Reset(seedDevelopmentWorld: true);
    }
}
