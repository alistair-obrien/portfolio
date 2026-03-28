using System;
using System.Collections.Generic;

public interface ILogger
{
    void Log(object message);
    void LogWarning(object message);
    void LogError(object message);
}

internal sealed class CommandExecutionContext
{
    public readonly int SimLayer;
    public readonly List<IGameEvent> Events;

    public CommandExecutionContext(int simLayer, List<IGameEvent> events)
    {
        SimLayer = simLayer;
        Events = events;
    }
}

internal sealed class CommandTraceNode
{
    public readonly IGameCommand Command;
    public readonly int SimLayer;
    public readonly List<CommandTraceNode> Children = new();

    public CommandResult Result;

    public CommandTraceNode(IGameCommand command, int simLayer)
    {
        Command = command;
        SimLayer = simLayer;
    }
}

public record CommandResult(bool Ok, string ErrorMessage)
{
    internal static CommandResult Fail(string message) => new CommandResult(false, message);
    internal static CommandResult Success() => new CommandResult(true, null);
}

public sealed class CommandRouter
{
    private readonly Dictionary<Type, Func<IGameCommand, CommandResult>> _handlers = new();

    public void Register<T>(Func<T, CommandResult> handler)
        where T : IGameCommand
    {
        _handlers[typeof(T)] = cmd => handler((T)cmd);
    }

    public CommandResult TryExecute(IGameCommand command)
    {
        if (!_handlers.TryGetValue(command.GetType(), out var handler))
        {
            Debug.Log($"GameAPI: Handler Not Found {command.GetType()}");
            return CommandResult.Fail($"GameAPI: Handler Not Found {command.GetType()}");
        }

        return handler(command);
    }
}

public interface IGameCommandHandler<in T>
where T : IGameCommand
{
    bool Handle(T command);
}

public partial class GameInstance : IDisposable
{
    private readonly CommandRouter _commandRouter = new();

    private readonly Stack<CommandExecutionContext> _executionStack = new();
    private readonly Stack<CommandTraceNode> _traceStack = new();
    private readonly List<CommandTraceNode> _traceRoots = new();
    private int _traceSessionDepth;

    internal TemplatesGameModel TemplatesGameModel; // This stays regardless of save/load
    internal Root RootModel; // This can be replaced completely
    internal readonly Rulebook Rulebook;

    // Subdomains
    private readonly List<APIDomain> _subDomains;
    public readonly FileAPI File;
    public readonly SaveDataAPI SaveData;
    public readonly TemplatesAPI Templates;
    public readonly DatabaseAPI Databases;
    public readonly ItemsAPI Items;
    public readonly MapsAPI Maps;
    public readonly PropsAPI WorldObjects;
    public readonly CharactersAPI Characters;
    public readonly CombatAPI Combat;
    public readonly DamageAPI Damage;
    public readonly TurnsAPI Turns;
    public readonly OperatingSystemsAPI OperatingSystems;
    public readonly InteractionsAPI Interactions;

    public readonly IGameSerializer GameSerializer;

    public int SimDepth => RootModel.GameDatabases.SimDepth;

    public GameInstance() : this(null, null, null)
    {

    }

    public GameInstance(
        Root model, 
        TemplatesGameModel templatesGameModel, 
        Rulebook rulebook)
    {
        Debug.Log("Game Instance Created");

        GameSerializer = new JsonGameSerializer();

        // Subdomain Constructors should never do anything except for link the GameAPI reference
        // All other references are resolved through properties pointing though GameAPI
        // If additional initialization is required post connection resolve, make an initialization method in this class and run all _subDomain initializions
        _subDomains = new()
        {
            (File = new FileAPI(this)),
            (SaveData = new SaveDataAPI(this)),
            (Templates = new TemplatesAPI(this)),
            (Databases = new DatabaseAPI(this)),
            (Items = new ItemsAPI(this)),
            (Maps = new MapsAPI(this)),
            (WorldObjects = new PropsAPI(this)),
            (Characters = new CharactersAPI(this)),
            (Combat = new CombatAPI(this)),
            (Damage = new DamageAPI(this)),
            (Turns = new TurnsAPI(this)),
            (OperatingSystems = new OperatingSystemsAPI(this)),
            (Interactions = new InteractionsAPI(this)),
        };

        foreach (var subdomain in _subDomains)
        {
            subdomain.RegisterHandlers(_commandRouter);
        }

        SetGameModel(model);
        Rulebook = rulebook ?? new Rulebook(this);
        TemplatesGameModel = templatesGameModel ?? new TemplatesGameModel();

        Templates.LoadAll(new TemplatesAPI.Commands.LoadAllTemplates());
        Databases.Initialize();
    }

