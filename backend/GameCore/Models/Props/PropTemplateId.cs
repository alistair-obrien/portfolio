using System;

public readonly struct PropTemplateId : ITemplateDbId, IEquatable<PropTemplateId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);

    public PropTemplateId(string value)
    {
        Value = value;
    }

    static PropTemplateId()
    {
        TypedIdTypeRegistry.Register("PropTemplateId", s => new PropTemplateId(s));
    }

    public static PropTemplateId New(string id = null) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(PropTemplateId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is PropTemplateId other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
