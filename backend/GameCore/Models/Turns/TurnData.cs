using System.Collections.Generic;

public sealed class TurnData : BaseEntity
{
    // Every character, including the player, processes their actions one turn at a time
    internal Queue<CharacterId> TurnOrder = new Queue<CharacterId>();
    internal Dictionary<CharacterId, Queue<IGameCommand>> TurnActionRequests = new ();

    public TurnData()
    {
        TurnOrder = new Queue<CharacterId>();
        TurnActionRequests = new ();
    }

    public TurnData(TurnData turnData)
    {
        TurnOrder = new Queue<CharacterId>(turnData.TurnOrder);

        TurnActionRequests = new Dictionary<CharacterId, Queue<IGameCommand>>();

        foreach (var kvp in turnData.TurnActionRequests)
        {
            // Clone each queue
            TurnActionRequests[kvp.Key] = new Queue<IGameCommand>(kvp.Value);
        }
    }

    internal bool TryGetCharacterForTurn(out CharacterId characterUid)
    {
        return TurnOrder.TryPeek(out characterUid);
    }

    internal bool TryAddCharacterToTurnQueue(CharacterId characterId)
    {
        if (!characterId.IsValid)
            throw new System.Exception();

        if (TurnOrder.Contains(characterId)) 
        { 
            Debug.LogWarning($"{characterId} already exists in turn queue."); 
            return false; 
        }
        TurnOrder.Enqueue(characterId);
        if (!TurnActionRequests.ContainsKey(characterId))
        {
            TurnActionRequests.TryAdd(characterId, new Queue<IGameCommand>());
        }

        return true;
    }

    internal bool TryAddActionToCharactersTurnQueue(CharacterId characterUid, IGameCommand request)
    {
        if (!TurnOrder.Contains(characterUid)) { return false; }
        TurnActionRequests[characterUid].Enqueue(request);

        //Debug.Log($"Queued {request} for {characterUid}");

        return true;
    }

    internal bool TryGetNextActionRequestForCurrentTurn(out IGameCommand request)
    {
        request = null;

        if (!TurnOrder.TryPeek(out var currentTurnCharacter))
        {
            Debug.LogWarning("There are no characters left in the queue");
            return false;
        }

        if (!TurnActionRequests.ContainsKey(currentTurnCharacter))
            throw new System.Exception();

        if (!TurnActionRequests[currentTurnCharacter].TryDequeue(out request))
        {
            return false;
        }

        // We found a request at the top
        //Debug.Log($"Dequeued {request} for {currentTurnCharacter}");

        return true;
    }
}