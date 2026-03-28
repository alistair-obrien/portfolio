using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Newtonsoft.Json;

public sealed record BrowserGameEnvelope(
    bool Ok,
    string SessionId,
    string ErrorMessage,
    IReadOnlyList<HeadlessEventEnvelope> Events,
    RootGameModelPresentation? State
);

[SupportedOSPlatform("browser")]
public static partial class BrowserGameExports
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, HeadlessGameSession> Sessions = new();
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
    }

    [JSExport]
    public static string CreateSession()
    {
        var session = new HeadlessGameSession();
        var sessionId = Guid.NewGuid().ToString("N");
        var state = session.Reset(seedDevelopmentWorld: true);

        lock (SyncRoot)
        {
            Sessions[sessionId] = session;
        }

        return Serialize(new BrowserGameEnvelope(
            Ok: true,
            SessionId: sessionId,
            ErrorMessage: string.Empty,
            Events: Array.Empty<HeadlessEventEnvelope>(),
            State: state));
    }

    [JSExport]
    public static string GetSessionState(string sessionId)
    {
        if (!TryGetSession(sessionId, out var session))
            return SerializeMissingSession(sessionId);

        return Serialize(new BrowserGameEnvelope(
            Ok: true,
            SessionId: sessionId,
            ErrorMessage: string.Empty,
            Events: Array.Empty<HeadlessEventEnvelope>(),
            State: session.GetState()));
    }

    [JSExport]
    public static string ResetSession(string sessionId)
    {
        if (!TryGetSession(sessionId, out var session))
            return SerializeMissingSession(sessionId);

        return Serialize(new BrowserGameEnvelope(
            Ok: true,
            SessionId: sessionId,
            ErrorMessage: string.Empty,
            Events: Array.Empty<HeadlessEventEnvelope>(),
            State: session.Reset(seedDevelopmentWorld: true)));
    }

    [JSExport]
    public static string MovePlayerToCell(string sessionId, string mapId, int x, int y)
    {
        if (!TryGetSession(sessionId, out var session))
            return SerializeMissingSession(sessionId);

        var response = session.MovePlayerToCell(new MapChunkId(mapId), x, y);

        return Serialize(new BrowserGameEnvelope(
            Ok: response.Ok,
            SessionId: sessionId,
            ErrorMessage: response.ErrorMessage ?? string.Empty,
            Events: response.Events ?? Array.Empty<HeadlessEventEnvelope>(),
            State: response.State));
    }

    [JSExport]
    public static bool DisposeSession(string sessionId)
    {
        lock (SyncRoot)
        {
            return Sessions.Remove(sessionId);
        }
    }

    private static bool TryGetSession(string sessionId, out HeadlessGameSession session)
    {
        HeadlessGameSession? resolvedSession;

        lock (SyncRoot)
        {
            var found = Sessions.TryGetValue(sessionId, out resolvedSession);
            session = resolvedSession!;
            return found;
        }
    }

    private static string SerializeMissingSession(string sessionId)
    {
        return Serialize(new BrowserGameEnvelope(
            Ok: false,
            SessionId: sessionId ?? string.Empty,
            ErrorMessage: $"No browser-local session exists for '{sessionId}'.",
            Events: Array.Empty<HeadlessEventEnvelope>(),
            State: null));
    }

    private static string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, JsonSettings);
    }
}
