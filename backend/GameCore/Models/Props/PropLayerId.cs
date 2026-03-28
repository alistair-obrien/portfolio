using System;

public readonly struct PropLayerId : ITypedStringId, IEquatable<PropLayerId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    public PropLayerId(string value)
    {
        Value = value;
    }

    public static PropLayerId New(string id = null) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(PropLayerId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is PropLayerId other && Equals(other);

    public override int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(PropLayerId left, PropLayerId right)
        => left.Equals(right);

    public static bool operator !=(PropLayerId left, PropLayerId right)
        => !left.Equals(right);

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
