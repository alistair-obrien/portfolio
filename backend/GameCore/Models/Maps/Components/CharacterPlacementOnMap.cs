using Newtonsoft.Json;
using System.Collections.Generic;

/// <summary>
/// Represents the authoritative placement of a character on a grid.
/// XPos and YPos represent the TOP-LEFT anchor of the character footprint.
/// The occupied cells are derived from Character.Width and Character.Height.
/// </summary>
public class CharacterPlacementOnMap : BaseEntity, 
    IMapPlacement,
    IHasGameDbResolvableReferences
{
    public CharacterId CharacterId { get; private set; }
    public CellFootprint Footprint { get; }

    public IGameDbId Id => CharacterId;

    public MapPlacementSaveData Save() => new MapPlacementSaveData(this);

    public CharacterPlacementOnMap(CharacterId characterId, CellFootprint footprint)
    {
        CharacterId = characterId;
        Footprint = footprint;
    }

    public IMapPlacement WithFootprint(CellFootprint newFootprint)
    {
        return new CharacterPlacementOnMap(CharacterId, newFootprint);
    }

    public List<IGameDbId> GetChildIdReferences()
    {
        return new List<IGameDbId> { CharacterId };
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        if (idMap.TryGetValue(Id, out ITypedStringId newId))
        {
            CharacterId = (CharacterId)newId;
        }
    }
}