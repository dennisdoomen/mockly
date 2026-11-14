using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Mockly;

/// <summary>
/// Produces a plausible, schema-conforming JSON value for an OpenAPI schema, preferring the examples, defaults and
/// enumerations declared by the document over synthesized values.
/// </summary>
internal static class ExampleGenerator
{
    /// <summary>
    /// Limits how deep nested and recursive schemas (such as a tree of categories) are expanded.
    /// </summary>
    private const int MaxDepth = 8;

    public static JsonNode? Generate(IOpenApiSchema? schema)
    {
        return Generate(schema, depth: 0);
    }

    private static JsonNode? Generate(IOpenApiSchema? schema, int depth)
    {
        if (schema is null || depth > MaxDepth)
        {
            return null;
        }

#pragma warning disable CS0618 // OpenAPI 3.0 documents still use the singular example
        JsonNode? declared = schema.Examples?.FirstOrDefault() ?? schema.Example ?? schema.Default ??
            schema.Enum?.FirstOrDefault();
#pragma warning restore CS0618

        if (declared is not null)
        {
            return declared.DeepClone();
        }

        if (schema.Const is not null)
        {
            return JsonValue.Create(schema.Const);
        }

        if (schema.AllOf is { Count: > 0 })
        {
            return GenerateAllOf(schema, depth);
        }

        IOpenApiSchema? alternative = schema.OneOf?.FirstOrDefault() ?? schema.AnyOf?.FirstOrDefault();
        if (alternative is not null)
        {
            return Generate(alternative, depth + 1);
        }

        JsonSchemaType? type = schema.Type & ~JsonSchemaType.Null;
        if (type is null || !SchemaValidator.HasAnyType(type.Value))
        {
            type = schema.Properties is { Count: > 0 } ? JsonSchemaType.Object
                : schema.Items is not null ? JsonSchemaType.Array
                : null;
        }

        return type switch
        {
            null => schema.Type == JsonSchemaType.Null ? null : JsonValue.Create("string"),
            _ when type.Value.HasFlag(JsonSchemaType.Object) => GenerateObject(schema, depth),
            _ when type.Value.HasFlag(JsonSchemaType.Array) => GenerateArray(schema, depth),
            _ when type.Value.HasFlag(JsonSchemaType.String) => JsonValue.Create(GenerateString(schema)),
            _ when type.Value.HasFlag(JsonSchemaType.Integer) => JsonValue.Create(GenerateInteger(schema)),
            _ when type.Value.HasFlag(JsonSchemaType.Number) => JsonValue.Create(GenerateNumber(schema)),
            _ when type.Value.HasFlag(JsonSchemaType.Boolean) => JsonValue.Create(true),
            _ => null
        };
    }

    private static JsonNode? GenerateAllOf(IOpenApiSchema schema, int depth)
    {
        var merged = new JsonObject();

        foreach (IOpenApiSchema part in schema.AllOf!)
        {
            JsonNode? generated = Generate(part, depth + 1);
            if (generated is not JsonObject partObject)
            {
                // Not an object composition, so we can't merge anything. The first concrete value will have to do.
                return generated;
            }

            foreach (KeyValuePair<string, JsonNode?> property in partObject.ToList())
            {
                partObject.Remove(property.Key);
                merged[property.Key] = property.Value;
            }
        }

        if (GenerateObject(schema, depth) is JsonObject own)
        {
            foreach (KeyValuePair<string, JsonNode?> property in own.ToList())
            {
                own.Remove(property.Key);
                merged[property.Key] = property.Value;
            }
        }

        return merged;
    }

    private static JsonObject GenerateObject(IOpenApiSchema schema, int depth)
    {
        var result = new JsonObject();

        foreach (KeyValuePair<string, IOpenApiSchema> property in schema.Properties ?? new Dictionary<string, IOpenApiSchema>())
        {
            if (property.Value.WriteOnly)
            {
                continue;
            }

            JsonNode? value = Generate(property.Value, depth + 1);
            bool isRequired = schema.Required?.Contains(property.Key) == true;
            bool allowsNull = property.Value.Type?.HasFlag(JsonSchemaType.Null) == true;

            if (value is not null || isRequired || allowsNull)
            {
                result[property.Key] = value;
            }
        }

        return result;
    }

    private static JsonArray GenerateArray(IOpenApiSchema schema, int depth)
    {
        var result = new JsonArray();
        if (depth >= MaxDepth)
        {
            return result;
        }

        int count = Math.Max(1, schema.MinItems ?? 0);
        if (schema.MaxItems is not null)
        {
            count = Math.Min(count, schema.MaxItems.Value);
        }

        for (int index = 0; index < count; index++)
        {
            result.Add(Generate(schema.Items, depth + 1));
        }

        return result;
    }

    private static string GenerateString(IOpenApiSchema schema)
    {
        string value = schema.Format?.ToUpperInvariant() switch
        {
            "DATE-TIME" => "2025-01-01T12:00:00Z",
            "DATE" => "2025-01-01",
            "TIME" => "12:00:00",
            "UUID" => "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "EMAIL" => "user@example.com",
            "URI" or "URL" => "https://example.com",
            "HOSTNAME" => "example.com",
            "IPV4" => "192.0.2.1",
            "IPV6" => "2001:db8::1",
            "BYTE" => "c3RyaW5n",
            _ => "string"
        };

        if (schema.MinLength is not null && value.Length < schema.MinLength.Value)
        {
            value = value.PadRight(schema.MinLength.Value, 'x');
        }

        if (schema.MaxLength is not null && value.Length > schema.MaxLength.Value)
        {
            value = value.Substring(0, schema.MaxLength.Value);
        }

        return value;
    }

    private static long GenerateInteger(IOpenApiSchema schema)
    {
        decimal value = GenerateNumber(schema);
        long rounded = (long)Math.Ceiling(value);

        return SchemaBounds.IsAboveMaximum(schema, rounded) ? (long)Math.Floor(value) : rounded;
    }

    private static decimal GenerateNumber(IOpenApiSchema schema)
    {
        decimal? minimum = SchemaBounds.Parse(schema.Minimum);
        decimal? exclusiveMinimum = SchemaBounds.Parse(schema.ExclusiveMinimum);
        decimal? maximum = SchemaBounds.Parse(schema.Maximum);
        decimal? exclusiveMaximum = SchemaBounds.Parse(schema.ExclusiveMaximum);

        if (minimum is not null)
        {
            return minimum.Value;
        }

        if (exclusiveMinimum is not null)
        {
            return exclusiveMinimum.Value + 1;
        }

        if (maximum is not null && maximum.Value < 0)
        {
            return maximum.Value;
        }

        if (exclusiveMaximum is not null && exclusiveMaximum.Value <= 0)
        {
            return exclusiveMaximum.Value - 1;
        }

        return 0;
    }
}

/// <summary>
/// Interprets the numeric bounds of a schema, which the OpenAPI object model exposes as strings.
/// </summary>
internal static class SchemaBounds
{
    public static decimal? Parse(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result) ? result : null;
    }

    public static bool IsAboveMaximum(IOpenApiSchema schema, decimal value)
    {
        return (Parse(schema.Maximum) is { } maximum && value > maximum) ||
            (Parse(schema.ExclusiveMaximum) is { } exclusiveMaximum && value >= exclusiveMaximum);
    }

    public static bool IsBelowMinimum(IOpenApiSchema schema, decimal value)
    {
        return (Parse(schema.Minimum) is { } minimum && value < minimum) ||
            (Parse(schema.ExclusiveMinimum) is { } exclusiveMinimum && value <= exclusiveMinimum);
    }
}
