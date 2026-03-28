using System.Collections.Generic;
using System.Linq;

// TODO: Make a string map like Damage Type and Damage Domain
public enum ModFrameCoverage
{ 
    None,
    Subtle,
    Prominent,
    Excessive,
    FullReplacement
}

public class ModFrameSaveData
{
    public List<CyberneticSlotId> CompaptibleSlotIds;
    public ModFrameCoverage ModFrameCoverage; 
    public int MaxDamageCapacity;
    public float CyberSecurityLevel;
    public List<ItemId?> InstalledMods;
    public List<DamageInstanceSaveData> Damage;

    public ModFrameSaveData()
    {
        CompaptibleSlotIds = new();
        InstalledMods = new();
        Damage = new();
    }
}

public class ModFrameComponent : ItemComponent
{
    public ModFrameSaveData Save()
    {
        return new ModFrameSaveData
        {
            CompaptibleSlotIds = CompatibleSlotIds,
            ModFrameCoverage = ModFrameCoverage,
            MaxDamageCapacity = MaxDamageCapacity,
            CyberSecurityLevel = CyberSecurityLevel,
            InstalledMods = Mods,
            Damage = Damage.ConvertAll(x => new DamageInstanceSaveData(x))
        };
    }

    public List<CyberneticSlotId> CompatibleSlotIds { get; private set; }
    public ModFrameCoverage ModFrameCoverage { get; private set; }
    public int MaxDamageCapacity { get; private set; }
    public float CyberSecurityLevel { get; private set; }
    public List<ItemId?> Mods { get; private set; }
    private List<DamageInstance> Damage = new();

    public ModFrameComponent() { }
    public ModFrameComponent(ModFrameSaveData data) 
    {
        CompatibleSlotIds = data.CompaptibleSlotIds;
        ModFrameCoverage = data.ModFrameCoverage;
        MaxDamageCapacity = data.MaxDamageCapacity;
        Damage = data.Damage.ConvertAll(damageData => new DamageInstance(damageData));
        CyberSecurityLevel = data.CyberSecurityLevel;
        Mods = new List<ItemId?>(); // We just initialize this because we will be attaching in the next step after all base entities are created
    }
    public ModFrameComponent(
        List<CyberneticSlotId> cyberneticBodySlotPath,
        ModFrameCoverage modFrameCapacity,
        int maxDamagePoints,
        List<DamageInstance> damage,
        float cyberSecurityLevel,
        List<ItemId?> mods) : base()
    {
        CompatibleSlotIds = cyberneticBodySlotPath;
        ModFrameCoverage = modFrameCapacity;
        MaxDamageCapacity = maxDamagePoints;
        Damage = damage;
        CyberSecurityLevel = cyberSecurityLevel;
        Mods = mods;
    }

    public bool TryInstallMod(ItemId itemId, int slotIndex)
    {
        //if (!itemId.GetIsMod()) { return false; }

        if (Mods[slotIndex] == null)
        {
            Mods[slotIndex] = itemId;
            return true;
        }
        else
        {
            return false;
        }
    }

    public float GetConditionPercent() => (float)GetCurrentDamagePoints() / MaxDamageCapacity;

    public int GetCurrentDamagePoints()
    {
        return Damage.Sum(x => x.DamageSeverity);
    }

    public int GetRemainingDamageCapacity()
    {
        return MaxDamageCapacity - GetCurrentDamagePoints();
    }

    public int GetMaxDamageCapacity()
    {
        return MaxDamageCapacity;
    }

    public int GetModCapacity()
    {
        return Mods.Count;
    }

    internal bool TryApplyDamage(DamageInstance damage)
    {
        //if (ParentItemId != damage.ItemUid) { return false; }

        // Has space for more injuries
        if (GetRemainingDamageCapacity() >= damage.DamageSeverity)
        {
            Damage.Add(damage);
            return true;
        }
        // Destroy item
        else
        {
            // WE DEAD SON
            return false;
        }
    }

    public IReadOnlyList<DamageInstance> GetDamage() => Damage;

    public HashSet<ISlotId> GetCompatibleSlotPaths()
    {
        if (CompatibleSlotIds.Contains(new CyberneticSlotId("character.cyber.internal"))) //HACK
        {
            return new HashSet<ISlotId>
            {
                SlotIds.Cybernetic.Internal1,
                SlotIds.Cybernetic.Internal2,
                SlotIds.Cybernetic.Internal3,
                SlotIds.Cybernetic.Internal4,
                SlotIds.Cybernetic.Internal5,
            };
        }

        HashSet<ISlotId> compatibleHashset = new HashSet<ISlotId>();

        foreach (var item in CompatibleSlotIds)
        {
            compatibleHashset.Add(item);
        } 
        return compatibleHashset;
    }

    //TODO: The Mod frame doesnt know this, and shouldnt know this.
    // This belongs in the rules layer where it knows where this is actually installed to
    public IEnumerable<AnatomySlotId> GetCoveredBodyParts()
    {
        return new List<AnatomySlotId> { };
        //return CyberneticsComponent.GetCoveredBodyparts(CompatibleSlotIds);
    }
}