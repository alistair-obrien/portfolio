using System.Collections.Generic;
using System.Linq;

public sealed record HeadlessEventEnvelope(
    string Type,
    string Description
);

public sealed record HeadlessPathStepEnvelope(
    int FromX,
    int FromY,
    int ToX,
    int ToY
);

public sealed record HeadlessCommandResponse(
    bool Ok,
    string ErrorMessage,
    IReadOnlyList<HeadlessEventEnvelope> Events,
    IReadOnlyList<HeadlessPathStepEnvelope> Path,
    RootGameModelPresentation State
);

public sealed class HeadlessGameSession
{
    private readonly GameInstance _gameInstance;

    public HeadlessGameSession()
    {
        _gameInstance = new GameInstance(new Root(), null, null);
    }

    public RootGameModelPresentation GetState()
    {
        return _gameInstance.PullRootGameModelPresentation();
    }

    public HeadlessCommandResponse Execute(IGameCommand command)
    {
        var result = _gameInstance.TryExecuteCommand(command, out var eventsBuffer);
        var path = BuildPathEnvelopes(eventsBuffer);
        return new HeadlessCommandResponse(
            result.Ok,
            result.ErrorMessage,
            BuildEventEnvelopes(eventsBuffer),
            path,
            _gameInstance.PullRootGameModelPresentation());
    }

    public HeadlessCommandResponse Preview(IGameCommand command)
    {
        var result = _gameInstance.TryPreviewCommand(command, out var eventsBuffer);
        var path = BuildPathEnvelopes(eventsBuffer);
        return new HeadlessCommandResponse(
            result.Ok,
            result.ErrorMessage,
            BuildEventEnvelopes(eventsBuffer),
            path,
            _gameInstance.PullRootGameModelPresentation());
    }

    public RootGameModelPresentation Reset(bool seedDevelopmentWorld = true)
    {
        _gameInstance.SetGameModel(new Root());

        if (seedDevelopmentWorld)
            SeedDevelopmentWorld();

        return _gameInstance.PullRootGameModelPresentation();
    }

    public RootGameModelPresentation SeedDevelopmentWorld()
    {
        var character = new CharacterBlueprint
        {
            Id = Root.SYSTEM_ID,
            Name = "Orion Styrogis",
        };

        character.OperatingSystem.Id = OperatingSystemIds.GenoSys;
        character.OperatingSystem.Modules.Add(OperatingSystemModuleBlueprint.CreateBuilding());
        character.OperatingSystem.Modules.Add(OperatingSystemModuleBlueprint.CreateAuthoring());

        Execute(new DatabaseAPI.Commands.CreateOrUpdateModel(character));
        Execute(new CharactersAPI.Commands.AssignPlayerCharacter(Root.SYSTEM_ID));

        var testMapId = MapChunkId.New();
        Execute(new MapsAPI.Commands.CreateMap(Root.SYSTEM_ID, testMapId, 48, 48));

        var locationTarget = new MapLocation(testMapId, new CellFootprint(5, 5, 1, 1));
        Execute(new ItemsAPI.Commands.MoveEntity(Root.SYSTEM_ID, Root.SYSTEM_ID, locationTarget, false));

        return _gameInstance.PullRootGameModelPresentation();
    }

    public HeadlessCommandResponse MovePlayerToCell(MapChunkId mapId, int x, int y)
    {
        var state = _gameInstance.PullRootGameModelPresentation();
        var playerId = state.PlayerHUD?.characterUid;

        if (!playerId.HasValue)
        {
            return new HeadlessCommandResponse(
                false,
                "No player character is assigned in this session.",
                new List<HeadlessEventEnvelope>(),
                new List<HeadlessPathStepEnvelope>(),
                state);
        }

        return Execute(new MapsAPI.Commands.MoveCharacterAlongPathToCell(
            playerId.Value,
            mapId,
            playerId.Value,
            x,
            y));
    }

    public HeadlessCommandResponse PreviewPlayerMoveToCell(MapChunkId mapId, int x, int y)
    {
        var state = _gameInstance.PullRootGameModelPresentation();
        var playerId = state.PlayerHUD?.characterUid;

        if (!playerId.HasValue)
        {
            return new HeadlessCommandResponse(
                false,
                "No player character is assigned in this session.",
                new List<HeadlessEventEnvelope>(),
                new List<HeadlessPathStepEnvelope>(),
                state);
        }

        return Preview(new MapsAPI.Commands.MoveCharacterAlongPathToCell(
            playerId.Value,
            mapId,
            playerId.Value,
            x,
            y));
    }

    private static IReadOnlyList<HeadlessEventEnvelope> BuildEventEnvelopes(IEnumerable<IGameEvent> eventsBuffer)
    {
        if (eventsBuffer == null)
            return new List<HeadlessEventEnvelope>();

        return eventsBuffer
            .Select(evt => new HeadlessEventEnvelope(
                evt.GetType().Name,
                evt.ToString() ?? evt.GetType().Name))
            .ToList();
    }

    private static IReadOnlyList<HeadlessPathStepEnvelope> BuildPathEnvelopes(IEnumerable<IGameEvent> eventsBuffer)
    {
        if (eventsBuffer == null)
            return new List<HeadlessPathStepEnvelope>();

        return eventsBuffer
            .OfType<MapsAPI.Events.CharacterMovedOnMap>()
            .Select(evt => new HeadlessPathStepEnvelope(
                evt.MoveResult.From.X,
                evt.MoveResult.From.Y,
                evt.MoveResult.To.X,
                evt.MoveResult.To.Y))
            .ToList();
    }
}
