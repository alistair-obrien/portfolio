using System;
using System.Collections.Generic;
using System.Linq;

public sealed class AnatomyMapEntry
{
    public AnatomySlotId BodySlotId;
    public OrganicBodyPartSaveData BodyPart;

    public AnatomyMapEntry()
    {

    }
}

public sealed class AnatomySaveData
{
    public List<AnatomyMapEntry> AnatomyMap = new();

    public AnatomySaveData()
    {

    }
}

public sealed class AnatomyComponent : BaseEntity
{
    internal AnatomySaveData Save()
    {
        List<AnatomyMapEntry> anatomyMaps = new List<AnatomyMapEntry>();
        foreach (var kvp in _bodyParts)
        {
            anatomyMaps.Add(new AnatomyMapEntry 
            { 
                BodySlotId = kvp.Key, 
                BodyPart = kvp.Value.Save() 
            });
        }

        return new AnatomySaveData { AnatomyMap = anatomyMaps };
    }

    private Dictionary<AnatomySlotId, OrganicBodyPartComponent> _bodyParts = new();

    public AnatomyComponent(AnatomySaveData data)
    {
        Dictionary<AnatomySlotId, OrganicBodyPartComponent> bodyParts = new();
        foreach (var item in data.AnatomyMap)
        {
            bodyParts.Add(item.BodySlotId, new OrganicBodyPartComponent(item.BodyPart));
        }
    }

    public AnatomyComponent(Dictionary<AnatomySlotId, OrganicBodyPartComponent> bodyParts) 
    {
        _bodyParts = bodyParts;
    }

    internal void AddBodyPart(AnatomySlotId slot, OrganicBodyPartComponent part)
    {
        if (_bodyParts.ContainsKey(slot))
            throw new InvalidOperationException($"Body part already exists: {slot}");

        _bodyParts[slot] = part;
    }

    internal int GetRemainingHealth() => GetMaxHealth() - GetCurrentHealth();

    internal int GetCurrentHealth() => (int)Mathf.Clamp(
        _bodyParts.Values.Sum(bodyPart => bodyPart.GetRemainingInjuryCapacity()), 
        0, 
        GetMaxHealth());
    internal int GetMaxHealth() => _bodyParts.Values.Sum(bodyPart => bodyPart.MaxOrganicInjuryCapacity);
    internal float GetHealthPercentage()
    {
        var max = GetMaxHealth();
        var current = GetCurrentHealth();
        return max <= 0f ? 0f : current / max;
    }

    internal IEnumerable<OrganicBodyPartComponent> AliveParts() => _bodyParts.Values.Where(p => p.GetRemainingInjuryCapacity() > 0);

    public bool TryResolveSlot(AnatomySlotId slotPath, out OrganicBodyPartComponent slot)
    {
        return _bodyParts.TryGetValue(slotPath, out slot);
    }

    public IReadOnlyDictionary<AnatomySlotId, OrganicBodyPartComponent> GetAllBodyParts()
    {
        return _bodyParts;
    }

    public bool TryApplyInjury(AnatomySlotId slot, DamageInstance injury)
    {
        if (TryResolveSlot(slot, out var bodyPart))
        {
            return bodyPart.TryApplyInjury(injury);
        }

        return false;
    }

    internal bool TryRemoveInjury(AnatomySlotId slot, DamageInstance injury)
    {
        if (TryResolveSlot(slot, out var bodyPart))
        {
            return bodyPart.TryRemoveInjury(injury);
        }

        return true;
    }

    internal bool TryGetInjury(AnatomySlotId bodySlotPath, int injuryIndex, out DamageInstance injury)
    {
        injury = default;

        if (!TryResolveSlot(bodySlotPath, out var bodyPart)) { return false; }
        if (!bodyPart.TryGetInjuryFromIndex(injuryIndex, out injury)) { return false; }

        return true;
    }
}
