using System;
using System.Collections.Generic;

public class RootBlueprint
{
    // Save all Database models into a flat list of blueprints
    public List<SavePacket> DatabaseAsSavePackets;
    public List<IGameEvent> Log;
    public List<IGameDbId> Worlds;
    public TurnData TurnData;
    public CharacterId? PlayerCharacterId;
    public int RNGSeed { get; set; } = 123;
}

public sealed class Root : Entity<RootBlueprint>
{
    public static readonly CharacterId SYSTEM_ID = new CharacterId("root@origin.sys");

    public IGameDbId OwnerEntityId => null; // No owner. I'm root!

    public GameModelDatabases GameDatabases { get; private set; } = new(); //TODO: This can just be singular now
    public List<IGameEvent> Log { get; private set; } = new();
    public List<IGameDbId> _worlds = new();

    public List<IGameDbId> AttachedEntities { get; private set; } = new();

    public int RNGSeed { get; private set; }

    internal TurnData TurnData { get; set; } = new();
    public CharacterId? PlayerCharacterId { get; set; }

    protected override int Version => throw new System.NotImplementedException();

    protected override string TypeId => throw new System.NotImplementedException();

    // History
    private readonly List<SimulationLayerSnapshot> _history = new();
    private int _historyIndex;
    private const int MaxUndoLayers = 50;
    public bool CanUndo => _historyIndex > 0;
    public bool CanRedo => _historyIndex < _history.Count;
    public int HistoryLength => _history.Count;
    public int HistoryIndex => _historyIndex;

    public Root() { }
    public Root(RootBlueprint data)
    {
        GameDatabases = new GameModelDatabases();
        // TODO: Convert back to Databases
        TurnData = new TurnData(data.TurnData);
        PlayerCharacterId = data.PlayerCharacterId;
        RNGSeed = data.RNGSeed;

        Random.InitState(RNGSeed); //Init Seeded RNG
    }

    public override RootBlueprint SaveToBlueprint()
    {
        RootBlueprint rootSaveData = new RootBlueprint();
        rootSaveData.DatabaseAsSavePackets = GameDatabases.Save();
        rootSaveData.Log = new List<IGameEvent>(Log);
        //TODO
        return rootSaveData;
    }

    internal void PushSimulation()
    {
        GameDatabases.PushSimulation();
    }

    internal void PopSimulation()
    {
        GameDatabases.PopSimulation();
    }

    public void AddToGameLog(IGameEvent log)
    {
        Log.Add(log);
    }

    public void AssignPlayerCharacter(CharacterId? characterId)
    {
        PlayerCharacterId = characterId;
    }

    public CharacterId? UnassignPlayerCharacter()
    {
        var unassigned = PlayerCharacterId;

        PlayerCharacterId = null;

        return unassigned;
    }

    internal bool TryAddEntity(IGameDbId entityId)
    {
        if (AttachedEntities.Contains(entityId))
            return false;
        
        AttachedEntities.Add(entityId);
        return true;
    }

    internal bool TryRemoveEntity(IGameDbId entityId)
    {
        return AttachedEntities.Remove(entityId);
    }

    public bool SetHistoryIndex(int index)
    {
        int target = Math.Clamp(index, 0, _history.Count);
        if (target == _historyIndex)
            return false;

        bool changed = false;

        while (_historyIndex > target)
        {
            if (!TryUndoStep())
                break;
            changed = true;
        }

        while (_historyIndex < target)
        {
            if (!TryRedoStep())
                break;
            changed = true;
        }

        return changed;
    }

    private void ResetHistory()
    {
        while (_historyIndex > 0 && GameDatabases.SimDepth > 0)
        {
            PopSimulation();
            _historyIndex--;
        }

        _history.Clear();
        _historyIndex = 0;
    }

    private void CompactUndoLayers()
    {
        while (_history.Count > MaxUndoLayers)
        {
            if (_historyIndex > 0)
            {
                if (!GameDatabases.CommitOldestSimulationLayer())
                    break;

                _historyIndex--;
            }

            _history.RemoveAt(0);
        }
    }

    public bool TryUndoStep()
    {
        if (_historyIndex == 0)
            return false;

        
        if (!GameDatabases.TryPopSimulationLayer(out _))
            return false;

        _historyIndex--;
        return true;
    }

    public bool TryRedoStep()
    {
        if (_historyIndex >= _history.Count)
            return false;

        GameDatabases.PushSimulationLayer(_history[_historyIndex]);
        _historyIndex++;
        return true;
    }

    public bool FlattenSimulationLayers()
    {
        if (GameDatabases.SimDepth == 0)
            return false;

        while (GameDatabases.CommitOldestSimulationLayer())
        {
        }

        _history.Clear();
        _historyIndex = 0;
        return true;
    }

    internal void AddToHistory(SimulationLayerSnapshot snapshot)
    {
        if (_historyIndex < _history.Count)
        {
            _history.RemoveRange(_historyIndex, _history.Count - _historyIndex);
        }

        _history.Add(snapshot);
        _historyIndex = _history.Count;

        CompactUndoLayers();
    }
}
