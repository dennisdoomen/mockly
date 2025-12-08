using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mockly.Common;

#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Fluent builder for configuring HTTP request mocks.
/// </summary>
public class RequestMockBuilder
{
    private readonly HttpMock mockBuilder;
    private readonly List<Matcher> customMatchers = new();
    private readonly uint? maxInvocations;
    private string? pathPattern;
    private string? queryPattern;
    private string? scheme;
    private string? hostPattern;
    private RequestCollection? requestCollection;

    internal RequestMockBuilder(HttpMock mockBuilder, HttpMethod method)
    {
        this.mockBuilder = mockBuilder;
        Method = method;

        // Defaults: https and localhost
        scheme = "https";
        hostPattern = "localhost";
    }

    /// <summary>
    /// Copy constructor to reuse settings from another builder.
    /// Only scheme and host are reused between calls; path, query, responders and invocation limits are not reused.
    /// </summary>
    internal RequestMockBuilder(HttpMock mockBuilder, RequestMockBuilder predecessor)
    {
        this.mockBuilder = mockBuilder;
        Method = predecessor.Method;

        // Reuse only scheme and host
        scheme = predecessor.scheme ?? "https";
        hostPattern = predecessor.hostPattern ?? "localhost";

        // Do NOT copy path/query/maxInvocations/custom matchers/response builders
        pathPattern = null;
        queryPattern = null;
        maxInvocations = null;
    }

    internal HttpMethod Method { get; set; }

    /// <summary>
    /// Specifies that the request must use HTTP scheme.
    /// </summary>
    public RequestMockBuilder ForHttp()
    {
        scheme = "http";
        return this;
    }

    /// <summary>
    /// Specifies that the request must use HTTPS scheme.
    /// </summary>
    public RequestMockBuilder ForHttps()
    {
        scheme = "https";
        return this;
    }

    /// <summary>
    /// Specifies the host pattern to match. Supports wildcards (*).
    /// </summary>
    public RequestMockBuilder ForHost(string hostPattern)
    {
        this.hostPattern = hostPattern;
        return this;
    }

    /// <summary>
    /// Matches any host.
    /// </summary>
    public RequestMockBuilder ForAnyHost()
    {
        hostPattern = "*";
        return this;
    }

    /// <summary>
    /// Specifies the path pattern to match. Supports wildcards (*).
    /// </summary>
    public RequestMockBuilder WithPath(string wildcardPattern)
    {
        pathPattern = wildcardPattern;
        return this;
    }

    /// <summary>
    /// Specifies the query string pattern to match. Supports wildcards (*).
    /// </summary>
    public RequestMockBuilder WithQuery(string wildcardPattern)
    {
        if (!wildcardPattern.StartsWith("?", StringComparison.Ordinal))
        {
            wildcardPattern = "?" + wildcardPattern;
        }

        queryPattern = wildcardPattern;
        return this;
    }

    /// <summary>
    /// Configures the request mock to match any query string in the request URI.
    /// </summary>
    public RequestMockBuilder WithAnyQuery()
    {
        return WithQuery("*");
    }

    /// <summary>
    /// Resets the query string matching to match any query string.
    /// </summary>
    public RequestMockBuilder WithoutQuery()
    {
        queryPattern = null;
        return this;
    }

    /// <summary>
    /// Configures the request mock to match the request body content against a specified regular expression.
    /// </summary>
    /// <param name="regex">
    /// The regular expression to match the request body against.
    /// </param>
    public RequestMockBuilder WithBodyMatchingRegex([StringSyntax(StringSyntaxAttribute.Regex)] string regex)
    {
        return With(request => request.Body is not null && Regex.IsMatch(request.Body, regex));
    }

    /// <summary>
    /// Configures the request mock to match requests whose body contains the JSON equivalent to the specified JSON,
    /// ignoring differences in whitespace and layout.
    /// </summary>
    /// <param name="json">The JSON string to compare against the request body.</param>
    /// <remarks>
    /// The comparison is performed by parsing both the expected JSON and the request body and then serializing
    /// them back to a canonical JSON representation. Any invalid JSON in the
    /// request body causes this matcher to return <c>false</c>.
    /// </remarks>
    public RequestMockBuilder WithBodyMatchingJson(string json)
    {
        if (json is null)
        {
            throw new ArgumentNullException(nameof(json));
        }

        JsonElement expectedRoot;

        using (var expectedDocument = JsonDocument.Parse(json))
        {
            expectedRoot = expectedDocument.RootElement.Clone();
        }

        return With(request =>
            {
                if (request.Body is null)
                {
                    return false;
                }

                try
                {
                    using var actualDocument = JsonDocument.Parse(request.Body);
                    return expectedRoot.JsonEquals(actualDocument.RootElement);
                }
                catch (JsonException jsonException)
                {
                    throw new RequestMatchingException("Could not parse the request body as JSON", jsonException);
                }
            }, $"Body matches JSON: {json}");
    }

    /// <summary>
    /// Configures the request mock to match requests whose body content satisfies the specified wildcard pattern.
    /// </summary>
    /// <param name="wildcardPattern">
    /// The wildcard pattern used to match the body of the request, where '?' represents any single character and '*' represents
    /// any sequence of characters.
    /// </param>
    /// <returns>The current <see cref="RequestMockBuilder"/> instance, updated with the specified body matching condition.</returns>
    public RequestMockBuilder WithBody(string wildcardPattern)
    {
        return With(request => request.Body is not null && request.Body.MatchesWildcard(wildcardPattern));
    }

