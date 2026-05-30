using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
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
    private string? pathPattern;
    private string? queryPattern;
    private string? scheme = "https";
    private string? hostPattern = "localhost";
    private RequestCollection? requestCollection;
    private JsonSerializerOptions? jsonSerializerOptions;

    internal RequestMockBuilder(HttpMock mockBuilder, HttpMethod method)
    {
        this.mockBuilder = mockBuilder;
        Method = method;
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
        scheme = predecessor.scheme ?? scheme;
        hostPattern = predecessor.hostPattern ?? hostPattern;
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
        return With(request => request.Body is not null && Regex.IsMatch(request.Body, regex), $"body matches regex {regex}");
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
            }, $"body matches JSON {json}");
    }

    /// <summary>
    /// Configures the request mock to match requests whose body contains the JSON equivalent to the specified object,
    /// serialized using <see cref="JsonSerializer"/>, ignoring differences in whitespace and layout.
    /// </summary>
    /// <param name="body">The object to serialize to JSON and compare against the request body.</param>
    /// <remarks>
    /// The <paramref name="body"/> is serialized using <see cref="JsonSerializer.Serialize(object?, System.Type, JsonSerializerOptions?)"/>
    /// with the default <see cref="JsonSerializerOptions"/>. The comparison is then performed by comparing the serialized JSON
    /// with the request body using JSON equivalence, ignoring differences in whitespace and layout.
    /// </remarks>
    public RequestMockBuilder WithBody(object body)
    {
        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        var json = JsonSerializer.Serialize(body, jsonSerializerOptions);
        return WithBodyMatchingJson(json);
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
        return With(
            request => request.Body is not null && request.Body.MatchesWildcard(wildcardPattern),
            $"body matches wildcard pattern \"{wildcardPattern}\"");
    }

    /// <summary>
    /// Configures the request mock to match requests that contain the specified header, regardless of its value.
    /// </summary>
    /// <param name="name">The name of the header that must be present on the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    public RequestMockBuilder WithHeader(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        return With(
            request => request.Headers.TryGetValues(name, out _),
            $"header \"{name}\" is present");
    }

    /// <summary>
    /// Configures the request mock to match requests that contain the specified header with a value satisfying the
    /// given wildcard pattern. For headers with multiple values, a match on any single value is sufficient.
    /// </summary>
    /// <param name="name">The name of the header that must be present on the request.</param>
    /// <param name="valuePattern">
    /// The wildcard pattern used to match the header value, where '?' represents any single character and '*' represents
    /// any sequence of characters.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="valuePattern"/> is <c>null</c>.
    /// </exception>
    public RequestMockBuilder WithHeader(string name, string valuePattern)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (valuePattern is null)
        {
            throw new ArgumentNullException(nameof(valuePattern));
        }

        return With(
            request => request.Headers.TryGetValues(name, out var values) &&
                values.Any(value => value.MatchesWildcard(valuePattern)),
            $"header \"{name}\" matches \"{valuePattern}\"");
    }

    /// <summary>
    /// Configures the request mock to match requests that carry an <c>Authorization</c> header using the <c>Bearer</c>
    /// scheme with a token satisfying the given wildcard pattern.
    /// </summary>
    /// <param name="tokenPattern">
    /// The wildcard pattern used to match the bearer token, where '?' represents any single character and '*' represents
    /// any sequence of characters. Defaults to '*', which matches any non-empty token.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenPattern"/> is <c>null</c>.</exception>
    public RequestMockBuilder WithBearerToken(string tokenPattern = "*")
    {
        if (tokenPattern is null)
        {
            throw new ArgumentNullException(nameof(tokenPattern));
        }

        return With(
            request =>
            {
                var authorization = request.Headers.Authorization;
                return authorization is not null &&
                    string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
                    authorization.Parameter is not null &&
                    authorization.Parameter.MatchesWildcard(tokenPattern);
            },
            $"bearer token matches \"{tokenPattern}\"");
    }

    /// <summary>
    /// Configures the request mock to match requests whose <c>Content-Type</c> media type satisfies the given wildcard
    /// pattern. Any parameters such as <c>charset</c> are ignored; only the media type is compared.
    /// </summary>
    /// <param name="mediaTypePattern">
    /// The wildcard pattern used to match the media type, where '?' represents any single character and '*' represents
    /// any sequence of characters.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="mediaTypePattern"/> is <c>null</c>.</exception>
    public RequestMockBuilder WithContentType(string mediaTypePattern)
    {
        if (mediaTypePattern is null)
        {
            throw new ArgumentNullException(nameof(mediaTypePattern));
        }

        return With(
            request => request.ContentType is not null && request.ContentType.MatchesWildcard(mediaTypePattern),
            $"content type matches \"{mediaTypePattern}\"");
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
    public RequestMockBuilder CollectingRequestsIn(RequestCollection collection)
    {
        requestCollection = collection;
        return this;
    }

    /// <summary>
    /// Specifies the <see cref="JsonSerializerOptions"/> to use for all JSON serialization in this mock,
    /// including body matching and response content generation.
    /// </summary>
    public RequestMockBuilder Using(JsonSerializerOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        jsonSerializerOptions = options;
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
    /// Responds with JSON content serialized from the object built by the specified builder and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of object to build and serialize.</typeparam>
    /// <param name="builder">The builder that will construct the object to serialize.</param>
    public RequestMockResponseBuilder RespondsWithJsonContent<T>(IResponseBuilder<T> builder)
    {
        return RespondsWithJsonContent(HttpStatusCode.OK, builder);
    }

    /// <summary>
    /// Responds with JSON content serialized from the specified object and a specific status code.
    /// </summary>
    public RequestMockResponseBuilder RespondsWithJsonContent(HttpStatusCode statusCode, object content)
    {
        var options = jsonSerializerOptions;

        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            Responder = _ =>
            {
                var json = JsonSerializer.Serialize(content, options);
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
    /// Responds with JSON content serialized from the object built by the specified builder and a specific status code.
    /// </summary>
    /// <typeparam name="T">The type of object to build and serialize.</typeparam>
    /// <param name="statusCode">The HTTP status code to respond with.</param>
    /// <param name="builder">The builder that will construct the object to serialize.</param>
    public RequestMockResponseBuilder RespondsWithJsonContent<T>(HttpStatusCode statusCode, IResponseBuilder<T> builder)
    {
        object content = builder.Build()!;
        return RespondsWithJsonContent(statusCode, content);
    }

    /// <summary>
    /// Responds with an RFC 7807 <c>application/problem+json</c> payload describing the specified problem.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to respond with and to include as the <c>status</c> member.</param>
    /// <param name="title">
    /// A short, human-readable summary of the problem type. When <see langword="null"/>, the reason phrase of
    /// <paramref name="statusCode"/> is used.
    /// </param>
    /// <param name="detail">A human-readable explanation specific to this occurrence of the problem.</param>
    /// <param name="type">A URI reference that identifies the problem type.</param>
    /// <param name="instance">A URI reference that identifies the specific occurrence of the problem.</param>
    /// <param name="extensions">Additional members to include in the problem details payload.</param>
    /// <remarks>
    /// The payload is serialized using the same <see cref="JsonSerializerOptions"/> as the other JSON responders
    /// (configurable through <see cref="Using"/>) and the response <c>Content-Type</c> is set to
    /// <c>application/problem+json</c>.
    /// </remarks>
    [SuppressMessage("Maintainability", "AV1561:Signature contains too many parameters",
        Justification = "The parameters mirror the RFC 7807 problem details members for an ergonomic single-call API.")]
    [SuppressMessage("Member Design", "AV1553:Do not use optional parameters with default value null",
        Justification = "The RFC 7807 members are all optional, so null is the natural way to omit them.")]
    public RequestMockResponseBuilder RespondsWithProblemDetails(
        HttpStatusCode statusCode,
        string? title = null,
        string? detail = null,
        string? type = null,
        string? instance = null,
        IDictionary<string, object?>? extensions = null)
    {
        var options = jsonSerializerOptions;

        var problemDetails = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (type is not null)
        {
            problemDetails["type"] = type;
        }

        problemDetails["title"] = title ?? GetReasonPhrase(statusCode);
        problemDetails["status"] = (int)statusCode;

        if (detail is not null)
        {
            problemDetails["detail"] = detail;
        }

        if (instance is not null)
        {
            problemDetails["instance"] = instance;
        }

        if (extensions is not null)
        {
            foreach (var extension in extensions)
            {
                problemDetails[extension.Key] = extension.Value;
            }
        }

        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            Responder = _ =>
            {
                var json = JsonSerializer.Serialize(problemDetails, options);
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/problem+json")
                };
            }
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    private static string? GetReasonPhrase(HttpStatusCode statusCode)
    {
        using var message = new HttpResponseMessage(statusCode);
        return message.ReasonPhrase;
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
    /// Configures an HTTP response with an OData v4 result envelope containing a single entity built by the specified builder
    /// and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of entity to build.</typeparam>
    /// <param name="builder">The builder that will construct the entity to include in the OData result.</param>
    public RequestMockResponseBuilder RespondsWithODataResult<T>(IResponseBuilder<T> builder)
    {
        return RespondsWithODataResult(HttpStatusCode.OK, builder);
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
    /// Configures an HTTP response with an OData v4 result envelope containing a single entity built by the specified builder.
    /// </summary>
    /// <typeparam name="T">The type of entity to build.</typeparam>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="builder">The builder that will construct the entity to include in the OData result.</param>
    public RequestMockResponseBuilder RespondsWithODataResult<T>(HttpStatusCode statusCode,
        IResponseBuilder<T> builder)
    {
        object value = builder.Build()!;
        return RespondsWithODataResult(statusCode, value);
    }

    /// <summary>
    /// Responds with an OData v4 result envelope: { "value": [...] } and status code 200 (OK).
    /// </summary>
    public RequestMockResponseBuilder RespondsWithODataResult(IEnumerable<object> value)
    {
        return RespondsWithODataResult(HttpStatusCode.OK, value);
    }

    /// <summary>
    /// Responds with an OData v4 result envelope containing entities built by the specified builders and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of entities to build.</typeparam>
    /// <param name="builders">The builders that will construct the entities to include in the OData result.</param>
    public RequestMockResponseBuilder RespondsWithODataResult<T>(IEnumerable<IResponseBuilder<T>> builders)
    {
        return RespondsWithODataResult(HttpStatusCode.OK, builders);
    }

    /// <summary>
    /// Responds with an OData v4 result envelope: { "value": [...] } and a specific status code.
    /// </summary>
    public RequestMockResponseBuilder RespondsWithODataResult(HttpStatusCode statusCode,
        IEnumerable<object> value)
    {
        var options = jsonSerializerOptions;

        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            Responder = _ =>
            {
                var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["value"] = value.ToArray()
                };

                string json = JsonSerializer.Serialize(payload, options);
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
    /// Responds with an OData v4 result envelope containing entities built by the specified builders and a specific status code.
    /// </summary>
    /// <typeparam name="T">The type of entities to build.</typeparam>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="builders">The builders that will construct the entities to include in the OData result.</param>
    public RequestMockResponseBuilder RespondsWithODataResult<T>(HttpStatusCode statusCode,
        IEnumerable<IResponseBuilder<T>> builders)
    {
        var builtValues = builders.Select(b => b.Build()).Cast<object>();
        return RespondsWithODataResult(statusCode, builtValues);
    }

    /// <summary>
    /// Responds with an OData v4 result envelope including the optional "@odata.context" value.
    /// </summary>
    public RequestMockResponseBuilder RespondsWithODataResult(HttpStatusCode statusCode, IEnumerable<object> value,
        string odataContext)
    {
        var options = jsonSerializerOptions;

        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
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

                string json = JsonSerializer.Serialize(payload, options);
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
    /// Responds with an OData v4 result envelope containing entities built by the specified builders and including the optional "@odata.context" value.
    /// </summary>
    /// <typeparam name="T">The type of entities to build.</typeparam>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="builders">The builders that will construct the entities to include in the OData result.</param>
    /// <param name="odataContext">The OData context URL to include in the response.</param>
    public RequestMockResponseBuilder RespondsWithODataResult<T>(HttpStatusCode statusCode, IEnumerable<IResponseBuilder<T>> builders,
        string odataContext)
    {
        var builtValues = builders.Select(b => b.Build()).Cast<object>();
        return RespondsWithODataResult(statusCode, builtValues, odataContext);
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
            Responder = _ => new HttpResponseMessage(statusCode)
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Responds with the contents of the specified file, streamed freshly for each matching request, and status code 200 (OK).
    /// </summary>
    /// <remarks>
    /// The file is opened for reading on every matching request, so the mock can be invoked multiple times and each
    /// invocation receives its own readable stream. When <paramref name="contentType"/> is not supplied, the content type
    /// is inferred from the file extension, defaulting to "application/octet-stream".
    /// </remarks>
    /// <param name="path">The path of the file whose contents are streamed as the response body.</param>
    /// <param name="contentType">
    /// The MIME type of the response content. When <see langword="null"/>, the content type is inferred from the file extension.
    /// </param>
#pragma warning disable AV1553 // Optional parameter defaults to null to mirror the documented public API contract.
    public RequestMockResponseBuilder RespondsWithFile(string path, string? contentType = null)
#pragma warning restore AV1553
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        string resolvedContentType = contentType ?? InferContentTypeFromExtension(path);

        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            Responder = _ =>
            {
                var stream = File.OpenRead(path);
                var content = new StreamContent(stream)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue(resolvedContentType),
                        ContentLength = stream.Length
                    }
                };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                };
            }
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Responds with the specified raw bytes and status code 200 (OK), buffering the payload so the mock can be invoked
    /// multiple times.
    /// </summary>
    /// <param name="content">The bytes to return as the response body.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public RequestMockResponseBuilder RespondsWithBytes(byte[] content, string contentType)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (contentType is null)
        {
            throw new ArgumentNullException(nameof(contentType));
        }

        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            Responder = _ =>
            {
                var byteContent = new ByteArrayContent(content)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue(contentType)
                    }
                };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = byteContent
                };
            }
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Responds with the specified stream and status code 200 (OK).
    /// </summary>
    /// <remarks>
    /// Note: A <see cref="Stream"/> can only be consumed once, so the same <paramref name="stream"/> instance is used for all
    /// matching requests. If the mock may be invoked more than once, prefer <see cref="RespondsWithBytes(byte[], string)"/> or
    /// <see cref="RespondsWithFile(string, string?)"/>, which provide fresh content for every request.
    /// </remarks>
    /// <param name="stream">The stream whose contents are returned as the response body.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public RequestMockResponseBuilder RespondsWithStream(Stream stream, string contentType)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (contentType is null)
        {
            throw new ArgumentNullException(nameof(contentType));
        }

#pragma warning disable CA2000 // The content is owned by the returned HttpResponseMessage and disposed by the caller.
        var content = new StreamContent(stream)
        {
            Headers =
            {
                ContentType = new MediaTypeHeaderValue(contentType)
            }
        };
#pragma warning restore CA2000

        var mock = new RequestMock
        {
            Method = Method,
            PathPattern = pathPattern,
            QueryPattern = queryPattern,
            Scheme = scheme,
            HostPattern = hostPattern,
            CustomMatchers = customMatchers,
            RequestCollection = requestCollection,
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            }
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    /// <summary>
    /// Responds with the specified HTTP content and status code 200 (OK).
    /// </summary>
    /// <param name="content">The HTTP content to include in the response.</param>
    /// <remarks>
    /// Note: The same <paramref name="content"/> instance is used for all matching requests.
    /// If the mock will be called multiple times, consider using the <see cref="RespondsWith(Func{RequestInfo, HttpResponseMessage})"/>
    /// overload to create a new content instance for each request.
    /// </remarks>
    public RequestMockResponseBuilder RespondsWith(HttpContent content)
    {
        return RespondsWith(HttpStatusCode.OK, content);
    }

    /// <summary>
    /// Responds with the specified HTTP content and status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="content">The HTTP content to include in the response.</param>
    /// <remarks>
    /// Note: The same <paramref name="content"/> instance is used for all matching requests.
    /// If the mock will be called multiple times, consider using the <see cref="RespondsWith(Func{RequestInfo, HttpResponseMessage})"/>
    /// overload to create a new content instance for each request.
    /// </remarks>
    public RequestMockResponseBuilder RespondsWith(HttpStatusCode statusCode, HttpContent content)
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
            Responder = _ => new HttpResponseMessage(statusCode)
            {
                Content = content
            }
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
            Responder = responder
        };

        mockBuilder.AddMock(mock);
        return new RequestMockResponseBuilder(mock);
    }

    private static string InferContentTypeFromExtension(string path)
    {
        string extension = Path.GetExtension(path).ToUpperInvariant();

        return extension switch
        {
            ".TXT" => "text/plain",
            ".JSON" => "application/json",
            ".XML" => "application/xml",
            ".HTM" or ".HTML" => "text/html",
            ".CSV" => "text/csv",
            ".PDF" => "application/pdf",
            ".PNG" => "image/png",
            ".JPG" or ".JPEG" => "image/jpeg",
            ".GIF" => "image/gif",
            ".SVG" => "image/svg+xml",
            ".ZIP" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
