using System;

public readonly struct MapChunkTemplateId : ITemplateDbId, IEquatable<MapChunkTemplateId>
{
    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);

    public MapChunkTemplateId(string value)
    {
        Value = value;
    }

    static MapChunkTemplateId()
    {
        TypedIdTypeRegistry.Register("MapChunkTemplateId", s => new MapChunkTemplateId(s));
    }

    public static MapChunkTemplateId New(string id = null) => new(id ?? Guid.NewGuid().ToString());

    public bool Equals(MapChunkTemplateId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is MapChunkTemplateId other && Equals(other);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override string ToString() => Value;
    public ITypedStringId NewOfSameType() => New(null);
    public ITypedStringId NewOfSameType(string newId) => New(newId);
}
