using System.Collections.Generic;
using System.Linq;

public sealed record HeadlessEventEnvelope(
    string Type,
    string Description
);

public sealed record HeadlessCommandResponse(
    bool Ok,
    string ErrorMessage,
    IReadOnlyList<HeadlessEventEnvelope> Events,
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
        return new HeadlessCommandResponse(
            result.Ok,
            result.ErrorMessage,
            BuildEventEnvelopes(eventsBuffer),
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
}
