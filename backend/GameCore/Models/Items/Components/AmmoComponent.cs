using System;

public record AmmoSaveData
{
    public string DamageType;
    public int BaseEnergy;

    public AmmoSaveData()
    {
        DamageType = global::DamageType.Impact.Id;
        BaseEnergy = 0;
    }

    public AmmoSaveData(AmmoComponent ammoComponent)
    {
        DamageType = ammoComponent.DamageType.Id;
        BaseEnergy = ammoComponent.BaseEnergy;
    }
}

public class AmmoComponent : ItemComponent
{
    public AmmoSaveData Save()
    {
        return new AmmoSaveData(this);
    }

    public DamageType DamageType { get; private set; }
    public int BaseEnergy { get; private set; }

    public AmmoComponent() 
    {

    }

    public AmmoComponent(AmmoSaveData ammo)
    {
        DamageType = DamageType.FromId(ammo.DamageType);
        BaseEnergy = Math.Max(0, ammo.BaseEnergy);
    }

    internal string GetSubtitle()
    {
        return $"Ammunition";
    }
}
