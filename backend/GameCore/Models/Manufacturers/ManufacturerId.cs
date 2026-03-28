using System;

public readonly struct ManufacturerId : IGameDbId, IEquatable<ManufacturerId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);

    static ManufacturerId()
    {
        TypedIdTypeRegistry.Register("ManufacturerId", s => new ManufacturerId(s));
    }

    public ManufacturerId(string value)
    {
        Value = value;
    }

    public static ManufacturerId New(string id) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(ManufacturerId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is ManufacturerId other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
