using System.Collections.Generic;

public sealed record TurnOutcome(IReadOnlyList<IGameEvent> Events);

public class TurnsAPI : APIDomain
{
    public TurnsAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    internal override void RegisterHandlers(CommandRouter router)
    {

    }

    public bool TryAddCharacterToTurnQueue(CharacterId characterId)
    {
        if (!TryResolve(characterId, out Character character)) { return false; }
        if (!Rulebook.TurnsSection.CanAddCharacterToTurnQueue(character)) { return false; }
        return RootModel.TurnData.TryAddCharacterToTurnQueue(characterId);
    }

    internal bool TryEnqueueActionRequestForCharacter(CharacterId characterId, IGameCommand request)
    {
        return RootModel.TurnData.TryAddActionToCharactersTurnQueue(characterId, request);
    }

    public bool TryProcessCurrentTurn(out TurnOutcome outcome)
    {
        var events = new List<IGameEvent>();

        // Execute all queued actions for the current character
        while (RootModel.TurnData.TryGetNextActionRequestForCurrentTurn(out var request))
        {
            if (!GameAPI.TryExecuteCommand(request, out var actionEvents).Ok)
                continue;

            events.AddRange(actionEvents);
        }

        if (events.Count == 0)
        {
            outcome = default;
            return false;
        }

        outcome = new TurnOutcome(events);
        return true;
    }

    internal bool TryGetCurrentCharactersTurn(out CharacterId currentTurnCharacterUid)
    {
        return RootModel.TurnData.TryGetCharacterForTurn(out currentTurnCharacterUid);
    }
}