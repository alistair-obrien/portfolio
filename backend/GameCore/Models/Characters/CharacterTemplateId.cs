using System;

public readonly struct CharacterTemplateId : ITemplateDbId, IEquatable<CharacterTemplateId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);

    static CharacterTemplateId()
    {
        TypedIdTypeRegistry.Register("CharacterTemplateId", s => new CharacterTemplateId(s));
    }

    public CharacterTemplateId(string value)
    {
        Value = value;
    }

    public static CharacterTemplateId New(string id = null) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(CharacterTemplateId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is CharacterTemplateId other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
