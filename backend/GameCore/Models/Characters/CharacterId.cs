using System;

public readonly struct CharacterId : IGameDbId, IEquatable<CharacterId>
{
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    public CharacterId(string value)
    {
        Value = value;
    }

    public static CharacterId New(string id = null) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(CharacterId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is CharacterId other && Equals(other);

    public override int GetHashCode()
        => Value?.GetHashCode() ?? 0;

    public static bool operator ==(CharacterId left, CharacterId right)
        => left.Equals(right);

    public static bool operator !=(CharacterId left, CharacterId right)
        => !left.Equals(right);

    public override string ToString() => Value;

    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}