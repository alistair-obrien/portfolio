using Newtonsoft.Json;
using System;
using System.Collections.Generic;

public sealed class CellFootprintJSONConverter : JsonConverter<CellFootprint>
{
    public override CellFootprint ReadJson(JsonReader reader, Type objectType, CellFootprint existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("CellFootprint must be a string");

        ReadOnlySpan<char> span = ((string)reader.Value).AsSpan();

        int c1 = span.IndexOf(':');
        if (c1 < 0) throw new JsonSerializationException("Invalid CellFootprint format");

        int c2 = span.Slice(c1 + 1).IndexOf(':');
        if (c2 < 0) throw new JsonSerializationException("Invalid CellFootprint format");
        c2 += c1 + 1;

        int c3 = span.Slice(c2 + 1).IndexOf(':');
        if (c3 < 0) throw new JsonSerializationException("Invalid CellFootprint format");
        c3 += c2 + 1;

        int x = int.Parse(span.Slice(0, c1));
        int y = int.Parse(span.Slice(c1 + 1, c2 - c1 - 1));
        int w = int.Parse(span.Slice(c2 + 1, c3 - c2 - 1));
        int h = int.Parse(span.Slice(c3 + 1));

        return new CellFootprint(x, y, w, h);
    }

    public override void WriteJson(JsonWriter writer, CellFootprint value, JsonSerializer serializer)
    {
        writer.WriteValue($"{value.X}:{value.Y}:{value.Width}:{value.Height}");
    }
}

[JsonConverter(typeof(CellFootprintJSONConverter))]
public readonly struct CellFootprint : IEquatable<CellFootprint>
{
    public CellPosition Position { get; }
    public CellSize Size { get; }

    [JsonIgnore] public int X => Position.X;
    [JsonIgnore] public int Y => Position.Y;
    [JsonIgnore] public int Width => Size.Width;
    [JsonIgnore] public int Height => Size.Height;

    [JsonConstructor]
    public CellFootprint(CellPosition position, CellSize size)
    {
        Position = position;
        Size = size;
    }

    public CellFootprint(int x, int y, int width, int height)
        : this(new CellPosition(x, y), new CellSize(width, height))
    {
    }

    public IEnumerable<CellPosition> Cells()
    {
        for (int dx = 0; dx < Width; dx++)
            for (int dy = 0; dy < Height; dy++)
                yield return new CellPosition(X + dx, Y + dy);
    }

    public bool Intersects(CellFootprint other)
    {
        return !(X + Width <= other.X ||
                 other.X + other.Width <= X ||
                 Y + Height <= other.Y ||
                 other.Y + other.Height <= Y);
    }

    public void ForEachCell(Action<CellPosition> action)
    {
        for (int dx = 0; dx < Width; dx++)
            for (int dy = 0; dy < Height; dy++)
                action(new CellPosition(X + dx, Y + dy));
    }

    public bool Equals(CellFootprint other) =>
        Position.Equals(other.Position) && Size.Equals(other.Size);

    public override bool Equals(object obj) =>
        obj is CellFootprint other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Position, Size);

    public override string ToString() =>
        $"Pos={Position}, Size={Size}";

    public static bool operator ==(CellFootprint left, CellFootprint right) => 
        left.Equals(right);

    public static bool operator !=(CellFootprint left, CellFootprint right) => 
        !left.Equals(right);

    public static CellFootprint operator +(CellFootprint left, Vec2Int right) => 
        new CellFootprint(left.X + right.x, left.Y + right.y, left.Width, left.Height);
}