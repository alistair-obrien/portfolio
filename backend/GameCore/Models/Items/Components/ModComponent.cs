using System;

public class ModSaveData
{
    public ModSaveData() { }
    public ModSaveData(ModComponent modComponent) { }
}

public class ModComponent : ItemComponent
{
    public ModSaveData Save()
    {
        return new ModSaveData(this);
    }

    public ModComponent() { }
    public ModComponent(ModSaveData mod) { }
}
