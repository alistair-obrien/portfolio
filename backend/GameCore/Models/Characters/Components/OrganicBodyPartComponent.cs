using System.Collections.Generic;
using System.Linq;

public class OrganicBodyPartSaveData
{
    public string Name;
    public string Description;
    public int MaxOrganicInjuryCapacity;
    public List<DamageInstanceSaveData> OrganicInjuries;
}

public class OrganicBodyPartComponent : BaseEntity
{
    internal OrganicBodyPartSaveData Save()
    {
        return new OrganicBodyPartSaveData
        {
            Name = Name,
            Description = Description,
            MaxOrganicInjuryCapacity = MaxOrganicInjuryCapacity,
            OrganicInjuries = Injuries.Select(x => x.Save()).ToList()
        };
    }

    public string Name { get; private set; }
    public string Description { get; private set; }
    public int MaxOrganicInjuryCapacity { get; private set; }

    private List<DamageInstance> Injuries = new();

    public int GetCurrentInjuryPoints() 
    {
        return Injuries.Sum(x => x.DamageSeverity);
    }

    public OrganicBodyPartComponent(OrganicBodyPartSaveData data)
    {
        var injuries = data.OrganicInjuries
            .Select(x => new DamageInstance(x))
            .ToList();

        Name = data.Name;
        Description = data.Description;
        MaxOrganicInjuryCapacity = Mathf.Max(0, data.MaxOrganicInjuryCapacity);
        Injuries = injuries;
    }

    // TODO: Probably get rid of this
    public OrganicBodyPartComponent(
        string name, 
        string description,
        int maxInjuryPoints,
        List<DamageInstance> injuries)
    {
        Name = name;
        Description = description;
        MaxOrganicInjuryCapacity = maxInjuryPoints;
        Injuries = injuries;
    }

    public void Tick()
    {
        //if (_isBleeding) { _currentHealth -= 1; }
    }

    public bool GetIsDestroyed()
    {
        return GetRemainingInjuryCapacity() <= 0;
    }

    public int GetRemainingInjuryCapacity()
    {
        return Mathf.Clamp(MaxOrganicInjuryCapacity - GetCurrentInjuryPoints(), 0, MaxOrganicInjuryCapacity);
    }

    public int GetMaxInjuryCapacity()
    {
        return MaxOrganicInjuryCapacity;
    }

    internal bool TryApplyInjury(DamageInstance injury)
    {
        if (Injuries.Contains(injury)) 
        {
            Debug.LogError("Trying to apply an injury that already exists.");
            return false; 
        }

        // Has space for more injuries
        if (GetRemainingInjuryCapacity() >= injury.DamageSeverity)
        {
            Injuries.Add(injury);
            return true;
        }
        // Destroy limb
        else
        {
            // WE DEAD SON
            Injuries.Add(injury);
            return false;
        }
    }

    internal bool TryRemoveInjury(DamageInstance injury)
    {
        return Injuries.Remove(injury);
    }

    public IEnumerable<DamageInstance> GetInjuries()
    {
        return Injuries;
    }

    public float GetConditionPercent()
    {
        return GetRemainingInjuryCapacity() / GetMaxInjuryCapacity();
    }

    public string GetConditionDescription()
    {
        float conditionPercent = GetConditionPercent();

        if (conditionPercent <= 0f)
        {
            return "No life left.";
        }
        if (conditionPercent <= 0.2f)
        {
            return "Fading fast.";
        }
        if (conditionPercent <= 0.5f)
        {
            return "Half pulverised.";
        }
        if (conditionPercent <= 0.8f)
        {
            return "Carrying a few scars.";
        }
        else
        {
            return "Strong and steady.";
        }
    }

    internal bool TryGetInjuryFromIndex(int injuryIndex, out DamageInstance injury)
    {
        injury = default;

        if (Injuries.Count <= injuryIndex)
        {
            return false;
        }

        injury = Injuries[injuryIndex];
        return true;
    }
}