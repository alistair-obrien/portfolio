namespace RoguelikeWidget.Services;

public sealed class WidgetSession
{
    private readonly HeadlessGameSession _session = new();

    public RootGameModelPresentation State { get; private set; } = null!;
    public string? LastMessage { get; private set; }

    public WidgetSession()
    {
        Reset();
    }

    public void Reset()
    {
        State = _session.Reset(seedDevelopmentWorld: true);
        LastMessage = null;
    }

    public void MovePlayerToCell(MapChunkId mapId, int x, int y)
    {
        var response = _session.MovePlayerToCell(mapId, x, y);
        State = response.State;
        LastMessage = response.Ok
            ? $"Moved to ({x}, {y})."
            : response.ErrorMessage;
    }
}