    public void EnqueueAction(CharacterId characterId, IGameCommand request)
    {
        Turns.TryEnqueueActionRequestForCharacter(characterId, request);
    }

    public void EnqueueActions(CharacterId characterId, IEnumerable<IGameCommand> requests)
    {
        foreach (var request in requests)
        {
            EnqueueAction(characterId, request);
        }
    }

    internal CommandResult TryPreviewCommand(
        IGameCommand command,
        out List<IGameEvent> eventsBuffer)
    {
        BeginTraceSession();
        PushSimulation();

        try
        {
            return TryExecuteCommand(command, out eventsBuffer);
        }
        finally
        {
            PopSimulation();
            EndTraceSession();
        }
    }

    internal CommandResult TryPreviewCommands(
        IEnumerable<IGameCommand> commands,
        out List<IGameEvent> eventsBuffer)
    {
        BeginTraceSession();
        PushSimulation();

        try
        {
            return TryExecuteCommands(commands, out eventsBuffer);
        }
        finally
        {
            PopSimulation();
            EndTraceSession();
        }
    }

    internal CommandResult TryExecuteCommands(
        IEnumerable<IGameCommand> commands,
        out List<IGameEvent> eventsBuffer)
    {
        BeginTraceSession();
        eventsBuffer = new List<IGameEvent>();

        var context = new CommandExecutionContext(SimDepth, eventsBuffer);
        _executionStack.Push(context);

        try
        {
            CommandResult result = CommandResult.Success();

            foreach (var cmd in commands)
            {
                if (cmd == null)
                    return CommandResult.Fail("Encountered null command in command batch.");

                result = ExecuteCommandInternal(cmd);
                if (!result.Ok)
                    return result;
            }

            return result;
        }
        finally
        {
            _executionStack.Pop();
            EndTraceSession();
        }
    }

    internal CommandResult TryExecuteCommand(
        IGameCommand command,
        out List<IGameEvent> eventsBuffer)
    {
        BeginTraceSession();
        eventsBuffer = new List<IGameEvent>();

        var context = new CommandExecutionContext(SimDepth, eventsBuffer);
        _executionStack.Push(context);

        try
        {
            return ExecuteCommandInternal(command);
        }
        finally
        {
            _executionStack.Pop();
            EndTraceSession();
        }
    }

    internal CommandResult TryExecuteCommand(IGameCommand command)
    {
        //if (_executionStack.Count == 0)
        //    throw new InvalidOperationException("No command execution context active");

        BeginTraceSession();

        try
        {
            return ExecuteCommandInternal(command);
        }
        finally
        {
            EndTraceSession();
        }
    }

    private CommandResult ExecuteCommandInternal(IGameCommand command)
    {
        var traceNode = new CommandTraceNode(command, SimDepth);
        if (_traceStack.Count > 0)
        {
            _traceStack.Peek().Children.Add(traceNode);
        }
        else
        {
            _traceRoots.Add(traceNode);
        }

        _traceStack.Push(traceNode);

        try
        {
            CommandResult result = _commandRouter.TryExecute(command);
            traceNode.Result = result;
            return result;
        }
        catch (Exception ex)
        {
            traceNode.Result = CommandResult.Fail($"Unhandled exception: {ex.Message}");
            throw;
        }
        finally
        {
            _traceStack.Pop();
        }
    }

    private void BeginTraceSession()
    {
        if (_traceSessionDepth == 0)
        {
            _traceRoots.Clear();
            _traceStack.Clear();
        }

        _traceSessionDepth++;
    }

