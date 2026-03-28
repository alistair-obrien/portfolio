using System;
using System.Collections.Generic;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

public record PropBlueprint : IBlueprint
{
    IGameDbId IBlueprint.Id => Id;

    public string TypeId => "Prop";

    [HideInEditor] public PropId Id;
    string IBlueprint.Name { get => Name; set => Name = value; }
    public string Name;
    [MultipleLineText(3)] public string Description;
    public string FlavorText;
    [ForceNotNull] public RenderKey RenderKey;
    public CellSize SizeOnMap;
    public List<PropLayerSaveData> Layers = new();

    public PropBlueprint()
    {
        Id = PropId.New();
        RenderKey = new RenderKey("");
    }
}

public class Prop : Entity<PropBlueprint>, 
    IMapPlaceable,
    IGameDbResolvable,
    IHasGameDbResolvableReferences
{
    protected override string TypeId => "Prop";
    protected override int Version => 1;

    IGameDbId IMapPlaceable.Id => Id;
    IGameDbId IGameDbResolvable.Id => Id;
    IDbId IDbResolvable.Id => Id;

    IGameModelLocation IGameDbResolvable.AttachedLocation => AttachedLocation;
    public IPropLocation AttachedLocation { get; private set; }

    List<IGameDbId> IHasGameDbResolvableReferences.GetChildIdReferences() => new List<IGameDbId>();
    void IHasGameDbResolvableReferences.RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap) => Id = (PropId)idMap[Id];

    void IGameDbResolvable.ClearAttachedLocation()
    {
        AttachedLocation = null;
    }

    void IGameDbResolvable.SetAttachedLocation(IGameModelLocation targetLocation)
    {
        if (targetLocation is not IPropLocation propLocation)
            return;

        AttachedLocation = propLocation;
    }

    public override PropBlueprint SaveToBlueprint()
    {
        return new PropBlueprint
        {
            Id = Id,
            Name = Name,
            Description = Description,
            FlavorText = FlavorText,
            RenderKey = RenderKey,
            SizeOnMap = SizeOnMap,
            Layers = PropLayers.ConvertAllToList(l => l.Save())
        };
    }

    // Identity
    public PropId Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string FlavorText { get; private set; }
    public RenderKey RenderKey { get; private set; }
    public CellSize SizeOnMap { get; private set; }
    public List<PropLayer> PropLayers { get; private set; }

    public Prop() 
    { 
        Id = PropId.New();
        Name = "Prop";
        SizeOnMap = new CellSize(1,1);
        RenderKey = new("");
        PropLayers = new();
    }

    public Prop(PropId propId)
    {
        Id = propId;
    }

    public bool GetIsDestroyed()
    {
        if (PropLayers == null || PropLayers.Count == 0)
            return true;

        foreach (var layer in PropLayers)
        {
            if (!layer.GetIsDestroyed())
                return false;
        }

        return true;
    }

    internal bool TryGetLayer(PropLayerId layerId, out PropLayer layer)
    {
        layer = PropLayers.FirstOrDefault((l) => l.Id == layerId);
        return layer != null;
    }

    internal float GetHPPercentage()
    {
        if (GetMaxHP() == 0) { return 0; }
        return (float)GetCurrentHP() / (float)GetMaxHP();
    }

    public int GetCurrentHP()
    {
        return GetCurrentCyberneticHP() + GetCurrentOrganicHP() + GetCurrentStructuralHP();
    }

    internal int GetMaxHP()
    {
        return GetMaxStructuralHP() + GetMaxCyberneticHP() + GetMaxOrganicHP();
    }

    public int GetCurrentStructuralHP()
    {
        return PropLayers.Sum(layer => layer.GetCurrentHP(DamageDomain.Structural));
    }

    public int GetMaxStructuralHP()
    {
        return PropLayers.Sum(layer => layer.GetMaxHP(DamageDomain.Structural));
    }

    public int GetCurrentCyberneticHP()
    {
        return PropLayers.Sum(layer => layer.GetCurrentHP(DamageDomain.Cybernetic));
    }

    public int GetMaxCyberneticHP()
    {
        return PropLayers.Sum(layer => layer.GetMaxHP(DamageDomain.Cybernetic));
    }

    public int GetCurrentOrganicHP()
    {
        return PropLayers.Sum(layer => layer.GetCurrentHP(DamageDomain.Organic));
    }

    public int GetMaxOrganicHP()
    {
        return PropLayers.Sum(layer => layer.GetMaxHP(DamageDomain.Organic));
    }

    public IReadOnlyList<ResistanceData> GetResistances()
    {
        var resistances = new List<ResistanceData>();

        foreach (var layer in PropLayers)
        {
            if (layer.GetIsDestroyed()) continue;
            resistances.AddRange(layer.Resistances);
        }

        return resistances;
    }

    public CellFootprint GetFootprintAt(int x, int y)
    {
        return new CellFootprint(
            new CellPosition(x,y),
            SizeOnMap
        );
    }

    public IEnumerable<CellPosition> GetBlockedCells(MapChunk map, int x, int y)
    {
        var footprint = GetFootprintAt(x, y);

        foreach (var cell in footprint.Cells())
        {
            if (!map.InBounds(cell.X, cell.Y) ||
                map.HasPlacementAt(cell))
            {
                yield return cell;
            }
        }
    }

    public void ReattachReferences(IGameDbResolvable originalModel)
    {
    }

    public IEnumerable<IGameDbId> GetAttachedEntityIds()
    {
        yield break;
    }

    public IEnumerable<IGameDbId> GetReferencedEntityIds()
    {
        yield break;
    }

    public void AttachEntities(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        //TODO
    }

    public void ApplyBlueprint(IBlueprint blueprint)
    {
        if (blueprint is not PropBlueprint data)
            return;

        Id = data.Id;
        Name = data.Name;
        Description = data.Description;
        FlavorText = data.FlavorText;
        RenderKey = data.RenderKey;
        SizeOnMap = data.SizeOnMap;
        PropLayers = data.Layers.ConvertAllToList(x => new PropLayer(x));
    }

    public IEnumerable<AttachmentChange> GetAttachmentChanges(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict)
    {
        yield break;
    }
}