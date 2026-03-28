public partial class SaveDataAPI
{
    public class Commands
    {
        public sealed record StartNewGame() : IGameCommand;
        public sealed record SaveGame(string saveSlotName) : IGameCommand;
        public sealed record LoadGame(string saveSlotName) : IGameCommand;
    }
}