using System.Collections.Generic;

public class EquipmentSlotToItemMapEntry
{
    public CyberneticSlotId CyberneticSlotId;
    public ItemId? ItemId;
}
public class CyberneticsSaveData
{
    public List<EquipmentSlotToItemMapEntry> Cybernetics = new();
    public CyberneticsSaveData() { }
}

public sealed class CyberneticsComponent : BaseEntity
{
    internal CyberneticsSaveData Save()
    {
        List<EquipmentSlotToItemMapEntry> cybernetics = new();
        foreach (var kvp in _items)
        {
            cybernetics.Add(new EquipmentSlotToItemMapEntry 
            { 
                CyberneticSlotId = kvp.Key, 
                ItemId = kvp.Value 
            });
        }

        return new CyberneticsSaveData { Cybernetics = cybernetics };
    }

    private static readonly Dictionary<CyberneticSlotId, List<AnatomySlotId>> _coverageMap = new()
    {
        { SlotIds.Cybernetic.Head, new() { SlotIds.Organic.Head } },
        { SlotIds.Cybernetic.Torso, new() { SlotIds.Organic.Torso } },
        { SlotIds.Cybernetic.LeftArm, new() { SlotIds.Organic.LeftArm } },
        { SlotIds.Cybernetic.RightArm, new() { SlotIds.Organic.RightArm } },
        { SlotIds.Cybernetic.Legs, new() { SlotIds.Organic.LeftLeg, SlotIds.Organic.RightLeg } },
    };

    private Dictionary<CyberneticSlotId, ItemId?> _items = new();

    public CyberneticsComponent(CyberneticsSaveData data)
    {
        Dictionary<CyberneticSlotId, ItemId?> items = new();
        foreach (var item in data.Cybernetics)
        {
            items.Add(item.CyberneticSlotId, item.ItemId);
        }

        _items = items;
    }

    // Get rid of this maybe
    public CyberneticsComponent(Dictionary<CyberneticSlotId, ItemId?> items)
    {
        _items = items;
    }

    internal Dictionary<CyberneticSlotId, ItemId?> GetAllItems() => _items;

    public static IEnumerable<AnatomySlotId> GetCoveredBodyparts(CyberneticSlotId slot)
    {
        if (_coverageMap.TryGetValue(slot, out var bodyPartSlots))
        {
            foreach (var coverageList in bodyPartSlots)
            {
                yield return coverageList;
            }
        }
    }

    internal int GetMaxDamageCapacity()
    {
        return 3;
        //return _items.Values
        //    .Where(item => item != null)
        //    .Where(item => item.Value != null)
        //    .Where(item => item.Value.GetIsModFrame())
        //    .Sum(item => item.Value.ModFrame.MaxDamageCapacity);
    }

    internal int GetCurrentHealth()
    {
        return 3;
        //return _items.Values
        //    .Where(item => item != null)
        //    .Where(item => item.Value != null)
        //    .Where(item => item.Value.GetIsModFrame())
        //    .Sum(item => item.Value.ModFrame.GetRemainingDamageCapacity());
    }

    internal float GetHealthPercentage()
    {
        float max = GetMaxDamageCapacity();
        float current = GetCurrentHealth();
        return max <= 0f ? 0f : current / max;
    }

    public float GetEfficiency()
    {
        return 0.5f;
    }

    public float GetTemperature()
    {
        return 120;
    }

    public float GetSecurity()
    {
        return 0.9f;
    }

    public bool TryResolveSlot(CyberneticSlotId slotPath, out ItemId? slot)
    {
        return _items.TryGetValue(slotPath, out slot);
    }

    public bool TryUnassignFromSlot(CyberneticSlotId slotPath, out ItemId unassignedItem)
    {
        unassignedItem = default;

        if (!_items.ContainsKey(slotPath))
            return false;

        if (!_items[slotPath].HasValue)
            return false;

        unassignedItem = _items[slotPath].Value;
        _items[slotPath] = null;

        return true;
    }

    public bool TryAssignToSlot(CyberneticSlotId slotPath, ItemId? itemId)
    {
        // Slot not found
        if (!_items.ContainsKey(slotPath))
            return false;

        // Already has something here. Can not assign
        if (_items[slotPath].HasValue)
            return false;

        _items[slotPath] = itemId;

        return true;
    }

    public Dictionary<CyberneticSlotId, ItemId?> GetAllSlots() => _items;
}