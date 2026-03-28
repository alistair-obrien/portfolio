using System;

public readonly struct ItemTemplateId : ITemplateDbId, IEquatable<ItemTemplateId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);

    public ItemTemplateId(string value)
    {
        Value = value;
    }

    public static ItemTemplateId New(string id = null) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(ItemTemplateId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is ItemTemplateId other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
