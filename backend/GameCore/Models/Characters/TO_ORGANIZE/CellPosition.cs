using Newtonsoft.Json;
using System;


public sealed class CellPositionJSONConverter : JsonConverter<CellPosition>
{
    public override CellPosition ReadJson(JsonReader reader, Type objectType, CellPosition existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("CellSize must be a string");

        ReadOnlySpan<char> span = ((string)reader.Value).AsSpan();

        int colon = span.IndexOf(':');
        if (colon < 0)
            throw new JsonSerializationException("Invalid CellSize format");

        int x = int.Parse(span.Slice(0, colon));
        int y = int.Parse(span.Slice(colon + 1));

        return new CellPosition(x, y);
    }

    public override void WriteJson(JsonWriter writer, CellPosition value, JsonSerializer serializer)
    {
        writer.WriteValue($"{value.X}:{value.Y}");
    }
}

[JsonConverter(typeof(CellPositionJSONConverter))]
public readonly struct CellPosition : IEquatable<CellPosition>
{
    public readonly int X;
    public readonly int Y;

    [JsonConstructor]
    public CellPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static CellPosition North => new CellPosition(0, 1);
    public static CellPosition South => new CellPosition(0, -1);
    public static CellPosition East => new CellPosition(1, 0);
    public static CellPosition West => new CellPosition(-1, 0);


    public bool Equals(CellPosition other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is CellPosition other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X}, {Y})";

    public static CellPosition operator +(CellPosition a, CellPosition b) => new CellPosition(a.X + b.X, a.Y + b.Y);
    public static CellPosition operator -(CellPosition a, CellPosition b) => new CellPosition(a.X - b.X, a.Y - b.Y);
    public static bool operator ==(CellPosition a, CellPosition b) => a.Equals(b);
    public static bool operator !=(CellPosition a, CellPosition b) => !a.Equals(b);
}
