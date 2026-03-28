using System.Collections.Generic;

public class StyleSlotToItemMapEntry
{
    public StyleSlotId StyleSlotId;
    public ItemId? ItemId;
}

public class StyleSaveData
{
    public List<StyleSlotToItemMapEntry> StyleItems;
}

public sealed class StyleComponent : BaseEntity
{
    internal StyleSaveData Save()
    {
        List<StyleSlotToItemMapEntry> styleItems = new List<StyleSlotToItemMapEntry>();
        foreach (var kvp in _items)
        {
            styleItems.Add(new StyleSlotToItemMapEntry
            {
                StyleSlotId = kvp.Key,
                ItemId = kvp.Value
            });
        }

        return new StyleSaveData { StyleItems = styleItems };
    }

    internal static StyleComponent Load(StyleSaveData data)
    {
        Dictionary<StyleSlotId, ItemId?> items = new();
        foreach (var item in data.StyleItems)
        {
            items.Add(item.StyleSlotId, item.ItemId);
        }

        return new StyleComponent(items);
    }

    private static readonly Dictionary<StyleSlotId, List<AnatomySlotId>> _coverageMap = new()
    {
        { SlotIds.Style.Head, new() { SlotIds.Organic.Head } },
        { SlotIds.Style.Face, new() { SlotIds.Organic.Head } },
        { SlotIds.Style.TorsoInner, new() { SlotIds.Organic.Torso, SlotIds.Organic.LeftArm, SlotIds.Organic.RightArm } },
        { SlotIds.Style.TorsoOuter, new() { SlotIds.Organic.Torso, SlotIds.Organic.LeftArm, SlotIds.Organic.RightArm } },
        { SlotIds.Style.Hands, new() { SlotIds.Organic.LeftArm, SlotIds.Organic.RightArm } },
        { SlotIds.Style.Legs, new() { SlotIds.Organic.LeftLeg, SlotIds.Organic.RightLeg } },
        { SlotIds.Style.Feet, new() { SlotIds.Organic.LeftLeg, SlotIds.Organic.RightLeg } }
    };

    private Dictionary<StyleSlotId, ItemId?> _items = new();
    
    public StyleComponent(Dictionary<StyleSlotId, ItemId?> items)
    {
        _items = items;
    }

    public StyleComponent(StyleSaveData style)
    {
        _items = new Dictionary<StyleSlotId, ItemId?>(); // Dont assign the Ids yet. This will be attached at a later time
    }

    internal Dictionary<StyleSlotId, ItemId?> GetAllItems() => _items;

    internal IEnumerable<ItemId> GetForBodyPart(AnatomySlotId bodyPart)
    {
        foreach (var (slot, item) in _items)
        {
            if (_coverageMap.TryGetValue(slot, out var coveredParts) &&
                coveredParts.Contains(bodyPart))
            {
                if (item.HasValue)
                {
                    yield return item.Value;
                }
            }
        }
    }

    public bool TryResolveSlot(StyleSlotId slotPath, out ItemId? slot)
    {
        return _items.TryGetValue(slotPath, out slot);
    }

    public bool TryUnassignFromSlot(StyleSlotId slotPath, out ItemId unassignedItem)
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

    public bool TryAssignToSlot(StyleSlotId slotPath, ItemId? itemId)
    {
        if (!_items.ContainsKey(slotPath))
            return false;

        if (_items[slotPath].HasValue)
            return false;

        _items[slotPath] = itemId;
        return true;
    }
}
