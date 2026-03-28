using System.Collections.Generic;

public struct InteractionRequest
{
    public string OptionKey;
    public string Name;
    public List<IGameCommand> Commands = new();

    public InteractionRequest(string name, IGameCommand commands) : this()
    {
        OptionKey = name; // For now
        Name = name;
        Commands = new List<IGameCommand> { commands };
    }

    public InteractionRequest(string name, List<IGameCommand> commands) : this()
    {
        OptionKey = name; // For now
        Name = name;
        Commands = commands;
    }
}