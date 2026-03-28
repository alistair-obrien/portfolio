
public sealed partial class MapsAPI
{
    public class Commands
    {
        // MAPS
        //public record LoadMap(
        //    MapChunkId MapId
        //) : IGameCommand;

        public sealed record CreateMap(
            CharacterId ActorId,
            MapChunkId MapId,
            int Width,
            int Height
        ) : IGameCommand;

        public sealed record DestroyMap(
            CharacterId ActorId,
            MapChunkId MapId
        ) : IGameCommand;

        public sealed record ResizeMap(
            CharacterId ActorId,
            MapChunkId MapId,
            int NewWidth,
            int NewHeight
        ) : IGameCommand;

        public sealed record ClearMap(
            CharacterId ActorId,
            MapChunkId MapId
        ) : IGameCommand;

        // GENERIC
        public sealed record AddEntityToMap(
            CharacterId ActorId,
            MapChunkId MapId,
            IGameDbId EntityId,
            CellPosition CellPosition
            ) : IGameCommand;

        // CHARACTERS
        public sealed record AddCharacterToMap(
            CharacterId ActorId,
            MapChunkId MapId,
            CharacterId CharacterId,
            int TileX,
            int TileY
        ) : IGameCommand;

        public sealed record MoveCharacterAlongPathToCell(
            CharacterId ActorId,
            MapChunkId MapId,
            CharacterId CharacterId,
            int ToX,
            int ToY
        ) : IGameCommand;

        public sealed record MoveCharacterOneStepRelative(
            CharacterId ActorId,
            CharacterId CharacterId,
            int DirectionX,
            int DirectionY
        ) : IGameCommand;

        public sealed record MoveCharacterDirectlyToCell(
            CharacterId ActorId,
            MapChunkId MapId,
            CharacterId CharacterId,
            int ToX,
            int ToY
        ) : IGameCommand;

        public sealed record RemoveCharacterFromMap(
            CharacterId ActorId,
            MapChunkId MapId,
            CharacterId CharacterId
        ) : IGameCommand;

        // PROPS
        public sealed record AddPropToMap(
            CharacterId ActorId,
            MapChunkId MapId,
            PropId PropId,
            int TileX,
            int TileY
        ) : IGameCommand;

        public sealed record MovePropAlongPathToCell(
            CharacterId ActorId,
            MapChunkId MapId,
            PropId PropId,
            int ToX,
            int ToY
        ) : IGameCommand;

        public sealed record MovePropOneStepRelative(
            CharacterId ActorId,
            PropId PropId,
            int DirectionX,
            int DirectionY
        ) : IGameCommand;

        public sealed record MovePropDirectlyToCell(
            CharacterId ActorId,
            MapChunkId MapId,
            PropId PropId,
            int ToX,
            int ToY
        ) : IGameCommand;

        public sealed record RemovePropFromMap(
            CharacterId ActorId,
            MapChunkId MapId,
            PropId PropId
        ) : IGameCommand;

        // ITEMS
        public sealed record AddItemToMap(
            CharacterId ActorId,
            MapChunkId MapId,
            ItemId ItemId,
            int TileX,
            int TileY
        ) : IGameCommand;

        public sealed record MoveItemAlongPathToCell(
            CharacterId ActorId,
            MapChunkId MapId,
            ItemId ItemId,
            int ToX,
            int ToY
        ) : IGameCommand;

        public sealed record MoveItemOneStepRelative(
            CharacterId ActorId,
            ItemId ItemId,
            int DirectionX,
            int DirectionY
        ) : IGameCommand;

        public sealed record MoveItemDirectlyToCell(
            CharacterId ActorId,
            MapChunkId MapId,
            ItemId ItemId,
            int ToX,
            int ToY
        ) : IGameCommand;

        public sealed record RemoveItemFromMap(
            CharacterId ActorId,
            MapChunkId MapId,
            ItemId ItemId
        ) : IGameCommand;

        public sealed record RemovePlacementFromMap(
            CharacterId ActorId,
            MapChunkId MapId,
            IGameDbId GameDbId
            ) : IGameCommand;

        //// TILES - NOT IMPLEMENTED
        //public sealed record SetTileType(
        //    MapChunkId MapId,
        //    int TileX,
        //    int TileY,
        //    string TileTypeUid
        //) : IGameCommand;

        //public sealed record SetTileBlocked(
        //    MapChunkId MapUid,
        //    int TileX,
        //    int TileY,
        //    bool IsBlocked
        //) : IGameCommand;

        //public sealed record SetTileProperty(
        //    MapChunkId MapUid,
        //    int TileX,
        //    int TileY,
        //    string PropertyName,
        //    object PropertyValue
        //) : IGameCommand;
    }
}