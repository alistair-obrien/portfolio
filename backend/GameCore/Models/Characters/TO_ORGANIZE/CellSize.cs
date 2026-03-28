using Newtonsoft.Json;
using System;

public sealed class CellSizeJSONConverter : JsonConverter<CellSize>
{
    public override CellSize ReadJson(JsonReader reader, Type objectType, CellSize existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("CellSize must be a string");

        ReadOnlySpan<char> span = ((string)reader.Value).AsSpan();

        int colon = span.IndexOf(':');
        if (colon < 0)
            throw new JsonSerializationException("Invalid CellSize format");

        int width = int.Parse(span.Slice(0, colon));
        int height = int.Parse(span.Slice(colon + 1));

        return new CellSize(width, height);
    }

    public override void WriteJson(JsonWriter writer, CellSize value, JsonSerializer serializer)
    {
        writer.WriteValue($"{value.Width}:{value.Height}");
    }
}

[JsonConverter(typeof(CellSizeJSONConverter))]
public readonly struct CellSize : IEquatable<CellSize>
{
    public readonly int Width;
    public readonly int Height;

    [JsonConstructor]
    public CellSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public bool Equals(CellSize other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object obj) => obj is CellSize other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
}
