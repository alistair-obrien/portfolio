using System.Collections.Generic;

public class MapSettingsPresentation
{
    public readonly MapChunkTemplateId MapTemplateId;
    public readonly int Width;
    public readonly int Height;

    public MapSettingsPresentation(GameInstance gameAPI, MapChunkId mapInstanceId, MapChunkTemplateId templateId)
    {
        if (!gameAPI.Databases.TryResolve(mapInstanceId, out MapChunk map))
            return;

        MapTemplateId = templateId;
        Width = map.Width;
        Height = map.Height;
    }
}

public class MapPresentation
{
    public readonly MapChunkId MapId;
    public readonly int Width;
    public readonly int Height;
    public readonly IEnumerable<MapTilePresentation> Tiles;
    public readonly IEnumerable<MapItemPlacementPresentation> Items;
    public readonly IEnumerable<MapCharacterPlacementPresentation> Characters;
    public readonly IEnumerable<MapPropPlacementPresentation> Props;

    public MapPresentation(GameInstance gameAPI, MapChunkId mapId)
    {
        if (!gameAPI.Databases.TryResolve(mapId, out MapChunk map))
            return;

        MapId = mapId;
        Width = map.Width;
        Height = map.Height;

        List<MapTilePresentation> tiles = new();
        foreach (var tile in map.GetAllTiles())
        {
            tiles.Add(new MapTilePresentation(tile));
        }

        List<MapPropPlacementPresentation> props = new();
        List<MapItemPlacementPresentation> items = new();
        List<MapCharacterPlacementPresentation> characters = new();

        var allPlacements = map.GetAllPlacements();
        foreach (var placement in allPlacements)
        {
            switch (placement)
            {
                case PropPlacementOnMap propPlacement:
                    props.Add(gameAPI.Maps.CreatePropPlacementPresentation(mapId, propPlacement.PropId));
                    break;
                case ItemPlacementOnMap itemPlacement:
                    items.Add(gameAPI.Maps.CreateItemPlacementPresentation(mapId, itemPlacement.ItemId));
                    break;
                case CharacterPlacementOnMap characterPlacement:
                    characters.Add(gameAPI.Maps.CreateCharacterPlacementPresentation(mapId, characterPlacement.CharacterId));
                    break;
                default:
                    break;
            }
        }

        Tiles = tiles;
        Props = props;
        Items = items;
        Characters = characters;
    }
}