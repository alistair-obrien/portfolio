public partial class SaveDataAPI : APIDomain
{
    private FileAPI File => GameAPI.File;

    public SaveDataAPI(GameInstance gameAPI) : base(gameAPI) 
    {
        //SaveLoaderRegistry.Register<TemplateSaveData>("template", Template.Load);

        // We actually only want to tell it to load save data
        SaveLoaderRegistry.Register<CharacterBlueprint>("Character", (saveData, version) => saveData);
        SaveLoaderRegistry.Register<MapChunkBlueprint>("MapChunk", (saveData, version) => saveData);
        SaveLoaderRegistry.Register<PropBlueprint>("Prop", (saveData, version) => saveData);
        SaveLoaderRegistry.Register<ItemBlueprint>("Item", (saveData, version) => saveData);
        SaveLoaderRegistry.Register<WorldBlueprint>("World", (saveData, version) => saveData);
    }

    internal override void RegisterHandlers(CommandRouter router)
    {
        router.Register<Commands.StartNewGame>(StartNewGame);
        router.Register<Commands.LoadGame>(LoadGame);
        router.Register<Commands.SaveGame>(SaveGame);
    }

    internal CommandResult StartNewGame(Commands.StartNewGame command)
    {
        GameAPI.SetGameModel(new Root());
        RaiseEvent(new NewGameStarted());
        return Ok();
    }

    private CommandResult SaveGame(Commands.SaveGame command)
    {
        if (!File.TrySaveToDisk(RootModel.SaveAsPacket(), command.saveSlotName))
            return Fail($"Failed to save the game to slot '{command.saveSlotName}'.");

        RaiseEvent(new GameSaved(command.saveSlotName));
        return Ok();
    }

    private CommandResult LoadGame(Commands.LoadGame command)
    {
        if (!File.TryLoadFromDisk<RootBlueprint>(command.saveSlotName, out var gameModel))
            return Fail($"Failed to load the game from slot '{command.saveSlotName}'.");

        Root root = new Root(gameModel);

        GameAPI.SetGameModel(root);
        RaiseEvent(new GameLoaded(command.saveSlotName));
        return Ok();
    }
}
