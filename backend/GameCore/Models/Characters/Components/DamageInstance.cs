using System;

public class DamageInstanceSaveData
{
    public string DamageDomain;
    public string DamageType;
    public int Severity;

    public DamageInstanceSaveData(DamageInstance resistance)
    {
        DamageDomain = resistance.DamageDomain.Id;
        DamageType = resistance.DamageType.Id;
        Severity = resistance.DamageSeverity;
    }

    public DamageInstanceSaveData()
    {

    }
}

public class DamageInstance
{
    internal DamageInstanceSaveData Save() => new DamageInstanceSaveData(this);

    public DamageDomain DamageDomain { get; private set; }
    public DamageType DamageType { get; private set; }
    public int DamageSeverity { get; private set; }
    
    public DamageInstance(DamageInstanceSaveData data)
    {
        DamageDomain = DamageDomain.FromId(data.DamageDomain);
        DamageType = DamageType.FromId(data.DamageType);
        DamageSeverity = Math.Max(1, data.Severity);
    }

    public DamageInstance(DamageDomain damageDomain, DamageType damageType, int damageSeverity)
    {
        DamageDomain = damageDomain;
        DamageType = damageType;
        DamageSeverity = damageSeverity;
    }


}
