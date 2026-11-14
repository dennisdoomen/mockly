using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Mockly.Common;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Checks a request of the system under test against the operations of one or more OpenAPI documents.
/// </summary>
internal static class ContractValidator
{
    private static readonly string[] IgnoredHeaderParameters = ["Accept", "Content-Type", "Authorization"];

    /// <summary>
    /// Throws a <see cref="RequestContractViolationException"/> when <paramref name="request"/> does not map onto
    /// an operation of any of the <paramref name="contracts"/>, or violates the operation it maps onto.
    /// </summary>
    public static void Validate(RequestInfo request, IReadOnlyList<OpenApiContract> contracts)
    {
        PathMatch? pathMatch = null;

        foreach (OpenApiContract contract in contracts)
        {
            PathMatch? match = contract.FindPath(request.Uri);
            OpenApiOperation? operation = match?.FindOperation(request.Method);
            if (operation is not null)
            {
                List<string> violations = ValidateOperation(request, match!, operation);
                if (violations.Count > 0)
                {
                    throw new RequestContractViolationException(
                        BuildMessage(request, $"operation {Describe(request.Method, match!, operation)}", violations));
                }

                return;
            }

            pathMatch ??= match;
        }

        string violation = pathMatch is null
            ? "No path in the OpenAPI document matches the request path"
            : $"Path {pathMatch.Template} does not support {request.Method.Method.ToUpperInvariant()}, only " +
            string.Join(", ", pathMatch.PathItem.Operations?.Keys.Select(method => method.Method.ToUpperInvariant()) ?? []);

        throw new RequestContractViolationException(BuildMessage(request,
            "the OpenAPI document(s) " + string.Join(", ", contracts.Select(contract => contract.Source)), [violation]));
    }

    private static List<string> ValidateOperation(RequestInfo request, PathMatch match, OpenApiOperation operation)
    {
        var violations = new List<string>();

        foreach (IOpenApiParameter parameter in GetParameters(match.PathItem, operation).Where(IsApplicable))
        {
            ValidateParameter(request, match, parameter, violations);
        }

        if (operation.RequestBody is not null)
        {
            ValidateBody(request, operation.RequestBody, violations);
        }

        return violations;
    }

    /// <summary>
    /// Combines the parameters of the path and the operation, where the operation can override a path parameter
    /// with the same name and location.
    /// </summary>
    private static IEnumerable<IOpenApiParameter> GetParameters(IOpenApiPathItem pathItem, OpenApiOperation operation)
    {
        List<IOpenApiParameter> operationParameters = operation.Parameters?.ToList() ?? [];

        IEnumerable<IOpenApiParameter> inherited = (pathItem.Parameters ?? [])
            .Where(parameter => !operationParameters.Any(own =>
                string.Equals(own.Name, parameter.Name, StringComparison.Ordinal) && own.In == parameter.In));

        return inherited.Concat(operationParameters);
    }

