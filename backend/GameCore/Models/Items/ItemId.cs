using System;

public readonly struct ItemId : IGameDbId, IEquatable<ItemId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    public ItemId(string value)
    {
        Value = value;
    }

    public static ItemId New(string id = null) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(ItemId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is ItemId other && Equals(other);

    public override int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(ItemId left, ItemId right)
        => left.Equals(right);

    public static bool operator !=(ItemId left, ItemId right)
        => !left.Equals(right);

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
