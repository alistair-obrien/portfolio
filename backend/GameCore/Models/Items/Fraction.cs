using Newtonsoft.Json;
using System;

public sealed class FractionJSONConverter : JsonConverter<Fraction>
{
    public override Fraction ReadJson(JsonReader reader, Type objectType, Fraction existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("Fraction must be a string");

        ReadOnlySpan<char> span = ((string)reader.Value).AsSpan();

        int slash = span.IndexOf('/');
        if (slash < 0)
            throw new JsonSerializationException("Invalid Fraction format");

        int current = int.Parse(span.Slice(0, slash));
        int max = int.Parse(span.Slice(slash + 1));

        return new Fraction(current, max);
    }

    public override void WriteJson(JsonWriter writer, Fraction value, JsonSerializer serializer)
    {
        writer.WriteValue($"{value.Current}/{value.Max}");
    }
}

[JsonConverter(typeof(FractionJSONConverter))]
public struct Fraction
{
    public int Current;
    public int Max;

    public Fraction(int current, int max)
    {
        Current = current; 
        Max = max;
    }
} 
