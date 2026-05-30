using System.Net;
using System.Text.Json;
#if NET472_OR_GREATER
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Mockly;

/// <summary>
/// Fluent builder returned by every <c>RespondsWith*</c> method that lets you chain additional
/// responses via <c>Then*</c> methods and configure per-response invocation counts via
/// <see cref="Once"/>, <see cref="Twice"/>, and <see cref="Times"/>.
/// </summary>
/// <remarks>
/// <c>Once()</c>, <c>Twice()</c>, and <c>Times(n)</c> apply to the response that was most recently
/// configured. For example:
/// <code>
/// mock.ForGet("/api")
///     .RespondsWithStatus(503).Once()       // 503 is returned once, then the sequence advances
///     .ThenRespondsWithStatus(200).Twice()   // 200 is returned twice, then the mock is exhausted
/// </code>
/// When the last response in the sequence has no explicit count it repeats indefinitely;
/// if it has an explicit count the mock becomes exhausted after that many invocations.
/// </remarks>
public class SequencedResponseBuilder : RequestMockResponseBuilder
{
    private readonly RequestMock requestMock;
    private readonly JsonSerializerOptions? jsonSerializerOptions;

    internal SequencedResponseBuilder(RequestMock requestMock, JsonSerializerOptions? jsonSerializerOptions = null)
        : base(requestMock)
    {
        this.requestMock = requestMock;
        this.jsonSerializerOptions = jsonSerializerOptions;
    }

    // ── Methods return SequencedResponseBuilder to keep the chain typed ───────────────────────
    // These intentionally hide base class members with covariant return types.
    // #pragma warning disable AV1010 is needed because the AV1010 rule flags all 'new' hiding,
    // but this pattern is correct and intentional for a fluent builder that needs typed chaining.
#pragma warning disable AV1010

    /// <inheritdoc cref="RequestMockResponseBuilder.Once"/>
    public new SequencedResponseBuilder Once()
    {
        base.Once();
        return this;
    }

    /// <inheritdoc cref="RequestMockResponseBuilder.Twice"/>
    public new SequencedResponseBuilder Twice()
    {
        base.Twice();
        return this;
    }

    /// <inheritdoc cref="RequestMockResponseBuilder.Times"/>
    public new SequencedResponseBuilder Times(uint count)
    {
        base.Times(count);
        return this;
    }

    /// <inheritdoc cref="RequestMockResponseBuilder.WithHeader(string, string)"/>
    public new SequencedResponseBuilder WithHeader(string name, string value)
    {
        return WithHeader(name, [value]);
    }

    /// <inheritdoc cref="RequestMockResponseBuilder.WithHeader(string, string[])"/>
    public new SequencedResponseBuilder WithHeader(string name, params string[] values)
    {
        base.WithHeader(name, values);
        return this;
    }

#pragma warning restore AV1010

