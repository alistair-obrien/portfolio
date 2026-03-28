using System;
using Newtonsoft.Json;

public sealed class BrowserConsoleLogger : ILogger
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        Formatting = Formatting.Indented,
        PreserveReferencesHandling = PreserveReferencesHandling.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        NullValueHandling = NullValueHandling.Include,
    };

    public void Log(object message)
    {
        Console.WriteLine(Normalize(message));
    }

    public void LogWarning(object message)
    {
        Console.WriteLine($"WARNING: {Normalize(message)}");
    }

    public void LogError(object message)
    {
        Console.Error.WriteLine($"ERROR: {Normalize(message)}");
    }

    private static string Normalize(object message)
    {
        if (message == null)
            return string.Empty;

        if (message is not string)
            return JsonConvert.SerializeObject(message, JsonSettings);

        var text = (message.ToString() ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();

        return text;
    }
}
