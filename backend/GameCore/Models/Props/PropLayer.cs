using System;
using System.Collections.Generic;
using System.Linq;

public record PropLayerSaveData
{
    public PropLayerId Id;
    public string Name;
    public string DamageDomain;
    public int MaxIntegrity;
    public int RicochetTransfer;
    public List<ResistanceDataSaveData> Resistances;
    public List<DamageInstanceSaveData> DamageInstances;
    public RenderKey RenderKey;

    public PropLayerSaveData() 
    { 
        Id = PropLayerId.New();
        Name = "Name";
        DamageDomain = global::DamageDomain.Structural.Id;
        Resistances = new();
        DamageInstances = new();
        RenderKey = new("");
    }
    public PropLayerSaveData(PropLayer layer)
    {
        Id = layer.Id;
        Name = layer.Name;
        DamageDomain = layer.DamageDomain.Id;
        MaxIntegrity = layer.MaxIntegrity;
        RicochetTransfer = layer.RicochetTransfer;
        Resistances = layer.Resistances?.ConvertAllToList(resistance => resistance.Save())?? new();
        DamageInstances = layer.DamageInstances?.ConvertAllToList(damageInstance => damageInstance.Save())?? new();
        RenderKey = layer.RenderKey;
    }
}

public class PropLayer : BaseEntity
{
    public PropLayerId Id { get; private set; }
    public string Name { get; private set; }
    public DamageDomain DamageDomain { get; private set; }
    public int MaxIntegrity { get; private set; }
    public int RicochetTransfer { get; private set; }
    public List<ResistanceData> Resistances { get; set; } = new();
    public List<DamageInstance> DamageInstances { get; set; } = new();
    public RenderKey RenderKey { get; private set; }

    internal PropLayerSaveData Save() => new PropLayerSaveData(this);

    public PropLayer() { }
    public PropLayer(
        PropLayerId id,
        string name,
        DamageDomain damageDomain,
        int maxIntegrity,
        int ricochetTransfer,
        List<ResistanceData> resistances,
        List<DamageInstance> damageInstances,
        RenderKey renderKey)
    {
        if (!id.IsValid)
            throw new ArgumentException("Invalid id", nameof(id));

        Id = id;

        Name = name;
        DamageDomain = damageDomain;
        MaxIntegrity = maxIntegrity;
        RicochetTransfer = ricochetTransfer;
        Resistances = new List<ResistanceData>(resistances ?? Array.Empty<ResistanceData>().ToList());
        DamageInstances = new List<DamageInstance>(damageInstances ?? Array.Empty<DamageInstance>().ToList());
        RenderKey = renderKey;
    }

    public PropLayer(PropLayerSaveData data)
    {
        Id = data.Id;

        Name = data.Name;
        DamageDomain = DamageDomain.FromId(data.DamageDomain);
        MaxIntegrity = data.MaxIntegrity;
        RicochetTransfer = data.RicochetTransfer;
        Resistances = data.Resistances?.ConvertAllToList(x => new ResistanceData(x)) ?? new(); 
        DamageInstances = data.DamageInstances?.ConvertAllToList(x => new DamageInstance(x)) ?? new();
        RenderKey = data.RenderKey;
    }

    internal bool GetIsDestroyed()
    {
        int total = 0;
        foreach (var damageInstance in DamageInstances)
        {
            total += damageInstance.DamageSeverity;
        }

        return total >= MaxIntegrity;
    }

    internal int GetCurrentHP(DamageDomain damageDomain)
    {
        int total = 0;
        foreach (var damageInstance in DamageInstances)
        {
            if (damageInstance.DamageDomain == damageDomain)
                total += damageInstance.DamageSeverity;
        }

        return Mathf.Max(GetMaxHP(damageDomain) - total, 0);
    }

    internal int GetMaxHP(DamageDomain damageDomain)
    {
        return DamageDomain == damageDomain ? MaxIntegrity : 0;
    }
}