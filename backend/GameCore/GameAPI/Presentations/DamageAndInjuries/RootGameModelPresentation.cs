using System.Collections.Generic;
using System.Linq;

public class RootGameModelPresentation
{
    public readonly IReadOnlyList<MapPresentation> Maps;
    public readonly PlayerInteractionPresentation PlayerHUD;

    public RootGameModelPresentation(GameInstance gameAPI, Root rootModel)
    {
        Maps = gameAPI.Databases
            .GetAllModels()
            .OfType<MapChunk>()
            .OrderBy(map => map.Id.Value)
            .Select(map => new MapPresentation(gameAPI, map.Id))
            .ToList();

        if (rootModel.PlayerCharacterId.HasValue)
        {
            if (gameAPI.Databases.TryResolve(rootModel.PlayerCharacterId.Value, out Character character))
                PlayerHUD = new PlayerInteractionPresentation(character);
        }
    }
}
