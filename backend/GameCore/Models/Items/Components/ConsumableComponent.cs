using System;

public record ConsumableSaveData
{
    public int CurrentUses;
    public int MaxUses;

    public ConsumableSaveData()
    {
        CurrentUses = 0; 
        MaxUses = 0;
    }
}

public sealed class ConsumableComponent : ItemComponent
{
    public ConsumableSaveData Save()
    {
        return new ConsumableSaveData 
        { 
            CurrentUses = CurrentUses, 
            MaxUses = MaxUses 
        };
    }

    public int MaxUses { get; private set; }
    public int CurrentUses { get; private set; }

    public ConsumableComponent() { }
    public ConsumableComponent(ConsumableSaveData consumableSaveData) 
    { 
        MaxUses = Math.Max(0, consumableSaveData.MaxUses);
        CurrentUses = Math.Max(0, consumableSaveData.CurrentUses);
    }
}