    private void EndTraceSession()
    {
        _traceSessionDepth--;

        if (_traceSessionDepth > 0)
            return;

        foreach (var root in _traceRoots)
        {
            LogTraceNode(root, 0);
        }

        _traceRoots.Clear();
        _traceStack.Clear();
    }

    private void LogTraceNode(CommandTraceNode node, int depth)
    {
        string prefix = BuildTracePrefix(depth);

        if (node.Result != null && node.Result.Ok)
        {
            string successLabel = node.SimLayer > 0
                ? "[color=blue]Simulation Command Succeeded:[/color]"
                : "[color=green]Command Succeeded:[/color]";

            Debug.Log($"{prefix}{successLabel} {node.Command}");
        }
        else
        {
            string failureLabel = node.SimLayer > 0
                ? "[color=pink]Simulation Command Failed:[/color]"
                : "[color=red]Command Failed:[/color]";

            string errorMessage = node.Result?.ErrorMessage ?? "Command failed without a result.";

            Debug.Log($"{prefix}{failureLabel} {errorMessage}");
            Debug.Log($"{prefix}[color=gray]Command:[/color] {node.Command}");
        }

        foreach (var child in node.Children)
        {
            LogTraceNode(child, depth + 1);
        }
    }

    private static string BuildTracePrefix(int depth)
    {
        if (depth <= 0)
            return string.Empty;

        var prefix = new System.Text.StringBuilder(depth * 22);
        for (int i = 0; i < depth; i++)
        {
            prefix.Append("[color=gray]| [/color]");
        }

        return prefix.ToString();
    }

    internal void RaiseEvent(IGameEvent evt)
    {
        //if (_executionStack.Count == 0)
        //    throw new InvalidOperationException("No command execution context active");

        // Only raise if there is an active execution stack
        if (_executionStack.Count > 0)
            _executionStack.Peek().Events.Add(evt);
    }

    //internal IReadOnlyList<IGameEvent> ConsumeRaisedEvents()
    //{
    //    if (_raisedEvents.Count == 0)
    //        return Array.Empty<IGameEvent>();

    //    var copy = _raisedEvents.ToArray();
    //    _raisedEvents.Clear();
    //    return copy;
    //}

    public RootGameModelPresentation PullRootGameModelPresentation()
    {
        return new RootGameModelPresentation(this, RootModel);
    }

    internal void SetGameModel(Root rootGameModel)
    {
        //RootModel?.Shutdown();
        RootModel = rootGameModel ?? new Root();
        //RootModel.Initialize();

        Databases.Initialize(); // Need too do this so that the db registration points at the right place
    }

    public void Dispose()
    {
    }

    internal void PushSimulation()
    {
        RootModel.PushSimulation();
    }

    internal void PopSimulation()
    {
        RootModel.PopSimulation();
    }

    internal bool TryPopSimulationLayer(out SimulationLayerSnapshot snapshot)
    {
        return RootModel.GameDatabases.TryPopSimulationLayer(out snapshot);
    }

    internal void PushSimulationLayer(SimulationLayerSnapshot snapshot)
    {
        RootModel.GameDatabases.PushSimulationLayer(snapshot);
    }

    internal bool CommitOldestSimulationLayer()
    {
        return RootModel.GameDatabases.CommitOldestSimulationLayer();
    }

    internal void ClearSimulationLayers()
    {
        RootModel.GameDatabases.ClearSimulationLayers();
    }

    internal CommandResult TryExecuteCommandsTracked(IEnumerable<IGameCommand> commands, out List<IGameEvent> events)
    {
        PushSimulation();

        CommandResult result = TryExecuteCommands(commands, out events);
        if (!result.Ok)
        {
            PopSimulation();
            return result;
        }

        if (!TryPopSimulationLayer(out var snapshot))
            return CommandResult.Fail("Failed to pop the simulation layer after executing tracked commands.");

        PushSimulationLayer(snapshot);

        RootModel.AddToHistory(snapshot);
        return CommandResult.Success();
    }
}
