using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Builds a response for an operation of an OpenAPI document from the examples and schemas of its success response.
/// </summary>
internal static class ResponseGenerator
{
    public static HttpResponseMessage CreateResponse(OpenApiOperation operation)
    {
        (HttpStatusCode statusCode, IOpenApiResponse? definition) = SelectResponse(operation);

        // .NET Framework leaves the content null by default, while .NET returns an empty content.
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent([])
        };

        if (SelectMediaType(definition) is not { } mediaType)
        {
            return response;
        }

        IOpenApiMediaType media = mediaType.Value;
#pragma warning disable CS0618 // OpenAPI 3.0 documents still use the singular example
        JsonNode? body = media.Example?.DeepClone() ??
            media.Examples?.Values.Select(example => example.Value).FirstOrDefault(example => example is not null)?.DeepClone() ??
            ExampleGenerator.Generate(media.Schema);
#pragma warning restore CS0618

        if (body is null && media.Schema is null)
        {
            return response;
        }

        string contentType = mediaType.Key.EndsWith("*", StringComparison.Ordinal)
            ? "application/json"
            : MediaTypes.StripParameters(mediaType.Key);

        string content = !MediaTypes.IsJson(contentType) && body is JsonValue value && value.TryGetValue(out string? text)
            ? text
            : body?.ToJsonString() ?? "null";

        response.Content = new StringContent(content, Encoding.UTF8, contentType);

        return response;
    }

    /// <summary>
    /// Picks the lowest explicit 2xx response, then a <c>2XX</c> range, then the <c>default</c> response.
    /// </summary>
    private static (HttpStatusCode StatusCode, IOpenApiResponse? Definition) SelectResponse(OpenApiOperation operation)
    {
        List<KeyValuePair<string, IOpenApiResponse>> responses = operation.Responses?.ToList() ?? [];

        KeyValuePair<string, IOpenApiResponse> success = responses
            .Where(pair => int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int code) &&
                code is >= 200 and < 300)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        if (success.Value is not null)
        {
            return ((HttpStatusCode)int.Parse(success.Key, CultureInfo.InvariantCulture), success.Value);
        }

        IOpenApiResponse? fallback = responses
            .Where(pair => pair.Key.Equals("2XX", StringComparison.OrdinalIgnoreCase) ||
                pair.Key.Equals("default", StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .FirstOrDefault();

        return (HttpStatusCode.OK, fallback);
    }

    private static KeyValuePair<string, IOpenApiMediaType>? SelectMediaType(IOpenApiResponse? definition)
    {
        if (definition?.Content is not { Count: > 0 } content)
        {
            return null;
        }

        return content.Any(pair => MediaTypes.IsJson(pair.Key))
            ? content.First(pair => MediaTypes.IsJson(pair.Key))
            : content.First();
    }
}
