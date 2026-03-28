using System;

public sealed partial class MapsAPI
{
    public class Events
    {
        // MAPS
        public sealed record MapCreated(
            MapChunkId MapId,
            MapPresentation Presentation
        ) : IGameEvent;

        public sealed record MapLoaded(
            MapChunkId MapId,
            MapPresentation Presentation
        ) : IGameEvent;

        public sealed record MapDestroyed(
            MapChunkId MapId
        ) : IGameEvent;

        public sealed record MapResized(
            MapChunkId MapId,
            int NewWidth,
            int NewHeight
        ) : IGameEvent;

        public sealed record MapCleared(
            MapChunkId MapId
        ) : IGameEvent;

        // CHARACTERS
        public sealed record CharacterAddToMapResolved(
            bool Success,
            MapChunkId MapId,
            MapCharacterPlacementPresentation Presentation
        ) : IGameEvent;

        public sealed record CharacterMovedOnMap(
            MapChunkId MapId,
            CharacterId CharacterId,
            MapCharacterPlacementPresentation Presentation,
            MoveResult MoveResult
        ) : IGameEvent;

        public sealed record CharacterRemovedFromMap(
            MapChunkId MapId,
            CharacterId CharacterId
        ) : IGameEvent;

        // PROPS
        public sealed record PropAddToMapResolved(
            bool Success,
            MapChunkId MapId,
            MapPropPlacementPresentation Presentation
        ) : IGameEvent;

        public sealed record PropMovedOnMap(
            MapChunkId MapId,
            PropId PropId,
            MapPropPlacementPresentation Presentation,
            MoveResult MoveResult
        ) : IGameEvent;

        public sealed record PropRemovedFromMap(
            MapChunkId MapId,
            PropId PropId
        ) : IGameEvent;

        // For when a neighbor changes
        public sealed record PropUpdatedOnMap(
            MapChunkId MapId,
            PropId PropId,
            MapPropPlacementPresentation Presentation
            ) : IGameEvent;

        // ITEMS
        public sealed record ItemAddToMapResolved(
            bool Success,
            MapChunkId MapId,
            MapItemPlacementPresentation Presentation
        ) : IGameEvent;

        public sealed record ItemMovedOnMap(
            MapChunkId MapId,
            ItemId ItemId,
            MapItemPlacementPresentation Presentation,
            MoveResult MoveResult
        ) : IGameEvent;

        public sealed record ItemRemovedFromMap(
            MapChunkId MapId,
            ItemId ItemId
        ) : IGameEvent;

        //// TILES - NOT IMPLEMENTED
        //public sealed record TileTypeChanged(
        //    MapChunkId MapId,
        //    int TileX,
        //    int TileY,
        //    string TileTypeUid
        //) : IGameEvent;

        //public sealed record TileBlockedChanged(
        //    MapChunkId MapId,
        //    int TileX,
        //    int TileY,
        //    bool IsBlocked
        //) : IGameEvent;

        //public sealed record TilePropertyChanged(
        //    MapChunkId MapId,
        //    int TileX,
        //    int TileY,
        //    string PropertyName,
        //    object PropertyValue
        //) : IGameEvent;
    }
}