using System;

public readonly struct PropId : IGameDbId, IEquatable<PropId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    public PropId(string value)
    {
        Value = value;
    }

    public static PropId New(string id = null) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(PropId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is PropId other && Equals(other);

    public override int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(PropId left, PropId right)
        => left.Equals(right);

    public static bool operator !=(PropId left, PropId right)
        => !left.Equals(right);

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
