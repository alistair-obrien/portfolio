using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Newtonsoft.Json;

public sealed record BrowserGameEnvelope(
    string SessionId,
    CommandResult Result,
    IReadOnlyList<GameEventEnvelope> Events,
    RootGameModelPresentation? State
);

[SupportedOSPlatform("browser")]
public static partial class BrowserGameExports
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, GameInstance> Sessions = new(StringComparer.Ordinal);
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        Formatting = Formatting.None,
        PreserveReferencesHandling = PreserveReferencesHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        NullValueHandling = NullValueHandling.Include,
    };

    static BrowserGameExports()
    {
        TypedIdTypeRegistry.EnsureInitialized();
        Debug.BindLogger(new BrowserConsoleLogger());
    }

    [JSExport]
    public static string CreateSession()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new GameInstance();
        var response = session.Reset(seedDevelopmentWorld: true);

        lock (SyncRoot)
        {
            Sessions[sessionId] = session;
        }

        return Serialize(new BrowserGameEnvelope(
            SessionId: sessionId,
            Result: response.Result,
            Events: response.Events,
            State: null));
    }

    [JSExport]
    public static string ResetSession(string sessionId)
    {
        return ExecuteForSession(
            sessionId,
            session => session.Reset(seedDevelopmentWorld: true));
    }

    [JSExport]
    public static string DisposeSession(string sessionId)
    {
        GameInstance? session = null;

        lock (SyncRoot)
        {
            if (sessionId != null && Sessions.TryGetValue(sessionId, out var resolvedSession))
            {
                session = resolvedSession;
                Sessions.Remove(sessionId);
            }
        }

        if (session == null)
            return SerializeMissingSession(sessionId ?? string.Empty);

        session.Dispose();

        return Serialize(new BrowserGameEnvelope(
            SessionId: sessionId ?? string.Empty,
            Result: new CommandResult(true, null),
            Events: Array.Empty<GameEventEnvelope>(),
            State: null));
    }

    [JSExport]
    public static string ExecuteCommand(string sessionId, string commandJson)
    {
        return ExecuteForSession(
            sessionId,
            session => session.ExecuteCommandPayload(commandJson));
    }

    [JSExport]
    public static string ExecuteCommands(string sessionId, string commandsJson)
    {
        return ExecuteForSession(
            sessionId,
            session => session.ExecuteCommandsPayload(commandsJson));
    }

    [JSExport]
    public static string ExecuteTrackedCommand(string sessionId, string commandJson)
    {
        return ExecuteForSession(
            sessionId,
            session => session.ExecuteTrackedCommandPayload(commandJson));
    }

    [JSExport]
    public static string ExecuteTrackedCommands(string sessionId, string commandsJson)
    {
        return ExecuteForSession(
            sessionId,
            session => session.ExecuteTrackedCommandsPayload(commandsJson));
    }

    [JSExport]
    public static string ExecutePreviewCommand(string sessionId, string commandJson)
    {
        return ExecuteForSession(
            sessionId,
            session => session.ExecutePreviewCommandPayload(commandJson));
    }

    [JSExport]
    public static string ExecutePreviewCommands(string sessionId, string commandsJson)
    {
        return ExecuteForSession(
            sessionId,
            session => session.ExecutePreviewCommandsPayload(commandsJson));
    }

    [JSExport]
    public static string GetGameState(string sessionId)
    {
        if (!TryGetSession(sessionId, out var session))
            return SerializeMissingSession(sessionId);

        try
        {
            return Serialize(new BrowserGameEnvelope(
                SessionId: sessionId ?? string.Empty,
                Result: new CommandResult(true, null),
                Events: Array.Empty<GameEventEnvelope>(),
                State: session.GetGameState()));
        }
        catch (Exception ex)
        {
            return Serialize(new BrowserGameEnvelope(
                SessionId: sessionId ?? string.Empty,
                Result: new CommandResult(false, ex.Message),
                Events: Array.Empty<GameEventEnvelope>(),
                State: null));
        }
    }

    private static string ExecuteForSession(
        string sessionId,
        Func<GameInstance, GameSessionExecutionResponse> operation)
    {
        if (!TryGetSession(sessionId, out var session))
            return SerializeMissingSession(sessionId);

        try
        {
            var response = operation(session);
            return Serialize(new BrowserGameEnvelope(
                SessionId: sessionId ?? string.Empty,
                Result: response.Result,
                Events: response.Events,
                State: null));
        }
        catch (Exception ex)
        {
            return Serialize(new BrowserGameEnvelope(
                SessionId: sessionId ?? string.Empty,
                Result: new CommandResult(false, ex.Message),
                Events: Array.Empty<GameEventEnvelope>(),
                State: null));
        }
    }

    private static bool TryGetSession(string sessionId, out GameInstance session)
    {
        GameInstance? resolvedSession = null;

        lock (SyncRoot)
        {
            var found = sessionId != null && Sessions.TryGetValue(sessionId, out resolvedSession);
            session = resolvedSession!;
            return found;
        }
    }

    private static string SerializeMissingSession(string sessionId)
    {
        return Serialize(new BrowserGameEnvelope(
            SessionId: sessionId ?? string.Empty,
            Result: new CommandResult(false, $"No browser-local session exists for '{sessionId}'."),
            Events: Array.Empty<GameEventEnvelope>(),
            State: null));
    }

    private static string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, JsonSettings);
    }
}
