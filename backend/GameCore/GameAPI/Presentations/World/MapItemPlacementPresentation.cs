public class MapItemPlacementPresentation
{
    public readonly ItemId ItemId;
    public readonly CellFootprint CellFootprint;
    public readonly ItemPresentation Presentation;

    public MapItemPlacementPresentation(GameInstance gameAPI, ItemPlacementOnMap placement)
    {
        ItemId = placement.ItemId;
        CellFootprint = placement.Footprint;
        Presentation = new ItemPresentation(gameAPI, ItemId);
    }
}
