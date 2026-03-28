//using UnityEngine;

//// TODO: This may need to turn into an interaction sink rather than adapter
//public sealed class MapEditorWorldInteractionAdapter
//    : IWorldCommands, IWorldQueries
//{
//    private readonly IMapEditorQueries _editorQueries;
//    private readonly IMapEditorCommands _editorCommands;

//    public MapEditorWorldInteractionAdapter(
//        IMapEditorQueries editorQueries,
//        IMapEditorCommands editorCommands)
//    {
//        _editorQueries = editorQueries;
//        _editorCommands = editorCommands;
//    }

//    public void RequestPrimaryInteraction(IGameModelLocation location)
//    {
//        if (location is not MapLocation mapLocation)
//            return;

//        Debug.Log("Request Primary");
//        // If holding something → place
//        if (_editorQueries.HasStaged())
//        {
//            _editorCommands.TryPlaceStagedAt(mapLocation.CellFootprint.Position.X, mapLocation.CellFootprint.Position.Y);
//            return;
//        }

//        Debug.Log("Stage From Map");
//        _editorCommands.StageFromCell(mapLocation.CellFootprint.Position.X, mapLocation.CellFootprint.Position.Y);
//    }

//    public void RequestSecondaryInteraction(IGameModelLocation location)
//    {
//        if (location is not MapLocation mapLocation)
//            return;

//        // Secondary click = context menu or unstage
//        if (_editorQueries.HasStaged())
//        {
//            _editorCommands.Unstage();
//            return;
//        }

//        // Otherwise, let editor decide (context menu logic already exists)
//        _editorCommands.ProcessActionOnCell(mapLocation.CellFootprint.Position.X, mapLocation.CellFootprint.Position.Y, "open-context");
//    }

//    public void ChangeHoveredCell(IGameModelLocation location)
//    {
//        if (location is MapLocation mapLocation)
//            _editorCommands.SetHoveredCell(mapLocation.CellFootprint.Position);
//    }

//    public void ClearHoveredLocation()
//    {
//        _editorCommands.ClearHoveredCell();
//    }


//    public bool CanInteractWithMaps() => true;

//    public bool IsTileValid(int x, int y)
//    {
//        //TODO
//        return true;

//        //var map = _editorQueries.GetWorkingMapId();

//        //if (map == null)
//        //    return false;

//        //if (!map.InBounds(x, y))
//        //    return false;

//        //return true;
//    }

//    public bool TryGetPlayerCharacterPosition(out MapCharacterPlacementPresentation presentation)
//    {
//        presentation = null;
//        return false;
//    }

//    public bool TryGetEntityAtPosition(CellPosition cell, out IGridPlacement placement)
//    {
//        placement = default;
//        return false;
//    }

//    public void SubmitIntent(Intents.IIntent intent)
//    {
//        //Debug.Log(intent);
//        switch (intent)
//        {
//            case Intents.GameEntityLocationHoverEnterIntent h:
//                    ChangeHoveredCell(h.TargetEntityLocation);
//                break;
//            case Intents.GameEntityLocationHoverExitIntent h:
//                    ClearHoveredLocation();
//                break;
//            case Intents.LocationPrimaryCommit p:
//                    RequestPrimaryInteraction(p.TargetEntityLocation);
//                break;
//            case Intents.LocationSecondaryCommit p:
//                    RequestSecondaryInteraction(p.TargetEntityLocation);
//                break;
//            default:
//                break;
//        }
//    }

//    public bool TryGetPlayerCharacterLocationPresentation(out MapCharacterPlacementPresentation presentation)
//    {
//        presentation = default;
//        return false;
//    }

//    public bool TryGetPlayerCharacterLocation(out ICharacterLocation characterLocation)
//    {
//        characterLocation = default;
//        return false;
//    }

//    public bool IsValidMapLocation(MapLocation location)
//    {
//        //TODO
//        return true;
//        //return _editorQueries.GetWorkingMapId().InBounds(location);
//    }

//    public bool TryGetCurrentMap(out MapChunkId currentMapId)
//    {
//        currentMapId = _editorQueries.GetWorkingMapId();
//        return true;
//    }
//}
