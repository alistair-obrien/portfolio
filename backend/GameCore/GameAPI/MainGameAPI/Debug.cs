using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public static class Debug
{
    private static List<ILogger> _loggers = new();

    public static void BindLogger(ILogger logger)
    {
        _loggers.Add(logger);
    }

    public static void Log(object message)
    {
        string formatted = PrettyFormat(message.ToString());
        formatted = string.Join("\n", formatted.Split('\n').Select(l => l.TrimEnd()));
        _loggers.ForEach(l => l.Log(formatted));
    }

    public static void LogWarning(object message)
    {
        _loggers.ForEach(l => l.LogWarning(message));
    }

    public static void LogError(object message)
    {
        _loggers.ForEach(l => l.LogError(message));
    }

    public static string PrettyFormat(string input)
    {
        // 🔹 Step 1: normalize (THIS is what you're missing)
        input = input
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\t", " ")
            .Trim();

        int indent = 0;
        var result = new System.Text.StringBuilder();

        foreach (char c in input)
        {
            switch (c)
            {
                case '{':
                    result.Append(" {");
                    indent++;
                    result.AppendLine();
                    result.Append(new string(' ', indent * 2));
                    break;

                case '}':
                    indent--;
                    result.AppendLine();
                    result.Append(new string(' ', indent * 2));
                    result.Append('}');
                    break;

                case ',':
                    result.Append(',');
                    result.AppendLine();
                    result.Append(new string(' ', indent * 2));
                    break;

                default:
                    result.Append(c);
                    break;
            }
        }

        return result.ToString();
    }
}
