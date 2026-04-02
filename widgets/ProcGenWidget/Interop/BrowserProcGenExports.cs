using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using GenOSys.ProcGen;

public sealed class BrowserProcGenEnvelope
{
    public required bool Ok { get; init; }
    public string? ErrorMessage { get; init; }
    public BrowserProcGenMap? Map { get; init; }
}

public sealed class BrowserProcGenMap
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required BrowserProcGenLayers Layers { get; init; }
}

public sealed class BrowserProcGenLayers
{
    public required int[] Walls { get; init; }
    public required int[] Props { get; init; }
    public required int[] Items { get; init; }
    public required int[] Characters { get; init; }
}

internal sealed class GenerateMapRequest
{
    public string? GeneratorId { get; init; }
    public JsonElement Options { get; init; }
}

[SupportedOSPlatform("browser")]
public static partial class BrowserProcGenExports
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [JSExport]
    public static string GenerateMap(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<GenerateMapRequest>(requestJson, JsonOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.GeneratorId))
            {
                return SerializeError("Missing generatorId.");
            }

            var map = request.GeneratorId switch
            {
                "apartment" => BuildApartment(request.Options),
                "apartment-floor" => BuildApartmentFloor(request.Options),
                "apartment-building" => BuildApartmentBuilding(request.Options),
                _ => null,
            };

            if (map == null)
            {
                return SerializeError($"Unsupported generator '{request.GeneratorId}'.");
            }

            return JsonSerializer.Serialize(new BrowserProcGenEnvelope
            {
                Ok = true,
                ErrorMessage = null,
                Map = ToBrowserMap(map),
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return SerializeError(ex.Message);
        }
    }

    private static IProcGenMapData BuildApartment(JsonElement options)
    {
        return new ApartmentGenerator
        {
            Width = GetRequiredInt(options, "width", 6, 128),
            Height = GetRequiredInt(options, "height", 6, 128),
        }.Build();
    }

    private static IProcGenMapData BuildApartmentFloor(JsonElement options)
    {
        return new ApartmentFloorGenerator
        {
            Width = GetRequiredInt(options, "width", 8, 256),
            Height = GetRequiredInt(options, "height", 8, 256),
        }.Build();
    }

    private static IProcGenMapData BuildApartmentBuilding(JsonElement options)
    {
        int minApartmentWidth = GetRequiredInt(options, "minApartmentWidth", 6, 48);
        int maxApartmentWidth = GetRequiredInt(options, "maxApartmentWidth", minApartmentWidth, 64);
        int minApartmentDepth = GetRequiredInt(options, "minApartmentDepth", 6, 48);
        int maxApartmentDepth = GetRequiredInt(options, "maxApartmentDepth", minApartmentDepth, 64);
        int corridorWidth = GetRequiredInt(options, "corridorWidth", 8, 48);
        int width = GetRequiredInt(options, "width", 96, 384);
        int height = GetRequiredInt(options, "height", 96, 384);
        int requiredDimension = corridorWidth * 2 + minApartmentDepth * 4 + 1;

        if (width < requiredDimension || height < requiredDimension)
        {
            throw new InvalidOperationException(
                $"Building width and height must both be at least {requiredDimension} for the current corridor and minimum depth.");
        }

        return new ApartmentBuildingGenerator
        {
            Width = width,
            Height = height,
            CorridorWidth = corridorWidth,
            MinApartmentWidth = minApartmentWidth,
            MaxApartmentWidth = maxApartmentWidth,
            MinApartmentDepth = minApartmentDepth,
            MaxApartmentDepth = maxApartmentDepth,
            DoorWidth = GetRequiredInt(options, "doorWidth", 1, 6),
        }.Build();
    }

    private static int GetRequiredInt(JsonElement options, string propertyName, int min, int max)
    {
        if (options.ValueKind != JsonValueKind.Object || !options.TryGetProperty(propertyName, out var value))
        {
            throw new InvalidOperationException($"Missing option '{propertyName}'.");
        }

        if (!value.TryGetInt32(out var parsed))
        {
            throw new InvalidOperationException($"Option '{propertyName}' must be an integer.");
        }

        if (parsed < min || parsed > max)
        {
            throw new InvalidOperationException($"Option '{propertyName}' must be between {min} and {max}.");
        }

        return parsed;
    }

    private static BrowserProcGenMap ToBrowserMap(IProcGenMapData map)
    {
        return new BrowserProcGenMap
        {
            Width = map.Width,
            Height = map.Height,
            Layers = new BrowserProcGenLayers
            {
                Walls = Flatten(map.Walls),
                Props = Array.Empty<int>(),
                Items = Array.Empty<int>(),
                Characters = Array.Empty<int>(),
            },
        };
    }

    private static int[] Flatten(IEnumerable<Vector2I> cells)
    {
        var values = new List<int>();

        foreach (var cell in cells)
        {
            values.Add(cell.X);
            values.Add(cell.Y);
        }

        return values.ToArray();
    }

    private static string SerializeError(string message)
    {
        return JsonSerializer.Serialize(new BrowserProcGenEnvelope
        {
            Ok = false,
            ErrorMessage = message,
            Map = null,
        }, JsonOptions);
    }
}