    // ── Sequence methods ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Appends a response to the sequence that responds with the specified HTTP status code.
    /// </summary>
    /// <remarks>
    /// Consecutive matching requests are served by consecutive responses in the order they were configured.
    /// Once the last response's count (if set) is exhausted the mock stops matching; without an explicit
    /// count the last response repeats indefinitely.
    /// </remarks>
    public SequencedResponseBuilder ThenRespondsWithStatus(HttpStatusCode statusCode)
    {
        requestMock.AppendResponder(ResponderFactory.Status(statusCode));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence that uses the specified custom responder function.
    /// </summary>
    /// <remarks>
    /// Consecutive matching requests are served by consecutive responses in the order they were configured.
    /// Once the last response's count (if set) is exhausted the mock stops matching; without an explicit
    /// count the last response repeats indefinitely.
    /// </remarks>
    public SequencedResponseBuilder ThenRespondsWith(Func<RequestInfo, HttpResponseMessage> responder)
    {
        requestMock.AppendResponder(responder);
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence using a custom asynchronous responder function.
    /// </summary>
    /// <param name="responder">An asynchronous function that produces the response for a matching request.</param>
    /// <remarks>
    /// Consecutive matching requests are served by consecutive responses in the order they were configured.
    /// Once the last response's count (if set) is exhausted the mock stops matching; without an explicit
    /// count the last response repeats indefinitely.
    /// </remarks>
    public SequencedResponseBuilder ThenRespondsWith(Func<RequestInfo, Task<HttpResponseMessage>> responder)
    {
        if (responder is null)
        {
            throw new ArgumentNullException(nameof(responder));
        }

        return ThenRespondsWith((request, _) => responder(request));
    }

    /// <summary>
    /// Appends a response to the sequence using a custom asynchronous responder function that receives the
    /// <see cref="CancellationToken"/> flowing from the HTTP pipeline.
    /// </summary>
    /// <param name="responder">
    /// An asynchronous function that produces the response for a matching request, observing the supplied
    /// <see cref="CancellationToken"/>.
    /// </param>
    /// <remarks>
    /// Consecutive matching requests are served by consecutive responses in the order they were configured.
    /// Once the last response's count (if set) is exhausted the mock stops matching; without an explicit
    /// count the last response repeats indefinitely.
    /// </remarks>
    public SequencedResponseBuilder ThenRespondsWith(Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        if (responder is null)
        {
            throw new ArgumentNullException(nameof(responder));
        }

        requestMock.AppendAsyncResponder(responder);
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence that responds with the specified HTTP content and status code 200 (OK).
    /// </summary>
    /// <param name="content">The HTTP content to include in the response.</param>
    /// <remarks>
    /// Note: The same <paramref name="content"/> instance is reused every time this response is served, including when
    /// it is the last response in the sequence and repeats. If the response will be served multiple times, consider
    /// using the <see cref="ThenRespondsWith(Func{RequestInfo, HttpResponseMessage})"/> overload to create a new
    /// content instance for each request.
    /// </remarks>
    public SequencedResponseBuilder ThenRespondsWith(HttpContent content)
    {
        return ThenRespondsWith(HttpStatusCode.OK, content);
    }

    /// <summary>
    /// Appends a response to the sequence that responds with the specified HTTP content and status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="content">The HTTP content to include in the response.</param>
    /// <remarks>
    /// Note: The same <paramref name="content"/> instance is reused every time this response is served, including when
    /// it is the last response in the sequence and repeats. If the response will be served multiple times, consider
    /// using the <see cref="ThenRespondsWith(Func{RequestInfo, HttpResponseMessage})"/> overload to create a new
    /// content instance for each request.
    /// </remarks>
    public SequencedResponseBuilder ThenRespondsWith(HttpStatusCode statusCode, HttpContent content)
    {
        requestMock.AppendResponder(ResponderFactory.HttpContent(statusCode, content));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence with JSON content serialized from the specified object and status code 200 (OK).
    /// </summary>
    public SequencedResponseBuilder ThenRespondsWithJsonContent(object content)
    {
        return ThenRespondsWithJsonContent(HttpStatusCode.OK, content);
    }

    /// <summary>
    /// Appends a response to the sequence with JSON content serialized from the object built by the specified builder and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of object to build and serialize.</typeparam>
    /// <param name="builder">The builder that will construct the object to serialize.</param>
    public SequencedResponseBuilder ThenRespondsWithJsonContent<T>(IResponseBuilder<T> builder)
    {
        return ThenRespondsWithJsonContent(HttpStatusCode.OK, builder);
    }

    /// <summary>
    /// Appends a response to the sequence with JSON content serialized from the specified object and a specific status code.
    /// </summary>
    public SequencedResponseBuilder ThenRespondsWithJsonContent(HttpStatusCode statusCode, object content)
    {
        requestMock.AppendResponder(ResponderFactory.JsonContent(statusCode, content, jsonSerializerOptions));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence with JSON content serialized from the object built by the specified builder and a specific status code.
    /// </summary>
    /// <typeparam name="T">The type of object to build and serialize.</typeparam>
    /// <param name="statusCode">The HTTP status code to respond with.</param>
    /// <param name="builder">The builder that will construct the object to serialize.</param>
    public SequencedResponseBuilder ThenRespondsWithJsonContent<T>(HttpStatusCode statusCode, IResponseBuilder<T> builder)
    {
        object content = builder.Build()!;
        return ThenRespondsWithJsonContent(statusCode, content);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing a single entity and status code 200 (OK).
    /// </summary>
    /// <param name="value">The entity to include in the OData result.</param>
    public SequencedResponseBuilder ThenRespondsWithODataResult(object value)
    {
        return ThenRespondsWithODataResult(HttpStatusCode.OK, [value]);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing a single entity built by the specified builder and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of entity to build.</typeparam>
    /// <param name="builder">The builder that will construct the entity to include in the OData result.</param>
    public SequencedResponseBuilder ThenRespondsWithODataResult<T>(IResponseBuilder<T> builder)
    {
        return ThenRespondsWithODataResult(HttpStatusCode.OK, builder);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing a single entity.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="value">The entity to include in the OData result.</param>
    public SequencedResponseBuilder ThenRespondsWithODataResult(HttpStatusCode statusCode, object value)
    {
        return ThenRespondsWithODataResult(statusCode, [value]);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing a single entity built by the specified builder.
    /// </summary>
    /// <typeparam name="T">The type of entity to build.</typeparam>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="builder">The builder that will construct the entity to include in the OData result.</param>
    public SequencedResponseBuilder ThenRespondsWithODataResult<T>(HttpStatusCode statusCode, IResponseBuilder<T> builder)
    {
        object value = builder.Build()!;
        return ThenRespondsWithODataResult(statusCode, value);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope: { "value": [...] } and status code 200 (OK).
    /// </summary>
    public SequencedResponseBuilder ThenRespondsWithODataResult(IEnumerable<object> value)
    {
        return ThenRespondsWithODataResult(HttpStatusCode.OK, value);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing entities built by the specified builders and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of entities to build.</typeparam>
    /// <param name="builders">The builders that will construct the entities to include in the OData result.</param>
    public SequencedResponseBuilder ThenRespondsWithODataResult<T>(IEnumerable<IResponseBuilder<T>> builders)
    {
        return ThenRespondsWithODataResult(HttpStatusCode.OK, builders);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope: { "value": [...] } and a specific status code.
    /// </summary>
    public SequencedResponseBuilder ThenRespondsWithODataResult(HttpStatusCode statusCode, IEnumerable<object> value)
    {
        requestMock.AppendResponder(ResponderFactory.ODataResult(statusCode, value, odataContext: null, jsonSerializerOptions));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing entities built by the specified builders and a specific status code.
    /// </summary>
    /// <typeparam name="T">The type of entities to build.</typeparam>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="builders">The builders that will construct the entities to include in the OData result.</param>
    public SequencedResponseBuilder ThenRespondsWithODataResult<T>(HttpStatusCode statusCode, IEnumerable<IResponseBuilder<T>> builders)
    {
        var builtValues = builders.Select(b => b.Build()).Cast<object>();
        return ThenRespondsWithODataResult(statusCode, builtValues);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope including the optional "@odata.context" value.
    /// </summary>
    public SequencedResponseBuilder ThenRespondsWithODataResult(HttpStatusCode statusCode, IEnumerable<object> value,
        string odataContext)
    {
        requestMock.AppendResponder(ResponderFactory.ODataResult(statusCode, value, odataContext, jsonSerializerOptions));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing entities built by the specified builders and including the optional "@odata.context" value.
    /// </summary>
    /// <typeparam name="T">The type of entities to build.</typeparam>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="builders">The builders that will construct the entities to include in the OData result.</param>
    /// <param name="odataContext">The OData context URL to include in the response.</param>
    public SequencedResponseBuilder ThenRespondsWithODataResult<T>(HttpStatusCode statusCode,
        IEnumerable<IResponseBuilder<T>> builders, string odataContext)
    {
        var builtValues = builders.Select(b => b.Build()).Cast<object>();
        return ThenRespondsWithODataResult(statusCode, builtValues, odataContext);
    }

    /// <summary>
    /// Appends a response to the sequence with the specified raw string content, status code 200 (OK), and content type "application/json".
    /// </summary>
    /// <param name="content">The body content to return in the HTTP response.</param>
    public SequencedResponseBuilder ThenRespondsWithContent(string content)
    {
        return ThenRespondsWithContent(HttpStatusCode.OK, content);
    }

    /// <summary>
    /// Appends a response to the sequence with a specific HTTP status code, content, and content type.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to respond with.</param>
    /// <param name="content">The response content as a string.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public SequencedResponseBuilder ThenRespondsWithContent(HttpStatusCode statusCode, string content,
        string contentType = "application/json")
    {
        requestMock.AppendResponder(ResponderFactory.Content(statusCode, content, contentType));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence with empty content.
    /// </summary>
    public SequencedResponseBuilder ThenRespondsWithEmptyContent(HttpStatusCode statusCode = HttpStatusCode.NoContent)
    {
        requestMock.AppendResponder(ResponderFactory.Status(statusCode));
        return this;
    }
}

