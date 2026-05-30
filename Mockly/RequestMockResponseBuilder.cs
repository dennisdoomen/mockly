using System.Net;
using System.Text.Json;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Fluent builder for configuring invocation limits and sequenced responses on a configured mock response.
/// </summary>
public class RequestMockResponseBuilder
{
    private readonly RequestMock requestMock;
    private readonly JsonSerializerOptions? jsonSerializerOptions;

    internal RequestMockResponseBuilder(RequestMock requestMock, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        this.requestMock = requestMock;
        this.jsonSerializerOptions = jsonSerializerOptions;
    }

    /// <summary>
    /// Limits this mock to be used exactly once.
    /// </summary>
    public RequestMockResponseBuilder Once()
    {
        requestMock.MaxInvocations = 1;
        return this;
    }

    /// <summary>
    /// Limits this mock to be used exactly twice.
    /// </summary>
    public RequestMockResponseBuilder Twice()
    {
        requestMock.MaxInvocations = 2;
        return this;
    }

    /// <summary>
    /// Limits this mock to be used exactly the specified number of times.
    /// </summary>
    public RequestMockResponseBuilder Times(uint count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot limit the number of mock invocations to less than 1");
        }

        requestMock.MaxInvocations = count;
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence that responds with the specified HTTP status code.
    /// </summary>
    /// <remarks>
    /// Consecutive matching requests are served by consecutive responses in the order they were configured.
    /// Once the sequence is exhausted, the last response is repeated for every subsequent request.
    /// </remarks>
    public RequestMockResponseBuilder Then(HttpStatusCode statusCode)
    {
        requestMock.AppendResponder(ResponderFactory.Status(statusCode));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence that uses the specified custom responder function.
    /// </summary>
    /// <remarks>
    /// Consecutive matching requests are served by consecutive responses in the order they were configured.
    /// Once the sequence is exhausted, the last response is repeated for every subsequent request.
    /// </remarks>
    public RequestMockResponseBuilder Then(Func<RequestInfo, HttpResponseMessage> responder)
    {
        requestMock.AppendResponder(responder);
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence that responds with the specified HTTP content and status code 200 (OK).
    /// </summary>
    /// <param name="content">The HTTP content to include in the response.</param>
    /// <remarks>
    /// Note: The same <paramref name="content"/> instance is reused every time this response is served, including when
    /// it is the last response in the sequence and repeats. If the response will be served multiple times, consider
    /// using the <see cref="Then(Func{RequestInfo, HttpResponseMessage})"/> overload to create a new content instance
    /// for each request.
    /// </remarks>
    public RequestMockResponseBuilder Then(HttpContent content)
    {
        return Then(HttpStatusCode.OK, content);
    }

    /// <summary>
    /// Appends a response to the sequence that responds with the specified HTTP content and status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="content">The HTTP content to include in the response.</param>
    /// <remarks>
    /// Note: The same <paramref name="content"/> instance is reused every time this response is served, including when
    /// it is the last response in the sequence and repeats. If the response will be served multiple times, consider
    /// using the <see cref="Then(Func{RequestInfo, HttpResponseMessage})"/> overload to create a new content instance
    /// for each request.
    /// </remarks>
    public RequestMockResponseBuilder Then(HttpStatusCode statusCode, HttpContent content)
    {
        requestMock.AppendResponder(ResponderFactory.HttpContent(statusCode, content));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence with JSON content serialized from the specified object and status code 200 (OK).
    /// </summary>
    public RequestMockResponseBuilder ThenRespondsWithJsonContent(object content)
    {
        return ThenRespondsWithJsonContent(HttpStatusCode.OK, content);
    }

    /// <summary>
    /// Appends a response to the sequence with JSON content serialized from the object built by the specified builder and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of object to build and serialize.</typeparam>
    /// <param name="builder">The builder that will construct the object to serialize.</param>
    public RequestMockResponseBuilder ThenRespondsWithJsonContent<T>(IResponseBuilder<T> builder)
    {
        return ThenRespondsWithJsonContent(HttpStatusCode.OK, builder);
    }

    /// <summary>
    /// Appends a response to the sequence with JSON content serialized from the specified object and a specific status code.
    /// </summary>
    public RequestMockResponseBuilder ThenRespondsWithJsonContent(HttpStatusCode statusCode, object content)
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
    public RequestMockResponseBuilder ThenRespondsWithJsonContent<T>(HttpStatusCode statusCode, IResponseBuilder<T> builder)
    {
        object content = builder.Build()!;
        return ThenRespondsWithJsonContent(statusCode, content);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing a single entity and status code 200 (OK).
    /// </summary>
    /// <param name="value">The entity to include in the OData result.</param>
    public RequestMockResponseBuilder ThenRespondsWithODataResult(object value)
    {
        return ThenRespondsWithODataResult(HttpStatusCode.OK, [value]);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing a single entity built by the specified builder and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of entity to build.</typeparam>
    /// <param name="builder">The builder that will construct the entity to include in the OData result.</param>
    public RequestMockResponseBuilder ThenRespondsWithODataResult<T>(IResponseBuilder<T> builder)
    {
        return ThenRespondsWithODataResult(HttpStatusCode.OK, builder);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing a single entity.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="value">The entity to include in the OData result.</param>
    public RequestMockResponseBuilder ThenRespondsWithODataResult(HttpStatusCode statusCode, object value)
    {
        return ThenRespondsWithODataResult(statusCode, [value]);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing a single entity built by the specified builder.
    /// </summary>
    /// <typeparam name="T">The type of entity to build.</typeparam>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="builder">The builder that will construct the entity to include in the OData result.</param>
    public RequestMockResponseBuilder ThenRespondsWithODataResult<T>(HttpStatusCode statusCode, IResponseBuilder<T> builder)
    {
        object value = builder.Build()!;
        return ThenRespondsWithODataResult(statusCode, value);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope: { "value": [...] } and status code 200 (OK).
    /// </summary>
    public RequestMockResponseBuilder ThenRespondsWithODataResult(IEnumerable<object> value)
    {
        return ThenRespondsWithODataResult(HttpStatusCode.OK, value);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope containing entities built by the specified builders and status code 200 (OK).
    /// </summary>
    /// <typeparam name="T">The type of entities to build.</typeparam>
    /// <param name="builders">The builders that will construct the entities to include in the OData result.</param>
    public RequestMockResponseBuilder ThenRespondsWithODataResult<T>(IEnumerable<IResponseBuilder<T>> builders)
    {
        return ThenRespondsWithODataResult(HttpStatusCode.OK, builders);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope: { "value": [...] } and a specific status code.
    /// </summary>
    public RequestMockResponseBuilder ThenRespondsWithODataResult(HttpStatusCode statusCode, IEnumerable<object> value)
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
    public RequestMockResponseBuilder ThenRespondsWithODataResult<T>(HttpStatusCode statusCode, IEnumerable<IResponseBuilder<T>> builders)
    {
        var builtValues = builders.Select(b => b.Build()).Cast<object>();
        return ThenRespondsWithODataResult(statusCode, builtValues);
    }

    /// <summary>
    /// Appends a response to the sequence with an OData v4 result envelope including the optional "@odata.context" value.
    /// </summary>
    public RequestMockResponseBuilder ThenRespondsWithODataResult(HttpStatusCode statusCode, IEnumerable<object> value,
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
    public RequestMockResponseBuilder ThenRespondsWithODataResult<T>(HttpStatusCode statusCode,
        IEnumerable<IResponseBuilder<T>> builders, string odataContext)
    {
        var builtValues = builders.Select(b => b.Build()).Cast<object>();
        return ThenRespondsWithODataResult(statusCode, builtValues, odataContext);
    }

    /// <summary>
    /// Appends a response to the sequence with the specified raw string content, status code 200 (OK), and content type "application/json".
    /// </summary>
    /// <param name="content">The body content to return in the HTTP response.</param>
    public RequestMockResponseBuilder ThenRespondsWithContent(string content)
    {
        return ThenRespondsWithContent(HttpStatusCode.OK, content, "application/json");
    }

    /// <summary>
    /// Appends a response to the sequence with a specific HTTP status code, content, and content type.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to respond with.</param>
    /// <param name="content">The response content as a string.</param>
    /// <param name="contentType">The MIME type of the response content.</param>
    public RequestMockResponseBuilder ThenRespondsWithContent(HttpStatusCode statusCode, string content,
        string contentType = "application/json")
    {
        requestMock.AppendResponder(ResponderFactory.Content(statusCode, content, contentType));
        return this;
    }

    /// <summary>
    /// Appends a response to the sequence with empty content.
    /// </summary>
    public RequestMockResponseBuilder ThenRespondsWithEmptyContent(HttpStatusCode statusCode = HttpStatusCode.NoContent)
    {
        requestMock.AppendResponder(ResponderFactory.Status(statusCode));
        return this;
    }
}
