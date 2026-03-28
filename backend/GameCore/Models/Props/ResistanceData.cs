using System;

public record ResistanceDataSaveData
{
    public string DamageType;
    public int Resistance;

    public ResistanceDataSaveData()
    {

    }

    public ResistanceDataSaveData(ResistanceData resistance)
    {
        DamageType = resistance.DamageType.Id; 
        Resistance = resistance.Resistance;
    }
}

public class ResistanceData
{
    public DamageType DamageType;
    public int Resistance;

    public ResistanceDataSaveData Save()
    {
        return new ResistanceDataSaveData(this);
    }

    public ResistanceData() { }
    public ResistanceData(DamageType damageType, int resistance)
    {
        DamageType = damageType;
        Resistance = resistance;
    }

    public ResistanceData(ResistanceDataSaveData data)
    {
        DamageType = DamageType.FromId(data.DamageType);
        Resistance = Math.Max(0, data.Resistance);
    }
}