    /// <summary>
    /// Specifies a custom matcher predicate for the request.
    /// </summary>
    public RequestMockBuilder With(Func<RequestInfo, bool> matcher,
        [CallerArgumentExpression(nameof(matcher))]
        string? matcherText = null)
    {
        customMatchers.Add(new Matcher(request => Task.FromResult(matcher(request)), matcherText));
        return this;
    }

    /// <summary>
    /// Specifies a custom matcher predicate for the request.
    /// </summary>
    public RequestMockBuilder With(Func<RequestInfo, Task<bool>> matcher,
        [CallerArgumentExpression(nameof(matcher))]
        string? matcherText = null)
    {
        customMatchers.Add(new Matcher(matcher, matcherText));
        return this;
    }

    /// <summary>
    /// Collects captured requests in the specified collection.
    /// </summary>
    public RequestMockBuilder CollectingRequestIn(RequestCollection collection)
    {
        requestCollection = collection;
        return this;
    }

    /// <summary>
    /// Responds with the specified HTTP status code.
    /// </summary>
    public RequestMockResponseBuilder RespondsWithStatus(HttpStatusCode statusCode)
    {
        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            MaxInvocations = maxInvocations,
            Responder = _ => new HttpResponseMessage(statusCode)
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Responds with JSON content serialized from the specified object and status code 200 (OK).
    /// </summary>
    public RequestMockResponseBuilder RespondsWithJsonContent(object content)
    {
        return RespondsWithJsonContent(HttpStatusCode.OK, content);
    }

    /// <summary>
    /// Responds with JSON content serialized from the specified object and a specific status code..
    /// </summary>
    public RequestMockResponseBuilder RespondsWithJsonContent(HttpStatusCode statusCode, object content)
    {
        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            MaxInvocations = maxInvocations,
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
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Configures an HTTP response with an OData v4 result envelope containing a single entity of the specified type
    /// and status code 200 (OK).
    /// </summary>
    /// <param name="value">The entity to include in the OData result.</param>
    public RequestMockResponseBuilder RespondsWithODataResult(object value)
    {
        return RespondsWithODataResult(HttpStatusCode.OK, [value]);
    }

    /// <summary>
    /// Configures an HTTP response with an OData v4 result envelope containing a single entity of the specified type.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="value">The entity to include in the OData result.</param>
    public RequestMockResponseBuilder RespondsWithODataResult(HttpStatusCode statusCode,
        object value)
    {
        return RespondsWithODataResult(statusCode, [value]);
    }

    /// <summary>
    /// Responds with an OData v4 result envelope: { "value": [...] } and status code 200 (OK).
    /// </summary>
    public RequestMockResponseBuilder RespondsWithODataResult(IEnumerable<object> value)
    {
        return RespondsWithODataResult(HttpStatusCode.OK, value);
    }

    /// <summary>
    /// Responds with an OData v4 result envelope: { "value": [...] } and a specific status code.
    /// </summary>
    public RequestMockResponseBuilder RespondsWithODataResult(HttpStatusCode statusCode,
        IEnumerable<object> value)
    {
        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            MaxInvocations = maxInvocations,
            Responder = _ =>
            {
                var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["value"] = value.ToArray()
                };

                string json = JsonSerializer.Serialize(payload);
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Responds with an OData v4 result envelope including the optional "@odata.context" value.
    /// </summary>
    public RequestMockResponseBuilder RespondsWithODataResult(HttpStatusCode statusCode, IEnumerable<object> value,
        string odataContext)
    {
        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            MaxInvocations = maxInvocations,
            Responder = _ =>
            {
                var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["value"] = value.ToArray()
                };

                if (!string.IsNullOrWhiteSpace(odataContext))
                {
                    payload["@odata.context"] = odataContext;
                }

                string json = JsonSerializer.Serialize(payload);
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Configures the mock response to return the specified raw string content with a default HTTP status code of 200 (OK)
    /// and a default content type of "application/json".
    /// </summary>
    /// <param name="content">The body content to return in the HTTP response.</param>
    public RequestMockResponseBuilder RespondsWithContent(string content)
    {
        return RespondsWithContent(HttpStatusCode.OK, content, "application/json");
    }

    /// <summary>
    /// Configures the mock to respond with a specific HTTP status code, content, and content type.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to respond with.</param>
    /// <param name="content">The response content as a string.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public RequestMockResponseBuilder RespondsWithContent(HttpStatusCode statusCode, string content,
        string contentType = "application/json")
    {
        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            MaxInvocations = maxInvocations,
            Responder = _ => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, contentType)
            }
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Responds with empty content.
    /// </summary>
    public RequestMockResponseBuilder RespondsWithEmptyContent(HttpStatusCode statusCode = HttpStatusCode.NoContent)
    {
        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            MaxInvocations = maxInvocations,
            Responder = _ => new HttpResponseMessage(statusCode)
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Responds using a custom responder function.
    /// </summary>
    public RequestMockResponseBuilder RespondsWith(Func<RequestInfo, HttpResponseMessage> responder)
    {
        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            MaxInvocations = maxInvocations,
            Responder = responder
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }
}
