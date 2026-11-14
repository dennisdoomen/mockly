using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;

namespace Mockly;

/// <summary>
/// Validates JSON values sent by the system under test against the schemas of an OpenAPI document. Supports the subset
/// of JSON Schema that API descriptions commonly use: types, nullability, enumerations, string, numeric, array and
/// object constraints, and <c>allOf</c>/<c>anyOf</c>/<c>oneOf</c>/<c>not</c> composition.
/// </summary>
internal static class SchemaValidator
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    private static readonly JsonSchemaType[] AllTypes =
    [
        JsonSchemaType.Null, JsonSchemaType.Boolean, JsonSchemaType.Integer, JsonSchemaType.Number,
        JsonSchemaType.String, JsonSchemaType.Array, JsonSchemaType.Object
    ];

    /// <summary>
    /// Validates <paramref name="value"/> against <paramref name="schema"/> and returns a description of each violation.
    /// </summary>
    /// <param name="value">The value to validate, where <c>null</c> represents a JSON <c>null</c>.</param>
    /// <param name="schema">The schema to validate against.</param>
    /// <param name="location">How to refer to the value in violation messages, e.g. <c>$.name</c>.</param>
    public static List<string> Validate(JsonNode? value, IOpenApiSchema? schema, string location)
    {
        var violations = new List<string>();
        Validate(value, schema, location, violations);
        return violations;
    }

    private static void Validate(JsonNode? value, IOpenApiSchema? schema, string location, List<string> violations)
    {
        if (schema is null)
        {
            return;
        }

        if (!ValidateType(value, schema, location, violations))
        {
            return;
        }

        ValidateEnumeration(value, schema, location, violations);
        ValidateComposition(value, schema, location, violations);

        switch (value)
        {
            case JsonObject jsonObject:
            {
                ValidateObject(jsonObject, schema, location, violations);
                break;
            }

            case JsonArray jsonArray:
            {
                ValidateArray(jsonArray, schema, location, violations);
                break;
            }

            case JsonValue jsonValue when jsonValue.GetValueKind() == JsonValueKind.String:
            {
                ValidateString(jsonValue.GetValue<string>(), schema, location, violations);
                break;
            }

            case JsonValue jsonValue when jsonValue.GetValueKind() == JsonValueKind.Number:
            {
                ValidateNumber(jsonValue.GetValue<decimal>(), schema, location, violations);
                break;
            }

            default:
            {
                // Booleans and nulls have no further constraints
                break;
            }
        }
    }

    /// <summary>
    /// Checks the JSON type of <paramref name="value"/> and returns <c>false</c> when it doesn't match, so that the
    /// type-specific constraints, which would only produce noise, are skipped.
    /// </summary>
    private static bool ValidateType(JsonNode? value, IOpenApiSchema schema, string location, List<string> violations)
    {
        JsonSchemaType actual = GetJsonType(value);

        if (schema.Type is not { } expected || !HasAnyType(expected))
        {
            return true;
        }

        bool matches = expected.HasFlag(actual) ||
            (actual == JsonSchemaType.Integer && expected.HasFlag(JsonSchemaType.Number));

        if (!matches)
        {
            violations.Add($"{location} must be {Describe(expected)}, but found {Describe(actual)} {Format(value)}");
        }

        return matches;
    }

    private static void ValidateEnumeration(JsonNode? value, IOpenApiSchema schema, string location, List<string> violations)
    {
        if (schema.Enum is { Count: > 0 } allowed && !allowed.Any(candidate => JsonNode.DeepEquals(candidate, value)))
        {
            // OpenAPI 3.0 documents often list null in the enumeration only implicitly through 'nullable'.
            if (value is null && schema.Type?.HasFlag(JsonSchemaType.Null) == true)
            {
                return;
            }

            violations.Add(
                $"{location} must be one of {string.Join(", ", allowed.Select(Format))}, but found {Format(value)}");
        }

        if (schema.Const is not null && !JsonNode.DeepEquals(JsonValue.Create(schema.Const), value))
        {
            violations.Add($"{location} must be \"{schema.Const}\", but found {Format(value)}");
        }
    }

    private static void ValidateComposition(JsonNode? value, IOpenApiSchema schema, string location,
        List<string> violations)
    {
        foreach (IOpenApiSchema part in schema.AllOf ?? [])
        {
            Validate(value, part, location, violations);
        }

        if (schema.AnyOf is { Count: > 0 } anyOf &&
            !anyOf.Any(alternative => Validate(value, alternative, location).Count == 0))
        {
            violations.Add($"{location} does not match any of the {anyOf.Count} allowed schemas (anyOf)");
        }

        if (schema.OneOf is { Count: > 0 } oneOf)
        {
            int matches = oneOf.Count(alternative => Validate(value, alternative, location).Count == 0);
            if (matches != 1)
            {
                violations.Add($"{location} must match exactly one of the {oneOf.Count} allowed schemas (oneOf), " +
                    $"but matches {matches}");
            }
        }

        if (schema.Not is not null && Validate(value, schema.Not, location).Count == 0)
        {
            violations.Add($"{location} matches a schema it is not allowed to match (not)");
        }
    }

    private static void ValidateObject(JsonObject value, IOpenApiSchema schema, string location, List<string> violations)
    {
        IDictionary<string, IOpenApiSchema> properties =
            schema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        foreach (string required in schema.Required ?? new HashSet<string>(StringComparer.Ordinal))
        {
            // Read-only properties are only sent by the server, so the client can't be expected to provide them.
            bool isReadOnly = properties.TryGetValue(required, out IOpenApiSchema? propertySchema) && propertySchema.ReadOnly;
            if (!value.ContainsKey(required) && !isReadOnly)
            {
                violations.Add($"{location} is missing required property '{required}'");
            }
        }

        foreach (KeyValuePair<string, JsonNode?> property in value)
        {
            string propertyLocation = $"{location}.{property.Key}";

            if (properties.TryGetValue(property.Key, out IOpenApiSchema? propertySchema))
            {
                Validate(property.Value, propertySchema, propertyLocation, violations);
            }
            else if (!schema.AdditionalPropertiesAllowed)
            {
                violations.Add($"{location} contains property '{property.Key}' which the schema does not define");
            }
            else if (schema.AdditionalProperties is not null)
            {
                Validate(property.Value, schema.AdditionalProperties, propertyLocation, violations);
            }
            else
            {
                // Additional properties are allowed and unconstrained
            }
        }

        if (schema.MinProperties is { } minimum && value.Count < minimum)
        {
            violations.Add($"{location} must have at least {minimum} properties, but has {value.Count}");
        }

        if (schema.MaxProperties is { } maximum && value.Count > maximum)
        {
            violations.Add($"{location} must have at most {maximum} properties, but has {value.Count}");
        }
    }

    private static void ValidateArray(JsonArray value, IOpenApiSchema schema, string location, List<string> violations)
    {
        for (int index = 0; index < value.Count; index++)
        {
            Validate(value[index], schema.Items, $"{location}[{index}]", violations);
        }

        if (schema.MinItems is { } minimum && value.Count < minimum)
        {
            violations.Add($"{location} must have at least {minimum} items, but has {value.Count}");
        }

        if (schema.MaxItems is { } maximum && value.Count > maximum)
        {
            violations.Add($"{location} must have at most {maximum} items, but has {value.Count}");
        }

        if (schema.UniqueItems == true)
        {
            int distinct = value.Select(item => item?.ToJsonString() ?? "null").Distinct(StringComparer.Ordinal).Count();
            if (distinct != value.Count)
            {
                violations.Add($"{location} must contain unique items");
            }
        }
    }

    private static void ValidateString(string value, IOpenApiSchema schema, string location, List<string> violations)
    {
        if (schema.MinLength is { } minimum && value.Length < minimum)
        {
            violations.Add($"{location} must be at least {minimum} characters long, but \"{value}\" has {value.Length}");
        }

        if (schema.MaxLength is { } maximum && value.Length > maximum)
        {
            violations.Add($"{location} must be at most {maximum} characters long, but \"{value}\" has {value.Length}");
        }

        if (!string.IsNullOrEmpty(schema.Pattern) && !MatchesPattern(value, schema.Pattern!))
        {
            violations.Add($"{location} must match the pattern '{schema.Pattern}', but found \"{value}\"");
        }

        if (!HasValidFormat(value, schema.Format))
        {
            violations.Add($"{location} must be a valid {schema.Format}, but found \"{value}\"");
        }
    }

    private static void ValidateNumber(decimal value, IOpenApiSchema schema, string location, List<string> violations)
    {
        if (SchemaBounds.IsBelowMinimum(schema, value) || SchemaBounds.IsAboveMaximum(schema, value))
        {
            violations.Add($"{location} must be {DescribeRange(schema)}, but found {value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (schema.MultipleOf is { } multipleOf && multipleOf != 0 && value % multipleOf != 0)
        {
            violations.Add($"{location} must be a multiple of {multipleOf.ToString(CultureInfo.InvariantCulture)}, " +
                $"but found {value.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        try
        {
            // The pattern comes from the document, so guard against catastrophic backtracking.
            return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, PatternTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
        catch (ArgumentException)
        {
            // The document uses a pattern .NET can't parse, so we can't hold the request against it.
            return true;
        }
    }

    private static bool HasValidFormat(string value, string? format)
    {
        return format?.ToUpperInvariant() switch
        {
            "DATE-TIME" => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
                value.IndexOfAny(['T', 't']) > 0,
            "DATE" => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "UUID" => Guid.TryParse(value, out _),
            _ => true
        };
    }

    private static JsonSchemaType GetJsonType(JsonNode? value)
    {
        return value switch
        {
            null => JsonSchemaType.Null,
            JsonObject => JsonSchemaType.Object,
            JsonArray => JsonSchemaType.Array,
            _ => value.GetValueKind() switch
            {
                JsonValueKind.String => JsonSchemaType.String,
                JsonValueKind.True or JsonValueKind.False => JsonSchemaType.Boolean,
                JsonValueKind.Number when value.GetValue<decimal>() % 1 == 0 => JsonSchemaType.Integer,
                JsonValueKind.Number => JsonSchemaType.Number,
                _ => JsonSchemaType.Null
            }
        };
    }

    private static string Describe(JsonSchemaType type)
    {
        string[] names = AllTypes
            .Where(flag => type.HasFlag(flag))
            .Select(flag => flag switch
            {
                JsonSchemaType.Array => "an array",
                JsonSchemaType.Boolean => "a boolean",
                JsonSchemaType.Integer => "an integer",
                JsonSchemaType.Number => "a number",
                JsonSchemaType.Object => "an object",
                JsonSchemaType.String => "a string",
                _ => "null"
            })
            .ToArray();

        return string.Join(" or ", names);
    }

    /// <summary>
    /// Determines whether <paramref name="type"/> has at least one type flag set, since an empty set of flags means
    /// that the schema does not constrain the type.
    /// </summary>
    public static bool HasAnyType(JsonSchemaType type)
    {
        return AllTypes.Any(flag => type.HasFlag(flag));
    }

    private static string DescribeRange(IOpenApiSchema schema)
    {
        var parts = new List<string>();
        if (schema.Minimum is not null)
        {
            parts.Add($">= {schema.Minimum}");
        }

        if (schema.ExclusiveMinimum is not null)
        {
            parts.Add($"> {schema.ExclusiveMinimum}");
        }

        if (schema.Maximum is not null)
        {
            parts.Add($"<= {schema.Maximum}");
        }

        if (schema.ExclusiveMaximum is not null)
        {
            parts.Add($"< {schema.ExclusiveMaximum}");
        }

        return string.Join(" and ", parts);
    }

    private static string Format(JsonNode? value)
    {
        return value?.ToJsonString() ?? "null";
    }
}
