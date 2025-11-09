using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Mockly.Http;

/// <summary>
/// Fluent builder for configuring HTTP request mocks.
/// </summary>
public class RequestBuilder
{
    private readonly HttpMock mockBuilder;
    private readonly HttpMethod method;
    private string? pathPattern;
    private string? queryPattern;
    private Func<HttpRequestMessage, bool>? customMatcher;
    private RequestCollection? requestCollection;

    internal RequestBuilder(HttpMock mockBuilder, HttpMethod method)
    {
        this.mockBuilder = mockBuilder;
        this.method = method;
    }

    /// <summary>
    /// Specifies the path pattern to match. Supports wildcards (*).
    /// </summary>
    public RequestBuilder ForPath(string pathPattern)
    {
        this.pathPattern = pathPattern;
        return this;
    }

    /// <summary>
    /// Specifies the query string pattern to match. Supports wildcards (*).
    /// </summary>
    public RequestBuilder ForQuery(string queryPattern)
    {
        this.queryPattern = queryPattern;
        return this;
    }

    /// <summary>
    /// Specifies a custom matcher predicate for the request.
    /// </summary>
    public RequestBuilder For(Func<HttpRequestMessage, bool> matcher)
    {
        customMatcher = matcher;
        return this;
    }

    /// <summary>
    /// Collects captured requests in the specified collection.
    /// </summary>
    public RequestBuilder CollectingRequestIn(RequestCollection collection)
    {
        requestCollection = collection;
        return this;
    }

    /// <summary>
    /// Responds with the specified HTTP status code.
    /// </summary>
    public HttpMock RespondsWithStatus(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mock = new RequestMock
        {
            Method = method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            CustomMatcher = customMatcher,
            RequestCollection = requestCollection,
            Responder = _ => new HttpResponseMessage(statusCode)
        };

        mockBuilder.AddMock(mock);
        return mockBuilder;
    }

    /// <summary>
    /// Responds with JSON content serialized from the specified object.
    /// </summary>
    public HttpMock RespondsWithJsonContent(object content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mock = new RequestMock
        {
            Method = method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            CustomMatcher = customMatcher,
            RequestCollection = requestCollection,
            Responder = _ =>
            {
                var json = JsonSerializer.Serialize(content);
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        mockBuilder.AddMock(mock);
        return mockBuilder;
    }

    /// <summary>
    /// Responds with raw string content.
    /// </summary>
    public HttpMock RespondsWithContent(string content, string contentType = "text/plain", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mock = new RequestMock
        {
            Method = method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            CustomMatcher = customMatcher,
            RequestCollection = requestCollection,
            Responder = _ => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, contentType)
            }
        };

        mockBuilder.AddMock(mock);
        return mockBuilder;
    }

    /// <summary>
    /// Responds with empty content.
    /// </summary>
    public HttpMock RespondsWithEmptyContent(HttpStatusCode statusCode = HttpStatusCode.NoContent)
    {
        var mock = new RequestMock
        {
            Method = method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            CustomMatcher = customMatcher,
            RequestCollection = requestCollection,
            Responder = _ => new HttpResponseMessage(statusCode)
        };

        mockBuilder.AddMock(mock);
        return mockBuilder;
    }

    /// <summary>
    /// Responds using a custom responder function.
    /// </summary>
    public HttpMock RespondsWith(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var mock = new RequestMock
        {
            Method = method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            CustomMatcher = customMatcher,
            RequestCollection = requestCollection,
            Responder = responder
        };

        mockBuilder.AddMock(mock);
        return mockBuilder;
    }
}
