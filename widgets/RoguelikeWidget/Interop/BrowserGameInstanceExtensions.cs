using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public sealed record GameEventEnvelope(
    string Type,
    string Description,
    JToken Payload
);

public sealed record GameSessionExecutionResponse(
    CommandResult Result,
    IReadOnlyList<GameEventEnvelope> Events
);

internal static class BrowserGameInstanceExtensions
{
    public static RootGameModelPresentation GetGameState(this GameInstance game)
    {
        return game.PullRootGameModelPresentation();
    }

    public static GameSessionExecutionResponse Reset(this GameInstance game, bool seedDevelopmentWorld = true)
    {
        game.SetGameModel(new Root());

        if (!seedDevelopmentWorld)
            return CreateExecutionResponse(new CommandResult(true, null), Array.Empty<IGameEvent>());

        return game.ExecuteCommands(BuildDevelopmentWorldSeedCommands());
    }

    public static GameSessionExecutionResponse ExecuteCommand(this GameInstance game, IGameCommand command)
    {
        var result = game.TryExecuteCommand(command, out var eventsBuffer);
        return CreateExecutionResponse(result, eventsBuffer);
    }

    public static GameSessionExecutionResponse ExecuteCommands(this GameInstance game, IEnumerable<IGameCommand> commands)
    {
        var result = game.TryExecuteCommands(commands, out var eventsBuffer);
        return CreateExecutionResponse(result, eventsBuffer);
    }

    public static GameSessionExecutionResponse ExecuteTrackedCommand(this GameInstance game, IGameCommand command)
    {
        return game.ExecuteTrackedCommands(new[] { command });
    }

    public static GameSessionExecutionResponse ExecuteTrackedCommands(this GameInstance game, IEnumerable<IGameCommand> commands)
    {
        var result = game.TryExecuteCommandsTracked(commands, out var eventsBuffer);
        return CreateExecutionResponse(result, eventsBuffer);
    }

    public static GameSessionExecutionResponse ExecutePreviewCommand(this GameInstance game, IGameCommand command)
    {
        var result = game.TryPreviewCommand(command, out var eventsBuffer);
        return CreateExecutionResponse(result, eventsBuffer);
    }

    public static GameSessionExecutionResponse ExecutePreviewCommands(this GameInstance game, IEnumerable<IGameCommand> commands)
    {
        var result = game.TryPreviewCommands(commands, out var eventsBuffer);
        return CreateExecutionResponse(result, eventsBuffer);
    }

    public static GameSessionExecutionResponse ExecuteCommandPayload(this GameInstance game, string commandJson)
    {
        return game.ExecuteCommand(GameCommandWireProtocol.DeserializeCommand(commandJson));
    }

    public static GameSessionExecutionResponse ExecuteCommandsPayload(this GameInstance game, string commandsJson)
    {
        return game.ExecuteCommands(GameCommandWireProtocol.DeserializeCommands(commandsJson));
    }

    public static GameSessionExecutionResponse ExecuteTrackedCommandPayload(this GameInstance game, string commandJson)
    {
        return game.ExecuteTrackedCommand(GameCommandWireProtocol.DeserializeCommand(commandJson));
    }

    public static GameSessionExecutionResponse ExecuteTrackedCommandsPayload(this GameInstance game, string commandsJson)
    {
        return game.ExecuteTrackedCommands(GameCommandWireProtocol.DeserializeCommands(commandsJson));
    }

    public static GameSessionExecutionResponse ExecutePreviewCommandPayload(this GameInstance game, string commandJson)
    {
        return game.ExecutePreviewCommand(GameCommandWireProtocol.DeserializeCommand(commandJson));
    }

    public static GameSessionExecutionResponse ExecutePreviewCommandsPayload(this GameInstance game, string commandsJson)
    {
        return game.ExecutePreviewCommands(GameCommandWireProtocol.DeserializeCommands(commandsJson));
    }

