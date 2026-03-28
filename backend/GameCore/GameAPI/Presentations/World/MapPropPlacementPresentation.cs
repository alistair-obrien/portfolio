public class MapPropPlacementPresentation
{
    public readonly PropId Id;
    public readonly CellFootprint CellFootprint;
    public readonly RenderKey RenderKey;

    public readonly float HealthPercentage;

    // Wall neighbors
    public readonly RenderKey NorthRenderKey;
    public readonly RenderKey SouthRenderKey;
    public readonly RenderKey EastRenderKey;
    public readonly RenderKey WestRenderKey;

    public MapPropPlacementPresentation(
            GameInstance gameAPI,
            PropPlacementOnMap placement,
            RenderKey north,
            RenderKey south,
            RenderKey east,
            RenderKey west)
    {
        if (!gameAPI.Databases.TryResolve(placement.PropId, out Prop prop))
            return;

        Id = placement.PropId;
        CellFootprint = placement.Footprint;

        RenderKey = prop.RenderKey;

        NorthRenderKey = north;
        SouthRenderKey = south;
        EastRenderKey = east;
        WestRenderKey = west;

        HealthPercentage = prop.GetHPPercentage();
    }
}
