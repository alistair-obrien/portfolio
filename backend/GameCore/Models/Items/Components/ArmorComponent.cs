using System.Collections.Generic;

public record ArmorBlueprint
{
    public List<ResistanceDataSaveData> Resistances;

    public ArmorBlueprint()
    {
        Resistances = new();
    }
};

public class ArmorComponent : ItemComponent
{
    public ArmorBlueprint Save()
    {
        return new ArmorBlueprint
        {
            Resistances = _resistances.ConvertAll(r => r.Save())
        };
    }

    private readonly List<ResistanceData> _resistances = new();
    public IReadOnlyList<ResistanceData> Resistances => _resistances;

    //TODO: Make below obsolete
    public int PiercingResistance { get; private set; } // Thinking maybe to use spread to turn some guns into AoE combined with RoF
    public int ImpactResistance { get; private set; } 
    public int SlashingResistance { get; private set; }

    public ArmorComponent() { }
    public ArmorComponent(List<ResistanceData> resistances)
        : base()
    {
        _resistances = resistances;
    }

    public ArmorComponent(ArmorBlueprint armor)
    {
        _resistances = armor.Resistances.ConvertAll(r => new ResistanceData(r));
    }

    internal HashSet<ISlotId> GetCompatibleSlotPaths()
    {
        return new HashSet<ISlotId>
        {
            SlotIds.Loadout.Armor,
        };
    }
}