    private static IEnumerable<IGameCommand> BuildDevelopmentWorldSeedCommands()
    {
        var driver = new NeuralDriverBlueprint
        {
            Id = Root.SYSTEM_DRIVER_ID,
            Name = "Administrator Neural Driver",
            Substrate = NeuralDriverSubstrate.Organic,
            Agency = NeuralDriverAgency.Human,
        };
        driver.Grants.AddRange(GrantCatalog.CreateBuildingGrants());
        driver.Grants.AddRange(GrantCatalog.CreateAuthoringGrants());

        var character = new CharacterBlueprint
        {
            Id = Root.SYSTEM_ID,
            Name = "Orion Styrogis",
        };

        character.OperatingSystem.Id = OperatingSystemIds.GenoSys;
        character.ActiveNeuralDriverId = driver.Id;

        const int mapWidth = 52;
        const int mapHeight = 30;
        var testMapId = MapChunkId.New();
        var locationTarget = new MapLocation(testMapId, new CellFootprint(5, 4, 1, 1));

        var commands = new List<IGameCommand>
        {
            new DatabaseAPI.Commands.CreateOrUpdateModel(driver),
            new DatabaseAPI.Commands.CreateOrUpdateModel(character),
            new CharactersAPI.Commands.AssignPlayerCharacter(Root.SYSTEM_ID),
            new MapsAPI.Commands.CreateMap(Root.SYSTEM_ID, testMapId, mapWidth, mapHeight),
        };

        commands.AddRange(BuildBasicLevelCommands(testMapId, mapWidth, mapHeight));
        commands.AddRange(BuildStarterItemsCommands(Root.SYSTEM_ID, testMapId));

        commands.Add(new ItemsAPI.Commands.MoveEntity(
            Root.SYSTEM_ID,
            Root.SYSTEM_ID,
            locationTarget,
            false));

        return commands;
    }

    private static IEnumerable<IGameCommand> BuildBasicLevelCommands(
        MapChunkId mapId,
        int mapWidth,
        int mapHeight)
    {
        foreach (var wallCell in EnumerateWallCells(mapWidth, mapHeight))
        {
            var wall = BuildWallBlueprint(wallCell.X, wallCell.Y);

            yield return new DatabaseAPI.Commands.CreateOrUpdateModel(wall);
            yield return new MapsAPI.Commands.AddPropToMap(
                Root.SYSTEM_ID,
                mapId,
                wall.Id,
                wallCell.X,
                wallCell.Y);
        }
    }

    private static PropBlueprint BuildWallBlueprint(int x, int y)
    {
        return new PropBlueprint
        {
            Id = PropId.New(),
            Name = $"Wall ({x}, {y})",
            Description = "A simple structural wall used for the generated test level.",
            FlavorText = "Cold, hard, and exactly where level generation put it.",
            RenderKey = new RenderKey("wall_basic"),
            SizeOnMap = new CellSize(1, 1),
            Layers = new List<PropLayerSaveData>
            {
                new PropLayerSaveData
                {
                    Name = "Wall Structure",
                    MaxIntegrity = 12,
                    RenderKey = new RenderKey("wall_basic"),
                }
            }
        };
    }

    private static IEnumerable<IGameCommand> BuildStarterItemsCommands(CharacterId characterId, MapChunkId mapId)
    {
        var backpack = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Street Pack",
            Description = "A worn but roomy pack with enough slots for field gear.",
            FlavorText = "Scuffed canvas, patched straps, still dependable.",
            RenderKey = new RenderKey("inventory_pack"),
            SizeInInventory = new CellSize(2, 3),
            Inventory = new InventoryBlueprint
            {
                Columns = 8,
                Rows = 6
            }
        };

