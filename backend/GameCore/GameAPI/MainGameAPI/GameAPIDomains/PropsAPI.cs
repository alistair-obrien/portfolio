public sealed class PropsAPI : APIDomain
{
    public PropsAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    internal override void RegisterHandlers(CommandRouter router)
    {

    }

    //public bool InspectWorldObject(InspectWorldObjectRequest request)
    //{
    //    if (!GameAPI.Databases.TryGetModel<Character>(request.SelfUid, out var self)) { return false; }
    //    if (!GameAPI.Databases.TryGetModel<MapChunk>(request.MapUid, out var map)) { return false; }
    //    if (!map.TryGetPropPlacement(request.worldObjectLocalId, out var placement)) { return false; }

    //    GameAPI.RaiseEvent(new InspectedWorldObject(placement.Prop));
    //    return true;
    //}

    //public bool TryGetWorldObjectDTO(
    //    string mapUid, 
    //    string localId, 
    //    out WorldObjectHoverPreviewDTO worldObjectDTO)
    //{
    //    worldObjectDTO = default;
    //    if (!GameModel.TryResolveModel<LocalMap>(mapUid, out var localMap)) { return false; }
    //    if (!localMap.TryGetWorldObjectPlacement(localId, out var placement)) { return false; }

    //    WorldObject worldObject = placement.WorldObject;

    //    worldObjectDTO = new WorldObjectHoverPreviewDTO(
    //        localId,
    //        worldObject.Name,
    //        worldObject.Description,
    //        worldObject.FlavorText,
    //        Rulebook.InjuriesAndDamageSection.GetMinEnergyMitigationOfWorldObject(worldObject),
    //        Rulebook.InjuriesAndDamageSection.GetMaxEnergyMitigationOfWorldObject(worldObject),
    //        Rulebook.InjuriesAndDamageSection.GetHealthSummaryOfWorldObject(worldObject),
    //        Rulebook.InjuriesAndDamageSection.GetResistancesSummaryOfWorldObject(worldObject)
    //        );

    //    return true;
    //}

    //internal bool TryGetProp(string worldObjectLocalId, out Prop worldObject)
    //{
    //    worldObject = default;

    //    foreach (var map in GameModel.LoadedMaps)
    //    {


    //        if (map.TryGetPropPlacement(worldObjectLocalId, out var placement))
    //        {
    //            worldObject = placement.Prop;
    //            return true;
    //        }
    //    }

    //    return false;
    //}
}