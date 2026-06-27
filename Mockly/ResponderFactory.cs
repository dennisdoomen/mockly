using System.Net;
using System.Text;
using System.Text.Json;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Centralizes the creation of responder functions used by both the initial <c>RespondsWith*</c> methods
/// and the sequenced <c>Then*</c> methods.
/// </summary>
internal static class ResponderFactory
{
    public static Func<RequestInfo, HttpResponseMessage> Status(HttpStatusCode statusCode)
    {
        return _ => new HttpResponseMessage(statusCode);
    }

    public static Func<RequestInfo, HttpResponseMessage> JsonContent(HttpStatusCode statusCode, object content,
        JsonSerializerOptions? options)
    {
        return _ =>
        {
            string json = JsonSerializer.Serialize(content, options);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        };
    }

    public static Func<RequestInfo, HttpResponseMessage> ODataResult(HttpStatusCode statusCode,
        IEnumerable<object> value, string? odataContext, JsonSerializerOptions? options)
    {
        return _ =>
        {
            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = value.ToArray()
            };

            if (!string.IsNullOrWhiteSpace(odataContext))
            {
                payload["@odata.context"] = odataContext;
            }

            string json = JsonSerializer.Serialize(payload, options);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        };
    }

    public static Func<RequestInfo, HttpResponseMessage> Content(HttpStatusCode statusCode, string content,
        string contentType)
    {
        return _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, contentType)
        };
    }

    public static Func<RequestInfo, HttpResponseMessage> HttpContent(HttpStatusCode statusCode, HttpContent content)
    {
        return _ => new HttpResponseMessage(statusCode)
        {
            Content = content
        };
    }
}