        var pistol = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Staccato Pistol",
            Description = "A compact sidearm tuned for close-quarter reliability.",
            FlavorText = "It kicks a little harder than it looks.",
            RenderKey = new RenderKey("weapon_pistol"),
            SizeInInventory = new CellSize(2, 3),
            Gun = new GunSaveData
            {
                ClipSize = 8,
                RequiresBothHands = false,
                ShotRadiusInTiles = 1f
            }
        };

        var jacket = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Padded Jacket",
            Description = "Flexible street armor with stitched ceramic pads.",
            FlavorText = "Less style than a trench, more survival than a hoodie.",
            RenderKey = new RenderKey("armor_jacket"),
            SizeInInventory = new CellSize(2, 3),
            Armor = new ArmorBlueprint()
        };

        var medkit = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Trauma Kit",
            Description = "A compact emergency kit with fast-seal injectors.",
            FlavorText = "Patch first. Panic later.",
            RenderKey = new RenderKey("consumable_medkit"),
            SizeInInventory = new CellSize(2, 2),
            Consumable = new ConsumableSaveData
            {
                CurrentUses = 3,
                MaxUses = 3
            }
        };

        var ammoBox = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Light Ammo",
            Description = "A tidy box of pistol rounds.",
            FlavorText = "Enough to feel brave for a little while.",
            RenderKey = new RenderKey("ammo_light"),
            SizeInInventory = new CellSize(1, 2),
            Ammo = new AmmoSaveData()
        };

        var battery = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Battery Cell",
            Description = "A charge cell for field electronics and improvised hacks.",
            FlavorText = "Still humming with residual charge.",
            RenderKey = new RenderKey("battery_cell"),
            SizeInInventory = new CellSize(1, 1)
        };

        var fieldRation = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Field Ration",
            Description = "Dense calories in a silvered pouch.",
            FlavorText = "Tastes like cardboard and optimism.",
            RenderKey = new RenderKey("ration_pack"),
            SizeInInventory = new CellSize(1, 2),
            Consumable = new ConsumableSaveData
            {
                CurrentUses = 2,
                MaxUses = 2
            }
        };

        var streetLoot = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Loose Cache",
            Description = "A scavenged bundle left out in the open.",
            FlavorText = "Someone meant to come back for it.",
            RenderKey = new RenderKey("street_cache"),
            SizeInInventory = new CellSize(2, 1)
        };

        var sidearmAmmo = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Spare Magazine",
            Description = "A loaded magazine wrapped in an elastic retention band.",
            FlavorText = "Always easier to carry than regret.",
            RenderKey = new RenderKey("ammo_magazine"),
            SizeInInventory = new CellSize(1, 2),
            Ammo = new AmmoSaveData()
        };

        var stimPatch = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Stim Patch",
            Description = "A disposable combat stimulant with a short burn window.",
            FlavorText = "A bad idea that often arrives just in time.",
            RenderKey = new RenderKey("stim_patch"),
            SizeInInventory = new CellSize(1, 1),
            Consumable = new ConsumableSaveData
            {
                CurrentUses = 1,
                MaxUses = 1
            }
        };

        var scrapBlade = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Scrap Shiv",
            Description = "A sharpened strip of metal wrapped in tape.",
            FlavorText = "Ugly, honest, and still dangerous.",
            RenderKey = new RenderKey("weapon_shiv"),
            SizeInInventory = new CellSize(1, 3)
        };

        var toolCase = new ItemBlueprint
        {
            Id = ItemId.New(),
            Name = "Tool Case",
            Description = "A compact repair kit full of stubborn little miracles.",
            FlavorText = "Heavy enough to matter, useful enough to keep.",
            RenderKey = new RenderKey("tool_case"),
            SizeInInventory = new CellSize(2, 2)
        };

        foreach (var item in new[] { backpack, pistol, jacket, medkit, ammoBox, battery, fieldRation, streetLoot, sidearmAmmo, stimPatch, scrapBlade, toolCase })
            yield return new DatabaseAPI.Commands.CreateOrUpdateModel(item);

        yield return new ItemsAPI.Commands.MoveEntity(characterId, backpack.Id, new AttachedLocation(characterId, SlotIds.Loadout.Inventory), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, pistol.Id, new AttachedLocation(characterId, SlotIds.Loadout.PrimaryWeapon), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, jacket.Id, new AttachedLocation(characterId, SlotIds.Loadout.Armor), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, medkit.Id, new InventoryLocation(backpack.Id, new CellPosition(0, 0)), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, ammoBox.Id, new InventoryLocation(backpack.Id, new CellPosition(2, 0)), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, battery.Id, new InventoryLocation(backpack.Id, new CellPosition(3, 0)), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, fieldRation.Id, new InventoryLocation(backpack.Id, new CellPosition(4, 1)), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, streetLoot.Id, new MapLocation(mapId, new CellFootprint(11, 5, streetLoot.SizeOnMap.Width, streetLoot.SizeOnMap.Height)), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, sidearmAmmo.Id, new MapLocation(mapId, new CellFootprint(21, 6, sidearmAmmo.SizeOnMap.Width, sidearmAmmo.SizeOnMap.Height)), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, stimPatch.Id, new MapLocation(mapId, new CellFootprint(29, 17, stimPatch.SizeOnMap.Width, stimPatch.SizeOnMap.Height)), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, scrapBlade.Id, new MapLocation(mapId, new CellFootprint(41, 9, scrapBlade.SizeOnMap.Width, scrapBlade.SizeOnMap.Height)), false);
        yield return new ItemsAPI.Commands.MoveEntity(characterId, toolCase.Id, new MapLocation(mapId, new CellFootprint(16, 24, toolCase.SizeOnMap.Width, toolCase.SizeOnMap.Height)), false);
    }

    private static IEnumerable<CellPosition> EnumerateWallCells(int mapWidth, int mapHeight)
    {
        var occupied = new HashSet<(int X, int Y)>();

        void Add(int x, int y)
        {
            if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight)
                return;

            occupied.Add((x, y));
        }

        void AddHorizontalWall(int fromX, int toX, int y, params int[] doorXs)
        {
            var doors = new HashSet<int>(doorXs ?? Array.Empty<int>());
            for (int x = fromX; x <= toX; x++)
            {
                if (doors.Contains(x))
                    continue;

                Add(x, y);
            }
        }

        void AddVerticalWall(int x, int fromY, int toY, params int[] doorYs)
        {
            var doors = new HashSet<int>(doorYs ?? Array.Empty<int>());
            for (int y = fromY; y <= toY; y++)
            {
                if (doors.Contains(y))
                    continue;

                Add(x, y);
            }
        }

        AddHorizontalWall(0, mapWidth - 1, 0);
        AddHorizontalWall(0, mapWidth - 1, mapHeight - 1);
        AddVerticalWall(0, 1, mapHeight - 2);
        AddVerticalWall(mapWidth - 1, 1, mapHeight - 2);

        AddVerticalWall(17, 1, mapHeight - 2, 6, 7, 8, 13, 14, 15, 21, 22, 23);
        AddVerticalWall(34, 1, mapHeight - 2, 9, 10, 11, 17, 18, 19, 23, 24, 25);
        AddHorizontalWall(1, mapWidth - 2, 14, 8, 9, 10, 25, 26, 27, 41, 42, 43);
        AddHorizontalWall(17, mapWidth - 2, 22, 23, 24, 25, 37, 38, 39);

        AddHorizontalWall(4, 12, 8, 7, 8, 9);
        AddVerticalWall(12, 3, 8, 4, 5, 6);
        AddHorizontalWall(38, 47, 8, 42, 43, 44);
        AddVerticalWall(38, 3, 8, 4, 5, 6);
        AddHorizontalWall(6, 15, 25, 9, 10, 11);
        AddVerticalWall(6, 22, 27, 23, 24, 25);
        AddVerticalWall(15, 22, 27, 23, 24, 25);

        foreach (var (x, y) in occupied.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
            yield return new CellPosition(x, y);
    }

    private static GameSessionExecutionResponse CreateExecutionResponse(
        CommandResult result,
        IEnumerable<IGameEvent> eventsBuffer)
    {
        return new GameSessionExecutionResponse(
            result,
            BuildEventEnvelopes(eventsBuffer));
    }

    private static IReadOnlyList<GameEventEnvelope> BuildEventEnvelopes(IEnumerable<IGameEvent> eventsBuffer)
    {
        if (eventsBuffer == null)
            return Array.Empty<GameEventEnvelope>();

        return eventsBuffer
            .Select(evt => new GameEventEnvelope(
                evt.GetType().Name,
                evt.ToString() ?? evt.GetType().Name,
                JToken.FromObject(evt)))
            .ToList();
    }
}
