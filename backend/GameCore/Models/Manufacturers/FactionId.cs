using System;

public readonly struct FactionId : IGameDbId, IEquatable<FactionId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);

    public FactionId(string value)
    {
        Value = value;
    }

    public static FactionId New(string id) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(FactionId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is FactionId other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
