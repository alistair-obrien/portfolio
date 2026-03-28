using System;

public interface ISlotId : ITypedStringId
{

}

public readonly struct AnatomySlotId : ISlotId, IEquatable<AnatomySlotId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    static AnatomySlotId()
    {
        TypedIdTypeRegistry.Register("AnatomySlotId", s => new AnatomySlotId(s));
    }

    public AnatomySlotId(string value)
    {
        Value = value;
    }

    public static AnatomySlotId New(string id) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(AnatomySlotId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is AnatomySlotId other && Equals(other);

    public override int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(AnatomySlotId left, AnatomySlotId right)
        => left.Equals(right);

    public static bool operator !=(AnatomySlotId left, AnatomySlotId right)
        => !left.Equals(right);

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}

public readonly struct LoadoutSlotId : ISlotId, IEquatable<LoadoutSlotId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    static LoadoutSlotId()
    {
        TypedIdTypeRegistry.Register("LoadoutSlotId", s => new LoadoutSlotId(s));
    }

    public LoadoutSlotId(string value)
    {
        Value = value;
    }

    public static LoadoutSlotId New(string id) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(LoadoutSlotId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is LoadoutSlotId other && Equals(other);

    public override int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(LoadoutSlotId left, LoadoutSlotId right)
        => left.Equals(right);

    public static bool operator !=(LoadoutSlotId left, LoadoutSlotId right)
        => !left.Equals(right);

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}

public readonly struct StyleSlotId : ISlotId, IEquatable<StyleSlotId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    static StyleSlotId()
    {
        TypedIdTypeRegistry.Register("StyleSlotId", s => new StyleSlotId(s));
    }

    public StyleSlotId(string value)
    {
        Value = value;
    }

    public static StyleSlotId New(string id) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(StyleSlotId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is StyleSlotId other && Equals(other);

    public override int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(StyleSlotId left, StyleSlotId right)
        => left.Equals(right);

    public static bool operator !=(StyleSlotId left, StyleSlotId right)
        => !left.Equals(right);

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}

public readonly struct CyberneticSlotId : ISlotId, IEquatable<CyberneticSlotId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    public CyberneticSlotId(string value)
    {
        Value = value;
    }

    public static CyberneticSlotId New(string id) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(CyberneticSlotId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is CyberneticSlotId other && Equals(other);

    public override int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(CyberneticSlotId left, CyberneticSlotId right)
        => left.Equals(right);

    public static bool operator !=(CyberneticSlotId left, CyberneticSlotId right)
        => !left.Equals(right);

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