    /// <summary>
    /// The OpenAPI specification states that header parameters named Accept, Content-Type or Authorization are ignored,
    /// since those are described by the media types and security schemes.
    /// </summary>
    private static bool IsApplicable(IOpenApiParameter parameter)
    {
        return parameter.In != ParameterLocation.Header ||
            !IgnoredHeaderParameters.Contains(parameter.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateParameter(RequestInfo request, PathMatch match, IOpenApiParameter parameter,
        List<string> violations)
    {
        string name = parameter.Name ?? string.Empty;
        string location = $"{Describe(parameter.In)} parameter '{name}'";
        List<string> values = GetParameterValues(request, match, parameter);

        if (values.Count == 0)
        {
            if (parameter.Required)
            {
                violations.Add($"Required {location} is missing");
            }

            return;
        }

        IOpenApiSchema? schema = parameter.Schema ?? parameter.Content?.Values.FirstOrDefault()?.Schema;
        if (schema is null)
        {
            return;
        }

        JsonNode? value = schema.Type?.HasFlag(JsonSchemaType.Array) == true
            ? new JsonArray(values
                .SelectMany(item => values.Count == 1 ? item.Split(',') : [item])
                .Select(item => ParameterValueToJson(item, schema.Items))
                .ToArray())
            : ParameterValueToJson(values[0], schema);

        violations.AddRange(SchemaValidator.Validate(value, schema, location));
    }

    private static List<string> GetParameterValues(RequestInfo request, PathMatch match, IOpenApiParameter parameter)
    {
        string name = parameter.Name ?? string.Empty;

        return parameter.In switch
        {
            ParameterLocation.Path => match.PathParameters.TryGetValue(name, out string? pathValue) ? [pathValue] : [],
            ParameterLocation.Query => (request.Uri?.Query.ParseUrlEncoded() ?? [])
                .Where(pair => string.Equals(pair.Key, name, StringComparison.Ordinal))
                .Select(pair => pair.Value)
                .ToList(),
            ParameterLocation.Header => request.Headers.TryGetValues(name, out IEnumerable<string>? headerValues)
                ? [string.Join(",", headerValues)]
                : [],
            ParameterLocation.Cookie => GetCookieValues(request, name),
            _ => []
        };
    }

    private static List<string> GetCookieValues(RequestInfo request, string name)
    {
        IEnumerable<string> cookieHeaders = request.Headers.TryGetValues("Cookie", out IEnumerable<string>? cookies)
            ? cookies
            : [];

        return cookieHeaders
            .SelectMany(cookie => cookie.Split(';'))
            .Select(cookie => cookie.Trim().Split(['='], 2))
            .Where(pair => pair.Length == 2 && string.Equals(pair[0], name, StringComparison.Ordinal))
            .Select(pair => pair[1])
            .ToList();
    }

    /// <summary>
    /// Parameters arrive as text, so interpret numbers and booleans as such when the schema asks for them. Text that
    /// can't be interpreted stays a string, which the schema validation will then report.
    /// </summary>
    private static JsonNode ParameterValueToJson(string value, IOpenApiSchema? schema)
    {
        JsonSchemaType? type = schema?.Type;

        bool expectsScalar = type?.HasFlag(JsonSchemaType.Integer) == true ||
            type?.HasFlag(JsonSchemaType.Number) == true ||
            type?.HasFlag(JsonSchemaType.Boolean) == true;

        if (expectsScalar)
        {
            try
            {
                JsonNode? parsed = JsonNode.Parse(value);
                if (parsed is JsonValue)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Not a number or boolean, so fall back to treating it as a string
            }
        }

        return JsonValue.Create(value);
    }

    private static void ValidateBody(RequestInfo request, IOpenApiRequestBody requestBody, List<string> violations)
    {
        bool hasBody = request.RawBody is { Length: > 0 };
        if (!hasBody)
        {
            if (requestBody.Required)
            {
                violations.Add("Required request body is missing");
            }

            return;
        }

        IDictionary<string, IOpenApiMediaType> content = requestBody.Content ?? new Dictionary<string, IOpenApiMediaType>();
        if (content.Count == 0)
        {
            return;
        }

        string contentType = request.ContentType ?? string.Empty;
        IOpenApiMediaType? mediaType = MediaTypes.FindBestMatch(content, contentType);
        if (mediaType is null)
        {
            violations.Add($"Content type '{contentType}' is not accepted, expected one of {string.Join(", ", content.Keys)}");
            return;
        }

        if (!MediaTypes.IsJson(contentType) || mediaType.Schema is null)
        {
            return;
        }

        JsonNode? body;
        try
        {
            body = JsonNode.Parse(request.RawBody!);
        }
        catch (JsonException exception)
        {
            violations.Add($"Request body is not valid JSON: {exception.Message}");
            return;
        }

        violations.AddRange(SchemaValidator.Validate(body, mediaType.Schema, "$"));
    }

    private static string Describe(ParameterLocation? location)
    {
        return location switch
        {
            ParameterLocation.Path => "path",
            ParameterLocation.Query => "query",
            ParameterLocation.Header => "header",
            ParameterLocation.Cookie => "cookie",
            _ => "unknown"
        };
    }

    private static string Describe(HttpMethod method, PathMatch match, OpenApiOperation operation)
    {
        string description = $"{method.Method.ToUpperInvariant()} {match.Template}";
        return operation.OperationId is null ? description : $"'{operation.OperationId}' ({description})";
    }

    private static string BuildMessage(RequestInfo request, string against, IEnumerable<string> violations)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Request violates the API contract:");
        builder.AppendLine($"  {request.Method} {request.Uri}");
        builder.AppendLine();
        builder.AppendLine($"Validated against {against}:");

        foreach (string violation in violations)
        {
            builder.Append("    ✗ ").AppendLine(violation);
        }

        builder.AppendLine();
        builder.AppendLine("Note that you can further inspect the executed requests through the HttpMock.Requests property.");

        return builder.ToString();
    }
}
